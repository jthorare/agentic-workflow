#!/usr/bin/env python3
"""Strip a built TDD page down to the plain handoff an agent needs (decision D8: "takes the rich react app output and
strips it down to optimized handoff").

Usage:
  strip_page.py <tdd.html> [--out-dir D]

Takes a BUILT page (a house TDD page rendered by tdd/app, an older system-design artifact, or a claude.ai export of
either) and writes three files into D (default: <dir of tdd.html>/<name>.stripped/, beside the page; write_agents.py
writes its own copy into agents/page/, so the default never touches the agents lane):
  plain.html    the server-rendered DOM only. Removed: <script> (and the JSON island), <style>, <template>, the
                contents rail (.fnav), hotspot anchors (.hs), overlays (.hs-pop, .ref-pop, .ref-panel, <dialog>,
                .shot-scrim, .drawer), buttons, form controls, inline SVG icons, hidden and aria-hidden elements.
                Kept: ids, headings, tables, lists, code, figures (<img src alt> + <figcaption>). Every <details> gets
                `open`. a.ref collapses to its text plus "(→ #id)", a status chip or pill to "[text]", a legend to
                "Legend: label = tone · ...". Classes, styles, data-* and aria-* attributes are dropped.
  plain.txt     readable text: "#" heading markers with {#id}, one paragraph per line, "- " list items, table rows
                as "|"-joined lines, code indented four spaces
  outline.json  headings (and the summaries of <details> that carry an id) with ids and nesting
Each file starts with a one-line header naming the source file and its sha256. Prints one summary line. Exit 0; 1 on
bad input.

Library (write_agents.py loads this file by path; python3 -I leaves the script's directory off sys.path):
  parse(html) -> Node                                  tolerant tree from html.parser; text children are str
  strip(root) -> (content Node, title, removed counts)  mutates root
  to_html(node) -> str
  render(node_or_html, mode="md"|"txt", **hooks) -> list[str]    one string per block
  render_inline(node_or_html, mode="md"|"txt", **hooks) -> str   one line
  outline(node) -> list[{level, kind, id, text, children}]
  hooks: ref(id, text) -> str, cite(text) -> str, href(url, text) -> str|None, img(src, alt) -> str|None,
         pre(lang, text) -> None (called once per <pre>), heading_shift=int
Standard library only."""
import hashlib, html as _html, json, os, re, sys
from html.parser import HTMLParser
sys.dont_write_bytecode = True

VERSION = "strip_page/1"
VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}
BLOCK = {"address", "article", "aside", "blockquote", "body", "caption", "dd", "details", "dialog", "div", "dl", "dt",
         "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header",
         "hgroup", "hr", "html", "li", "main", "nav", "ol", "p", "pre", "section", "summary", "table", "tbody", "td",
         "tfoot", "th", "thead", "tr", "ul", "#root"}
P_CLOSERS = BLOCK - {"td", "th", "tr", "tbody", "thead", "tfoot", "caption", "li", "dd", "dt", "summary", "#root"}
HEADINGS = {"h1", "h2", "h3", "h4", "h5", "h6"}
DROP_TAGS = {"script", "style", "template", "noscript", "button", "dialog", "link", "meta", "iframe", "object", "canvas",
             "select", "textarea", "head", "title", "base"}
DROP_CLASSES = {"fnav", "fnav-toggle", "hs", "hs-pop", "ref-pop", "ref-panel", "shot-scrim", "drawer", "sr-only", "zoom"}
KEEP_ATTRS = {"a": {"href"}, "img": {"src", "alt", "width", "height"}, "details": {"open"}, "td": {"colspan", "rowspan"},
              "th": {"colspan", "rowspan", "scope"}, "ol": {"start", "reversed"}, "time": {"datetime"}}
BOOL_ATTRS = {"open", "reversed", "checked", "disabled", "hidden"}
BR = " "   # line-break sentinel that survives whitespace collapsing


class Node:
    __slots__ = ("tag", "attrs", "children", "parent")

    def __init__(self, tag, attrs=None, parent=None):
        self.tag, self.attrs, self.children, self.parent = tag, dict(attrs or {}), [], parent

    @property
    def classes(self):
        return (self.attrs.get("class") or "").split()

    def get(self, k, d=None):
        return self.attrs.get(k, d)

    def text(self):
        return "".join(c if isinstance(c, str) else c.text() for c in self.children)

    def elements(self):
        """Depth-first, document order, self included."""
        stack = [self]
        while stack:
            n = stack.pop()
            yield n
            stack.extend(reversed([c for c in n.children if isinstance(c, Node)]))

    def find(self, pred):
        return next((e for e in self.elements() if e is not self and pred(e)), None)


class _Builder(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.root = Node("#root")
        self.cur = self.root

    def _nearest(self, tags, stops):
        n = self.cur
        while n is not None and n is not self.root:
            if n.tag in tags:
                return n
            if n.tag in stops:
                return None
            n = n.parent
        return None

    def _close(self, n):
        if n is not None:
            self.cur = n.parent

    def handle_starttag(self, tag, attrs):
        tag = tag.lower()
        if tag in P_CLOSERS and self.cur.tag == "p":
            self._close(self.cur)
        if tag == "li":
            self._close(self._nearest({"li"}, {"ul", "ol", "menu"}))
        elif tag in ("dt", "dd"):
            self._close(self._nearest({"dt", "dd"}, {"dl"}))
        elif tag == "tr":
            self._close(self._nearest({"tr"}, {"table", "thead", "tbody", "tfoot"}))
        elif tag in ("td", "th"):
            self._close(self._nearest({"td", "th"}, {"tr", "table"}))
        elif tag in ("thead", "tbody", "tfoot"):
            self._close(self._nearest({"thead", "tbody", "tfoot"}, {"table"}))
        n = Node(tag, [(k.lower(), v if v is not None else "") for k, v in attrs], self.cur)
        self.cur.children.append(n)
        if tag not in VOID:
            self.cur = n

    def handle_startendtag(self, tag, attrs):
        tag = tag.lower()
        n = Node(tag, [(k.lower(), v if v is not None else "") for k, v in attrs], self.cur)
        self.cur.children.append(n)

    def handle_endtag(self, tag):
        tag = tag.lower()
        if tag in VOID:
            return
        n = self.cur
        while n is not None and n is not self.root and n.tag != tag:
            n = n.parent
        if n is None or n is self.root:
            return   # stray end tag
        self.cur = n.parent

    def handle_data(self, data):
        if data:
            self.cur.children.append(data)


def parse(text):
    b = _Builder()
    b.feed(text)
    b.close()
    return b.root


def norm(s):
    return re.sub(r"[ \t\r\n\f\v]+", " ", s).strip()


# ----------------------------------------------------------------------------- strip

def _content_root(root):
    r = root.find(lambda e: e.get("id") == "tdd-root")
    if r:
        return r
    bodies = [e for e in root.elements() if e.tag == "body"]
    return bodies[-1] if bodies else root   # a claude.ai export wraps the page in a host document: take the inner body


def strip(root):
    """Remove the page chrome in place. Returns (content node, title, counts of what was removed)."""
    titles = [norm(e.text()) for e in root.elements() if e.tag == "title"]
    title = next((t for t in reversed(titles) if t), "")
    removed = {}
    content = _content_root(root)

    def bump(k):
        removed[k] = removed.get(k, 0) + 1

    def transform(n):
        tag, cls = n.tag, set(n.classes)
        if tag == "svg":
            label = n.get("aria-label") or next((norm(e.text()) for e in n.elements() if e.tag == "title"), "")
            bump("svg")
            return f"[inline SVG: {label}]" if label else None
        if tag in DROP_TAGS:
            bump(tag)
            return None
        hit = cls & DROP_CLASSES
        if hit:
            bump("." + sorted(hit)[0])
            return None
        if "hidden" in n.attrs and (n.attrs["hidden"] or "").lower() != "until-found":
            bump("[hidden]")
            return None
        if n.get("aria-hidden") == "true":
            bump("[aria-hidden]")
            return None
        if tag == "nav" and (n.get("aria-label") or "").lower() == "contents":
            bump("nav")
            return None
        if tag == "a" and ("ref" in cls or "data-ref" in n.attrs):
            rid = n.get("data-ref") or (n.get("href") or "").lstrip("#")
            t = norm(n.text())
            bump("a.ref")
            return f"{t} (→ #{rid})" if rid else t
        if tag == "input":
            if (n.get("type") or "").lower() == "checkbox":
                return "[x] " if "checked" in n.attrs else "[ ] "
            return None
        if tag == "span" and cls & {"chip", "pill"}:
            return f" [{norm(n.text())}] "
        if "key" in cls and tag in ("span", "div"):
            items = []
            for sp in n.children:
                if isinstance(sp, Node):
                    sw = next((e for e in sp.elements() if "sw" in e.classes), None)
                    tone = " ".join(c for c in (sw.classes if sw else []) if c != "sw")
                    label = norm(sp.text())
                    if label:
                        items.append(f"{label} = {tone}" if tone else label)
            return (" Legend: " + " · ".join(items) + ". ") if items else None
        if tag == "pre":
            lang = n.get("data-lang") or ""
            code = next((c for c in n.children if isinstance(c, Node) and c.tag == "code"), None)
            if code is not None:
                m = next((re.match(r"(?:lang|language)-(\S+)", c) for c in code.classes if re.match(r"(?:lang|language)-", c)), None)
                lang = lang or (m.group(1) if m else "")
                code.attrs = {"class": f"language-{lang}"} if lang else {}
                code.children = [code.text()]   # drop highlighting spans, keep the raw text
            n.attrs = {}
            return n
        clean(n)
        if tag == "details":
            n.attrs["open"] = ""
        allowed = {"id"} | KEEP_ATTRS.get(tag, set())
        n.attrs = {k: v for k, v in n.attrs.items() if k in allowed}
        if tag == "span" and not n.attrs:
            # React SSR puts no whitespace between elements: pad unwrapped spans so words don't glue together
            if any(a.tag in ("pre", "code") for a in _ancestors(n)):
                return n.children
            return [" " + ("· " if "one" in cls else "")] + n.children + [" "]
        return n

    def clean(n):
        out = []
        for ch in n.children:
            if isinstance(ch, str):
                out.append(ch)
                continue
            r = transform(ch)
            if r is None:
                continue
            for x in (r if isinstance(r, list) else [r]):
                if isinstance(x, Node):
                    x.parent = n
                out.append(x)
        n.children = out

    if content is not root:
        for e in root.elements():
            if e.tag in ("script", "style", "template") and not any(a is content for a in _ancestors(e)):
                bump(e.tag + " (outside the page root)")
    clean(content)
    return content, title, removed


def _ancestors(e):
    p = e.parent
    while p is not None:
        yield p
        p = p.parent


def to_html(n):
    if isinstance(n, str):
        return _html.escape(n, quote=False)
    inner = "".join(to_html(c) for c in n.children)
    if n.tag == "#root":
        return inner
    attrs = "".join(f" {k}" if (k in BOOL_ATTRS and v == "") else f' {k}="{_html.escape(v)}"' for k, v in n.attrs.items())
    if n.tag in VOID:
        return f"<{n.tag}{attrs}>"
    nl = "\n" if n.tag in BLOCK else ""
    return f"<{n.tag}{attrs}>{inner}</{n.tag}>{nl}"


# ----------------------------------------------------------------------------- markdown / text

def _ticks(s):
    if "`" not in s:
        return f"`{s}`"
    return f"`` {s} ``"


class Renderer:
    def __init__(self, mode="md", ref=None, cite=None, href=None, img=None, pre=None, heading_shift=0):
        self.md = mode == "md"
        self.ref, self.cite, self.href, self.img_hook, self.pre_hook = ref, cite, href, img, pre
        self.shift = heading_shift

    def sfx(self, aid):
        return f" {{#{aid}}}" if aid else ""

    # blocks
    def blocks(self, n):
        out, run = [], []

        def flush():
            t = self._finish("".join(run))
            run.clear()
            if t:
                out.append(t)
        for ch in n.children:
            if isinstance(ch, str):
                run.append(ch)
            elif ch.tag in BLOCK:
                flush()
                out.extend(self.block(ch))
            else:
                run.append(self.inline_el(ch))
        flush()
        return out

    def _finish(self, s):
        s = re.sub(r"[ \t\r\n\f\v]+", " ", s).strip()
        return s.replace(" " + BR, BR).replace(BR + " ", BR).replace(BR, "\n" if self.md else " ").strip()

    def block(self, el):
        t = el.tag
        aid = el.get("id")
        if t in HEADINGS:
            lvl = max(1, min(6, int(t[1]) + self.shift))
            s = self.inline(el)
            return [("#" * lvl) + " " + s + self.sfx(aid)] if s else []
        if t == "p":
            s = self.inline(el)
            return [s + self.sfx(aid)] if s else ([f"{{#{aid}}}"] if aid else [])
        if t in ("ul", "ol"):
            s = self.list(el)
            return [s] if s else []
        if t == "table":
            s = self.table(el)
            return [s] if s else []
        if t == "pre":
            return [self.pre(el)]
        if t == "hr":
            return ["---"]
        if t == "blockquote":
            inner = "\n\n".join(self.blocks(el)) if self.md else "\n".join(self.blocks(el))
            return ["\n".join(("> " + l) if l else ">" for l in inner.split("\n"))] if inner else []
        if t == "details":
            summ = next((c for c in el.children if isinstance(c, Node) and c.tag == "summary"), None)
            head = self.inline(summ) if summ is not None else ""
            rest = Node("div")
            rest.children = [c for c in el.children if c is not summ]
            out = [((f"**{head}**" if self.md else head) + self.sfx(aid))] if (head or aid) else []
            return out + self.blocks(rest)
        if t == "summary":
            s = self.inline(el)
            return [(f"**{s}**" if self.md else s) + self.sfx(aid)] if s else []
        if t == "dl":
            lines = []
            for c in el.children:
                if isinstance(c, Node) and c.tag in ("dt", "dd", "div"):
                    pairs = [c] if c.tag != "div" else [x for x in c.children if isinstance(x, Node)]
                    for x in pairs:
                        s = self.inline(x)
                        if x.tag == "dt":
                            lines.append("- " + (f"**{s}**" if self.md else s) + self.sfx(x.get("id") or c.get("id")))
                        elif s:
                            lines.append(("  " if lines else "") + s)
            return ["\n".join(lines)] if lines else []
        if t == "li":
            s = self.inline(el)
            return ["- " + s] if s else []
        bs = self.blocks(el)
        if aid:
            if bs and (bs[0].startswith("#") or bs[0].startswith("**")) and "{#" not in bs[0].split("\n")[0]:
                first, _, more = bs[0].partition("\n")
                bs[0] = first + self.sfx(aid) + ("\n" + more if more else "")
            else:
                bs.insert(0, f"{{#{aid}}}")
        return bs

    def list(self, el):
        ordered = el.tag == "ol"
        try:
            num = int(el.get("start") or 1)
        except ValueError:
            num = 1
        lines = []
        for li in el.children:
            if not isinstance(li, Node) or li.tag != "li":
                continue
            marker = f"{num}." if ordered else "-"
            num += 1
            parts = self.blocks(li)
            pad = " " * (len(marker) + 1)
            first = parts[0] if parts else ""
            if li.get("id"):
                f0, _, fr = first.partition("\n")
                first = f0 + self.sfx(li.get("id")) + ("\n" + fr if fr else "")
            body = [f"{marker} {first}".rstrip()]
            for p in parts[1:]:
                body.append(p)
            text = "\n".join(body)
            lines.append("\n".join((pad + l if (i and l) else l) for i, l in enumerate(text.split("\n"))))
        return "\n".join(lines)

    def cell(self, c):
        return " ".join(b.replace("\n", " ") for b in self.blocks(c)).strip()

    def table(self, el):
        rows = []
        for tr in el.elements():
            if tr.tag != "tr":
                continue
            owner = tr.parent
            while owner is not None and owner.tag != "table":
                owner = owner.parent
            if owner is not el:
                continue
            cells = [c for c in tr.children if isinstance(c, Node) and c.tag in ("td", "th")]
            texts = [self.cell(c) for c in cells]
            if tr.get("id") and texts:
                texts[0] += self.sfx(tr.get("id"))
            head = (tr.parent is not None and tr.parent.tag == "thead") or (cells and all(c.tag == "th" for c in cells))
            rows.append((texts, head))
        if not rows:
            return ""
        if not self.md:
            return "\n".join(" | ".join(r) for r, _ in rows)
        ncol = max(len(r) for r, _ in rows)
        esc = lambda s: s.replace("|", "\\|")
        if rows[0][1]:
            head, body = rows[0][0], rows[1:]
        else:
            head, body = [""] * ncol, rows
        line = lambda cells: "| " + " | ".join(esc(x) for x in (cells + [""] * (ncol - len(cells)))) + " |"
        return "\n".join([line(head), "| " + " | ".join(["---"] * ncol) + " |"] + [line(r) for r, _ in body])

    def pre(self, el):
        code = next((c for c in el.children if isinstance(c, Node) and c.tag == "code"), None)
        src = code if code is not None else el
        text = src.text().strip("\n")
        lang = el.get("data-lang") or ""
        if code is not None and not lang:
            m = next((re.match(r"(?:lang|language)-(\S+)", c) for c in code.classes if re.match(r"(?:lang|language)-", c)), None)
            lang = m.group(1) if m else ""
        if self.pre_hook:
            self.pre_hook(lang, text)
        if not self.md:
            return "\n".join("    " + l for l in text.split("\n"))
        fence = "~~~~" if "```" in text else "```"
        return f"{fence}{lang}\n{text}\n{fence}"

    # inline
    def inline(self, el):
        parts = []
        for ch in el.children:
            if isinstance(ch, str):
                parts.append(ch)
            elif ch.tag in BLOCK:
                parts.append(" " + " ".join(b.replace("\n", " ") for b in self.block(ch)) + " ")
            else:
                parts.append(self.inline_el(ch))
        return self._finish("".join(parts))

    def inline_el(self, el):
        t, cls = el.tag, el.classes
        if t == "br":
            return BR
        if t == "img":
            src, alt = el.get("src") or "", norm(el.get("alt") or "")
            if self.img_hook:
                r = self.img_hook(src, alt)
                if r is not None:
                    return r
            if not self.md:
                return f" [image: {alt}] " if alt else " [image] "
            return f" ![{alt.replace('[', '(').replace(']', ')')}]({src}) "
        if t == "a":
            cite = el.find(lambda e: e.tag == "code" and "cite" in e.classes)
            if cite is not None and self.cite:
                return self.cite(norm(cite.text()))
            text = self.inline(el)
            if "ref" in cls or "data-ref" in el.attrs:
                rid = el.get("data-ref") or (el.get("href") or "").lstrip("#")
                if self.ref:
                    return self.ref(rid, text)
                return f"[{text}](#{rid})" if self.md and rid else text
            href = el.get("href") or ""
            if self.href:
                r = self.href(href, text)
                if r is not None:
                    return r
            if not self.md or not href or not text:
                return text
            return f"[{text}]({href})"
        if t == "code":
            s = el.text()
            if "cite" in cls and self.cite:
                return self.cite(norm(s))
            s = norm(s)
            return _ticks(s) if (self.md and s) else s
        if t in ("strong", "b", "em", "i", "del", "s", "strike"):
            s = self.inline(el)
            if not s:
                return ""
            raw = el.text()
            lead = " " if raw[:1].isspace() else ""
            trail = " " if raw[-1:].isspace() else ""
            mark = "**" if t in ("strong", "b") else "*" if t in ("em", "i") else "~~"
            return lead + (f"{mark}{s}{mark}" if self.md else s) + trail
        if t == "input":
            return ("[x] " if "checked" in el.attrs else "[ ] ") if (el.get("type") or "").lower() == "checkbox" else ""
        if t == "span" and ("chip" in cls or "pill" in cls):
            return f" [{self.inline(el)}] "
        return self.inline(el)


def _as_node(x):
    return parse(x) if isinstance(x, str) else x


def render(x, mode="md", **hooks):
    """Block-level render of a node or an HTML string: one string per block."""
    return Renderer(mode, **hooks).blocks(_as_node(x))


def render_inline(x, mode="md", **hooks):
    return Renderer(mode, **hooks).inline(_as_node(x))


# ----------------------------------------------------------------------------- outline

def _owner_id(h):
    p = h.parent
    while p is not None and p.tag != "#root":
        if p.get("id"):
            first = next((e for e in p.elements() if e.tag in HEADINGS or e.tag == "summary"), None)
            return p.get("id") if first is h else None
        p = p.parent
    return None


def outline(node):
    """Headings and id-bearing <details> summaries in document order, nested. A heading inside a fold nests under it."""
    flat = []
    state = {"last": 1}

    def walk(n, floor):
        for ch in n.children:
            if not isinstance(ch, Node):
                continue
            if ch.tag in HEADINGS:
                lvl = max(int(ch.tag[1]), floor)
                state["last"] = lvl
                flat.append({"level": lvl, "kind": "heading", "id": ch.get("id") or _owner_id(ch), "text": norm(ch.text())})
            elif ch.tag == "details" and ch.get("id"):
                saved = state["last"]
                lvl = max(saved + 1, floor)
                summ = next((c for c in ch.children if isinstance(c, Node) and c.tag == "summary"), None)
                flat.append({"level": lvl, "kind": "fold", "id": ch.get("id"), "text": norm(summ.text()) if summ is not None else ""})
                state["last"] = lvl
                walk(ch, lvl + 1)
                state["last"] = saved
            else:
                walk(ch, floor)
    walk(node, 1)
    top, stack = [], []
    for item in flat:
        item["children"] = []
        while stack and stack[-1]["level"] >= item["level"]:
            stack.pop()
        (stack[-1]["children"] if stack else top).append(item)
        stack.append(item)
    return top


def count_outline(items):
    return sum(1 + count_outline(i["children"]) for i in items)


# ----------------------------------------------------------------------------- CLI

def header(src_name, digest):
    return f"stripped from {src_name} · sha256 {digest[:12]} · {VERSION} · generated, do not edit"


def strip_file(path, out_dir):
    raw = open(path, "rb").read()
    digest = hashlib.sha256(raw).hexdigest()
    root = parse(raw.decode("utf-8", "replace"))
    content, title, removed = strip(root)
    head = header(os.path.basename(path), digest)
    body = "".join(to_html(c) for c in content.children).strip() if content.tag in ("body", "html", "#root") else to_html(content).strip()
    body = re.sub(r"\n{3,}", "\n\n", body)
    plain_html = (f"<!doctype html>\n<!-- {head} -->\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>{_html.escape(title)}</title></head>\n"
                  f"<body>\n{body}\n</body></html>\n")
    txt = "\n".join(render(content, "txt"))
    plain_txt = f"> {head}\n" + (f"# {title}\n" if title and not txt.startswith("# ") else "") + txt.rstrip() + "\n"
    outl = outline(content)
    os.makedirs(out_dir, exist_ok=True)
    open(os.path.join(out_dir, "plain.html"), "w", encoding="utf-8").write(plain_html)
    open(os.path.join(out_dir, "plain.txt"), "w", encoding="utf-8").write(plain_txt)
    json.dump({"_generated": head, "title": title, "headings": outl}, open(os.path.join(out_dir, "outline.json"), "w", encoding="utf-8"),
              indent=1, ensure_ascii=False)
    return {"title": title, "in_bytes": len(raw), "html_bytes": len(plain_html.encode()), "txt_bytes": len(plain_txt.encode()),
            "txt_tokens": -(-len(plain_txt) // 4), "headings": count_outline(outl), "removed": removed}


def main(argv):
    pos, out_dir, i = [], None, 0
    while i < len(argv):
        if argv[i] == "--out-dir":
            if i + 1 >= len(argv):
                sys.exit("--out-dir needs a value")
            out_dir = argv[i + 1]
            i += 2
        elif argv[i].startswith("--"):
            sys.exit(f"unknown option {argv[i]}\n{__doc__}")
        else:
            pos.append(argv[i])
            i += 1
    if len(pos) != 1:
        sys.exit(__doc__)
    path = pos[0]
    if not os.path.isfile(path):
        sys.exit(f"{path}: no such file")
    out_dir = out_dir or os.path.join(os.path.dirname(os.path.abspath(path)), os.path.splitext(os.path.basename(path))[0] + ".stripped")
    r = strip_file(path, out_dir)
    rm = ", ".join(f"{v} {k}" for k, v in sorted(r["removed"].items(), key=lambda kv: -kv[1]))
    print(f"wrote {out_dir}: plain.html {r['html_bytes']} bytes (from {r['in_bytes']}), plain.txt {r['txt_bytes']} bytes "
          f"(~{r['txt_tokens']} tokens, chars/4), outline.json {r['headings']} headings; removed {rm or 'nothing'}")


if __name__ == "__main__":
    main(sys.argv[1:])
