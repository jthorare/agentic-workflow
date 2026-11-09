#!/usr/bin/env python3
"""Write the agents/ lane of a TDD packet (decisions D6 and D8): files and mappings sized for the agent tasks that use
a design (build a PR, iterate on a section, semantic search, SQL review, image review), generated from the compiled
content.json so they never drift from the page.

Usage:
  write_agents.py <build-dir> [--packet <dir>] [--out <dir>] [--budget-tokens N] [--link-images] [--force] [--quiet]
  write_agents.py <build-dir> --check [--out <dir>]

  <build-dir>       holds content.json (+ content.files.json) from build_tdd.py, and tdd.html once the app has built it
  --packet <dir>    the packet source (prs/pr-NN.md, diagrams/*.hotspots.json, schema/*.json); default: the parent
                    of <build-dir> when it holds content.json's source file
  --out <dir>       default <build-dir>/agents
  --budget-tokens N per-slice budget, default 8000; a section over it is split at its fold groups ("(part 2)")
  --link-images     reference the build's images in place instead of byte-copying them into agents/images/
  --force           overwrite an --out directory that is not an agents lane
  --check           verify the lane is current; print each problem; exit 1 when stale or missing, 0 when current

The skill runs this after every build (build_tdd.py, then the app, then this). Layout written (paths relative to
--out; every JSON path is relative to --out too):
  README.md            front door (<= 8 KB): what the doc is, status, rev, sha, which file for which task, the index
  llms.txt             the same index in llmstxt.org shape (H1, summary, link lists, ## Optional), relative links
  tdd.md / tdd.txt     the whole document: clean markdown (ids as {#id}, tables, lists, fences, citations as
                       path:line) / plain text, one paragraph per line, for grep and embeddings
  sections/NN-<id>.md  one per section with front matter (id, title, tokens, anchors); a section over the budget is
                       split into NN-<id>.md, NN-<id>-part-2.md, ... at fold-group boundaries
  decisions/D-N.md     one per decision, MADR-style front matter; decisions.json
  citations.json/.md   every path:line citation on the page (section, anchor, slice, href, from/to, sha)
  refs.json            anchors with their definition text and slice, reference links, linked/unlinked tokens
  sql/NN-<section>.sql every ```sql block; code/NN-<section>.<ext> every other fenced block
  schema/              one JSON per schema table (D7) and schema.md (columns, change flags, measured/expected)
  prs/                 pr-NN.md copies (or renders), prs.md index (milestone, title, change, rollback), prs.json
  images/              byte copies of diagrams and mocks, index.json (what each picture holds), <diagram>.md box text
  page/                plain.html + outline.json stripped from tdd.html by strip_page.py (when tdd.html exists)
  map.json             entities (sections, anchors, decisions, PRs, tables, images, citations, repo paths, sql/code,
                       tokens, files) and edges between them
  tokens.json          bytes, sha256 and a token estimate per file (chars/4: an estimate, not a tokenizer)
Every text file names its source and sha in a one-line header (front matter key `generated` in .md files with front
matter, `_generated` as the first key of JSON). Fallback: with no content.json but a tdd.html, a reduced lane is built
from the page through strip_page.py. Prints one summary line.

Library: check(build_dir, out=None) -> list[str]   problems; empty when the lane is current
         write(build_dir, packet=None, out=None, budget=8000, copy_images=True, force=False) -> dict summary
python3 -I, standard library only; imports nothing from system-design."""
import datetime, hashlib, importlib.util, json, os, re, shutil, sys
sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.normpath(os.path.join(HERE, ".."))
TEMPLATES = os.path.join(SKILL, "templates", "agents")
GENERATOR = "write_agents/1"
BUDGET = 8000
README_MAX = 8192
CODE_INLINE_MAX = 60          # lines; a longer block is linked from section slices instead of pasted
PACK = 0.9                    # pack slices to this share of the budget; links and footers are added after packing
EXEMPT = {"tdd.md", "tdd.txt", "map.json", "refs.json", "citations.json", "tokens.json", "images/index.json",
          "page/plain.html", "page/outline.json", "decisions/decisions.json", "prs/prs.json"}


def _load_strip():
    spec = importlib.util.spec_from_file_location("strip_page", os.path.join(HERE, "strip_page.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


sp = _load_strip()


def tok(s):
    return -(-len(s) // 4)


def sha256(b):
    return hashlib.sha256(b if isinstance(b, bytes) else b.encode("utf-8")).hexdigest()


def yq(s):
    return json.dumps("" if s is None else s, ensure_ascii=False)


def rel(target, from_dir):
    return os.path.relpath(target, from_dir or ".").replace(os.sep, "/")


def dirn(p):
    return os.path.dirname(p) or "."


def slugify(s):
    return re.sub(r"-{2,}", "-", re.sub(r"[^a-z0-9]+", "-", (s or "").lower())).strip("-") or "x"


def plain(html_str):
    return sp.render_inline(html_str, "txt") if html_str else ""


def clip(s, n):
    s = re.sub(r"\s+", " ", s or "").strip()
    return s if len(s) <= n else s[: n - 1].rstrip() + "…"


def dump_lines(obj, key_order=None):
    """JSON with one array item per line: small, greppable, still valid JSON."""
    def ser(v, ind):
        if isinstance(v, list) and v and all(isinstance(x, dict) for x in v):
            pad = " " * (ind + 1)
            return "[\n" + ",\n".join(pad + json.dumps(x, ensure_ascii=False) for x in v) + "\n" + " " * ind + "]"
        if isinstance(v, dict) and v and ind == 0:
            pad = " " * (ind + 1)
            return "{\n" + ",\n".join(f"{pad}{json.dumps(k, ensure_ascii=False)}: {ser(x, ind + 1)}" for k, x in v.items()) + "\n}"
        return json.dumps(v, ensure_ascii=False)
    return ser(obj, 0) + "\n"


YAML_WORDS = {"true", "false", "yes", "no", "on", "off", "null", "y", "n"}


def ys(v):
    """A YAML scalar: plain when unambiguous (an id, a word, a date), else a JSON-quoted string."""
    s = str(v)
    if re.fullmatch(r"\d{4}-\d{2}-\d{2}", s):
        return s
    if re.fullmatch(r"[A-Za-z_][\w.\-]*", s) and s.lower() not in YAML_WORDS:
        return s
    return yq(s)


def front(fields):
    lines = ["---"]
    for k, v in fields.items():
        if v is None:
            continue
        if isinstance(v, bool):
            lines.append(f"{k}: {'true' if v else 'false'}")
        elif isinstance(v, (int, float)):
            lines.append(f"{k}: {v}")
        elif isinstance(v, list):
            lines.append(f"{k}: [" + ", ".join(ys(x) for x in v) + "]")
        else:
            lines.append(f"{k}: {ys(v)}")
    lines.append("---")
    return "\n".join(lines)


def kids(n):
    t = n.get("type")
    if t == "cards":
        return [b for c in n.get("cards", []) for b in c.get("blocks", [])]
    if t == "persona":
        return list(n.get("text") or []) + list(n.get("media") or []) + ([n["today"]] if n.get("today") else [])
    if t == "today":
        return list(n.get("media") or []) + list(n.get("blocks") or [])
    if t == "duo":
        return list(n.get("items") or [])
    if t == "cols":
        return [b for col in n.get("cols", []) for b in col]
    if t == "options":
        return [b for o in n.get("options", []) for b in o.get("blocks", [])]
    if t == "list":
        return [c for it in n.get("items", []) for c in it.get("children", [])]
    return list(n.get("blocks") or [])


SQL_LANGS = {"sql", "psql", "postgres", "postgresql", "pgsql", "plpgsql", "mysql", "sqlite"}
EXT = {"ts": "ts", "typescript": "ts", "tsx": "tsx", "js": "js", "javascript": "js", "mjs": "mjs", "jsx": "jsx",
       "py": "py", "python": "py", "sh": "sh", "bash": "sh", "shell": "sh", "zsh": "sh", "console": "sh",
       "yaml": "yaml", "yml": "yaml", "toml": "toml", "json": "jsonc", "jsonc": "jsonc", "go": "go", "rust": "rs",
       "rs": "rs", "swift": "swift", "kotlin": "kt", "kt": "kt", "java": "java", "cs": "cs", "csharp": "cs",
       "c": "c", "cpp": "cpp", "css": "css", "graphql": "graphql", "gql": "graphql", "rb": "rb", "ruby": "rb",
       "proto": "proto", "hcl": "hcl", "tf": "tf", "dockerfile": "dockerfile"}
SLASH = {"ts", "tsx", "js", "mjs", "jsx", "jsonc", "go", "rs", "swift", "kt", "java", "cs", "c", "cpp", "proto"}
HASH = {"py", "sh", "yaml", "toml", "rb", "graphql", "hcl", "tf", "dockerfile"}


def code_ext(lang):
    lang = (lang or "").lower()
    return "sql" if lang in SQL_LANGS else EXT.get(lang, "txt")


def comment(ext, text):
    if ext == "sql":
        return f"-- {text}"
    if ext in SLASH:
        return f"// {text}"
    if ext in HASH:
        return f"# {text}"
    if ext == "css":
        return f"/* {text} */"
    return f"> {text}"


# ============================================================================ the lane

class Lane:
    def __init__(self, build, packet=None, out=None, budget=BUDGET, copy_images=True):
        self.build = os.path.abspath(build)
        self.out = os.path.abspath(out or os.path.join(self.build, "agents"))
        self.budget = budget
        self.copy_images = copy_images
        self.c = json.load(open(os.path.join(self.build, "content.json"), encoding="utf-8"))
        fpath = os.path.join(self.build, "content.files.json")
        self.manifest = json.load(open(fpath, encoding="utf-8")) if os.path.isfile(fpath) else {}
        self.meta = self.c.get("meta") or {}
        self.sha = self.meta.get("sha") or ""
        src = self.c.get("source") or ""
        if packet:
            self.packet = os.path.abspath(packet)
        else:
            parent = os.path.dirname(self.build)
            self.packet = parent if src and os.path.isfile(os.path.join(parent, src)) else None
        self.page = os.path.join(self.build, "tdd.html")
        self.page_sha = sha256(open(self.page, "rb").read())[:12] if os.path.isfile(self.page) else None
        src = ("~" + src[len(os.path.expanduser("~")):]) if isinstance(src, str) and src.startswith(os.path.expanduser("~") + os.sep) else src
        self.head = (f"agents lane · from {src} · rev {self.meta.get('rev') or '-'} · sha {self.sha or 'none'} · "
                     f"content {self.c.get('generated')} · page {self.page_sha or 'none'} · {GENERATOR} · generated, do not edit")
        self.defs = self.c.get("defs") or {}
        self.cite_by_text = {}
        for ct in self.c.get("cites") or []:
            self.cite_by_text.setdefault(ct.get("text"), ct)
        self.files = {}            # agents path -> str (text) or ("copy", abs source path)
        self.warn = []
        # filled by annotate()
        self.near, self.anchor_section, self.codes, self.images, self.prs, self.dec_nodes = {}, {}, [], [], [], {}
        self.code_file, self.img_file, self.pr_file = {}, {}, {}
        # filled by slicing / recording
        self.anchor_file, self.cite_occ, self.ref_links, self.slices = {}, [], [], []

    # ------------------------------------------------------------------ helpers
    def emit(self, path, text):
        self.files[path] = text if text.endswith("\n") else text + "\n"

    def md_head(self):
        return f"> {self.head}"

    def section_title(self, s):
        return f"{s['num']} · {s.get('titleText') or plain(s.get('title'))}"

    # ------------------------------------------------------------------ pass 1: annotate the tree in document order
    def annotate(self):
        for s in self.c.get("sections") or []:
            self.anchor_section[s["id"]] = s["id"]
            self._ann(s.get("blocks") or [], s["id"], s["id"], False)
        n_sql = n_code = 0
        for blk in self.codes:
            ext = code_ext(blk["lang"])
            if ext == "sql":
                n_sql += 1
                path = f"sql/{n_sql:02d}-{blk['section']}.sql"
            else:
                n_code += 1
                path = f"code/{n_code:02d}-{blk['section']}.{ext}"
            blk["file"] = path
            if blk.get("node") is not None:
                self.code_file[id(blk["node"])] = path
        for img in self.images:
            pub = img["node"].get("src") or ""
            img["published"] = pub
            self.img_file[pub] = ("images/" + pub) if self.copy_images else rel(os.path.join(self.build, pub), self.out)
        for p in self.prs:
            self.pr_file[p["pr"].get("label", p["pr"]["n"])] = p["pr"].get("file") or f"prs/pr-{p['pr']['n']:02d}.md"
        for t in self.c.get("schema") or []:
            if t.get("id"):
                self.anchor_file[t["id"]] = "schema/schema.md"

    def _ann(self, nodes, enc, sid, in_today):
        cur = enc
        for n in nodes or []:
            if not isinstance(n, dict):
                continue
            t, nid = n.get("type"), n.get("id")
            if t == "heading" and nid:
                cur = nid
                self.near[id(n)] = nid
                self.anchor_section[nid] = sid
                continue
            here = nid or cur
            self.near[id(n)] = here
            if nid:
                self.anchor_section[nid] = sid
            if t == "code":
                self.codes.append({"node": n, "lang": n.get("lang") or "", "text": n.get("text") or "", "section": sid, "anchor": here, "source": "page"})
            elif t == "diagram":
                self.images.append({"node": n, "kind": "diagram", "in_today": in_today, "section": sid, "anchor": nid or here})
            elif t == "shot":
                self.images.append({"node": n, "kind": "mock", "in_today": in_today, "section": sid, "anchor": here})
            elif t == "table":
                for r in n.get("rows") or []:
                    if r.get("id"):
                        self.anchor_section[r["id"]] = sid
            elif t == "glossary":
                for it in n.get("items") or []:
                    self.anchor_section[it["id"]] = sid
            elif t == "decision" and nid:
                self.dec_nodes[nid] = n
            elif t == "prs":
                for g in n.get("groups") or []:
                    if g.get("id"):
                        self.anchor_section[g["id"]] = sid
                    for p in g.get("prs") or []:
                        self.anchor_section[p["id"]] = sid
                        self.prs.append({"pr": p, "ms": plain(g.get("ms", "")), "note": plain(g.get("note", "")), "section": sid})
                        sp.render(p.get("bodyHtml") or "", "txt", pre=lambda lang, text, p=p: self.codes.append(
                            {"node": None, "lang": lang, "text": text, "section": sid, "anchor": p["id"], "source": p.get("file") or f"prs/pr-{p['n']:02d}.md"}))
            if t == "cards":
                for c in n.get("cards") or []:
                    if c.get("id"):
                        self.anchor_section[c["id"]] = sid
                    self._ann(c.get("blocks") or [], c.get("id") or here, sid, in_today)
                continue
            self._ann(kids(n), here, sid, in_today or t == "today")

    # ------------------------------------------------------------------ inline html -> md / txt
    def hooks(self, cx):
        def ref(aid, text):
            if cx.get("record"):
                self.ref_links.append({"token": text, "to": aid, "from": cx.get("cur"), "section": cx.get("section"), "file": cx.get("file")})
            if cx.get("used") is not None and aid:
                cx["used"].add(aid)
            return self.link(aid, text, cx)

        def href(url, text):
            if url.startswith("#") and len(url) > 1:
                return self.link(url[1:], text, cx)
            if cx["mode"] == "txt":
                return text
            return None

        def cite(text):
            ct = self.cite_by_text.get(text)
            if ct:
                path = ct.get("path") or text
                lines = (f":{ct['from']}" + (f"-{ct['to']}" if ct.get("to") else "")) if ct.get("from") else ""
                shown = path + lines
            else:
                shown = text
            if cx.get("record"):
                self.cite_occ.append({"text": text, "section": cx.get("section"), "anchor": cx.get("cur"), "file": cx.get("file")})
            return f"`{shown}`" if cx["mode"] == "md" else shown

        def img(src, alt):
            target = self.img_file.get(src)
            if not target:
                return None
            if cx["mode"] == "txt":
                return f" [image: {alt}] "
            return f" ![{alt}]({rel(target, dirn(cx['file']))}) "
        return {"ref": ref, "href": href, "cite": cite, "img": img}

    def link(self, aid, text, cx):
        if cx["mode"] == "txt":
            return text
        if cx.get("whole"):
            return f"[{text}](#{aid})"
        if cx.get("sizing"):
            return f"[{text}](00-placeholder-file.md#{aid})"
        tf = self.anchor_file.get(aid)
        if not tf:
            return text
        if tf == cx.get("file"):
            return f"[{text}](#{aid})"
        return f"[{text}]({rel(tf, dirn(cx['file']))}#{aid})"

    def ih(self, s, cx):
        return sp.render_inline(s, cx["mode"], **self.hooks(cx)) if s else ""

    def bh(self, s, cx, shift):
        return sp.render(s, cx["mode"], heading_shift=shift, **self.hooks(cx)) if s else []

    def sfx(self, aid, cx):
        return f" {{#{aid}}}" if (aid and cx.get("ids", True)) else ""

    def bold(self, s, cx):
        return f"**{s}**" if (cx["mode"] == "md" and s) else s

    # ------------------------------------------------------------------ node renderers (return a list of blocks)
    def r_nodes(self, nodes, d, cx):
        out = []
        for n in nodes or []:
            out += self.r_node(n, d, cx)
        return out

    def r_node(self, n, d, cx):
        if not isinstance(n, dict):
            return []
        cx["cur"] = self.near.get(id(n), cx.get("cur"))
        t, md = n.get("type"), cx["mode"] == "md"
        if t == "p":
            s = self.ih(n.get("html"), cx)
            return [s + self.sfx(n.get("id"), cx)] if s else []
        if t == "heading":
            lvl = max(1, min(6, (n.get("level") or 3) + d - 3))
            return ["#" * lvl + " " + self.ih(n.get("html"), cx) + self.sfx(n.get("id"), cx)]
        if t == "list":
            s = self.r_list(n, cx)
            return [s] if s else []
        if t == "table":
            return [self.r_table(n, cx)]
        if t == "code":
            return [self.r_code(n, cx)]
        if t == "callout":
            inner = self.r_nodes(n.get("blocks"), d, cx)
            tone = (n.get("tone") or "note").lower()
            if tone == "quote":
                label = None
            else:
                label = {"warn": "WARNING", "danger": "CAUTION", "good": "TIP"}.get(tone, "NOTE")
            if md:
                lines = ([f"[!{label}]"] if label else []) + "\n\n".join(inner).split("\n")
                return ["\n".join(("> " + l) if l else ">" for l in lines)]
            return [(f"{label}: " if label else "> ") + " ".join(inner)]
        if t == "hr":
            return ["---"]
        if t == "placeholder":
            s = self.ih(n.get("html"), cx)
            return [(f"*{s}*" if md else s) + self.sfx(n.get("id"), cx)]
        if t == "shot":
            return self.r_shot(n, cx)
        if t == "legend":
            return [self.legend(n.get("items"), cx)]
        if t == "diagram":
            return self.r_diagram(n, cx)
        if t == "cards":
            out = []
            for c in n.get("cards") or []:
                out += self.r_card(c, d, cx)
            return out
        if t == "persona":
            out = self.r_head(n, d, cx) + self.r_nodes(n.get("text"), d + 1, cx)
            for m in n.get("media") or []:
                out += self.r_node(m, d + 1, cx)
            if n.get("today"):
                out += self.r_node(n["today"], d + 1, cx)
            return out
        if t == "today":
            out = self.r_head(n, d, cx)
            for m in n.get("media") or []:
                out += self.r_node(m, d, cx)
            return out + self.r_nodes(n.get("blocks"), d, cx)
        if t in ("fold", "group", "decision"):
            return self.r_head(n, d, cx) + self.r_nodes(n.get("blocks"), d + 1, cx)
        if t in ("decisions", "steps", "col", "box"):
            return self.r_nodes(n.get("blocks"), d, cx)
        if t == "duo":
            return self.r_nodes(n.get("items"), d, cx)
        if t == "cols":
            return [b for col in n.get("cols") or [] for b in self.r_nodes(col, d, cx)]
        if t == "options":
            out = []
            for o in n.get("options") or []:
                out.append(self.bold(self.ih(o.get("title"), cx), cx) + (" (recommended)" if o.get("rec") else ""))
                out += self.r_nodes(o.get("blocks"), d, cx)
            return out
        if t == "glossary":
            lines = [f"- {self.bold(it['term'], cx)}{self.sfx(it['id'], cx)}: {self.ih(it.get('html'), cx)}" for it in n.get("items") or []]
            return ["\n".join(lines)] if lines else []
        if t == "prs":
            return self.r_prs(n, d, cx)
        if n.get("blocks"):
            return self.r_nodes(n.get("blocks"), d, cx)
        return []

    def r_head(self, n, d, cx):
        cx["cur"] = self.near.get(id(n), cx.get("cur"))
        t, h = n.get("type"), "#" * max(1, min(6, d))
        if t == "fold":
            out = [f"{h} {self.ih(n.get('title'), cx)}{self.sfx(n.get('id'), cx)}"]
            one = self.ih(n.get("one"), cx)
            if one:
                out.append(f"*{one}*" if cx["mode"] == "md" else one)
            return out
        if t == "group":
            out = [f"{h} {self.ih(n.get('title'), cx)}{self.sfx(n.get('id'), cx)}"]
            sub = self.ih(n.get("sub"), cx)
            return out + ([sub] if sub else [])
        if t == "decision":
            st = self.ih(n.get("status"), cx)
            return [f"{h} {n.get('did')} · {self.ih(n.get('question'), cx)}{self.sfx(n.get('id'), cx)}",
                    f"{self.bold('Status:', cx)} {st or 'not stated'} · state: {n.get('state')}"]
        if t == "persona":
            chip = f" [{n['chip']}]" if n.get("chip") else ""
            return [f"{h} {self.ih(n.get('title'), cx)}{chip}{self.sfx(n.get('id'), cx)}"]
        if t == "today":
            s = self.ih(n.get("summary"), cx)
            return [f"{self.bold((n.get('label') or 'Today') + ':', cx)} {s}".rstrip()]
        return []

    def r_card(self, c, d, cx):
        out, title, aid = [], self.ih(c.get("title"), cx), c.get("id")
        if aid:
            cx["cur"] = aid
        if title:
            out.append(self.bold(title, cx) + self.sfx(aid, cx))
        elif aid and cx.get("ids", True):
            out.append(f"{{#{aid}}}")
        return out + self.r_nodes(c.get("blocks"), d + 1, cx)

    def r_list(self, n, cx, lo=0, hi=None, top=True):
        items = (n.get("items") or [])[lo:hi]
        num = (n.get("start") or 1) + lo
        lines = []
        for it in items:
            marker = f"{num}." if n.get("ordered") else "-"
            num += 1
            text = self.ih(it.get("html"), cx)
            if it.get("check") is not None:
                text = ("[x] " if it["check"] else "[ ] ") + text
            if it.get("cls"):
                text = " ".join(f"[{c}]" for c in it["cls"]) + " " + text
            pad = " " * (len(marker) + 1)
            first, *rest = text.split("\n")
            lines.append(f"{marker} {first}".rstrip())
            lines += [pad + r for r in rest]
            for ch in it.get("children") or []:
                sub = self.r_list(ch, cx, top=False)
                lines += [(pad + l) if l else l for l in sub.split("\n")]
        return "\n".join(lines)

    def r_table(self, n, cx, lo=0, hi=None):
        base = self.near.get(id(n), cx.get("cur"))
        head = [self.ih(h.get("html"), cx) for h in n.get("head") or []]
        body = []
        for r in (n.get("rows") or [])[lo:hi]:
            cx["cur"] = r.get("id") or base
            cells = [self.ih(c.get("html"), cx) for c in r.get("cells") or []]
            if r.get("id") and cells:
                cells[0] += self.sfx(r["id"], cx)
            body.append(cells)
        cx["cur"] = base
        if cx["mode"] == "txt":
            return "\n".join(([" | ".join(head)] if any(head) else []) + [" | ".join(r) for r in body])
        ncol = max([len(head)] + [len(r) for r in body] + [1])
        esc = lambda s: s.replace("|", "\\|").replace("\n", " ")
        line = lambda cells: "| " + " | ".join(esc(x) for x in cells + [""] * (ncol - len(cells))) + " |"
        heads, seps = n.get("head") or [], []
        for j in range(ncol):
            al = heads[j].get("align") if j < len(heads) else None
            seps.append({"left": ":---", "center": ":---:", "right": "---:"}.get(al, "---"))
        return "\n".join([line(head), "| " + " | ".join(seps) + " |"] + [line(r) for r in body])

    def r_code(self, n, cx):
        lang, text = n.get("lang") or "", (n.get("text") or "").strip("\n")
        path = self.code_file.get(id(n))
        lines = text.split("\n")
        if cx["mode"] == "txt":
            return "\n".join("    " + l for l in lines)
        here = dirn(cx.get("file") or "tdd.md")
        if path and not cx.get("whole") and len(lines) > CODE_INLINE_MAX:
            return f"*{lang or 'code'} block, {len(lines)} lines, ~{tok(text)} tokens: [{path}]({rel(path, here)})*"
        fence = "~~~~" if "```" in text else "```"
        s = f"{fence}{lang}\n{text}\n{fence}"
        if path:
            s += f"\n\n*Extracted to [{path}]({rel(path, here)}).*"
        return s

    def img_rel(self, pub, cx):
        target = self.img_file.get(pub) or pub
        return rel(target, dirn(cx.get("file") or "tdd.md"))

    def r_shot(self, n, cx):
        alt = re.sub(r"[\[\]]", "", n.get("alt") or "")
        cap = self.ih(n.get("caption"), cx)
        if cx["mode"] == "txt":
            return [f"[mock: {cap}] {alt}".strip()]
        out = [f"![{alt}]({self.img_rel(n.get('src') or '', cx)})"]
        if cap:
            out[0] += f"\n*{cap}*"
        return out

    def legend(self, items, cx):
        return "Legend: " + " · ".join(f"{self.ih(i.get('label'), cx)} = {i.get('tone')}" for i in items or [])

    def r_diagram(self, n, cx):
        cx["cur"] = n.get("id") or cx.get("cur")
        title = self.ih(n.get("title"), cx)
        alt = re.sub(r"[\[\]]", "", n.get("alt") or "")
        out = []
        if cx["mode"] == "md":
            out.append(f"**Figure · {title}**{self.sfx(n.get('id'), cx)}")
            out.append(f"![{alt}]({self.img_rel(n.get('src') or '', cx)})")
        else:
            out.append(f"[diagram: {title}]{self.sfx(n.get('id'), cx)}" + (f" {alt}" if alt and alt != plain(n.get("title")) else ""))
        tail = []
        if n.get("legend"):
            tail.append(self.legend(n["legend"], cx))
        if n.get("reading"):
            tail.append("Reading: " + self.ih(n["reading"], cx))
        side = self.sidecar_path(n)
        if side and cx["mode"] == "md" and side in self.files_planned:
            tail.append(f"Box text: [{side}]({rel(side, dirn(cx.get('file') or 'tdd.md'))})")
        if tail:
            out.append("\n".join(tail) if cx["mode"] == "md" else " ".join(tail))
        return out

    def r_prs(self, n, d, cx):
        out = []
        for g in n.get("groups") or []:
            out.append("#" * max(1, min(6, d)) + " " + plain(g.get("ms", "")) + (f" · {self.ih(g['note'], cx)}" if g.get("note") else "") + self.sfx(g.get("id"), cx))
            for p in g.get("prs") or []:
                cx["cur"] = p["id"]
                out.append("#" * max(1, min(6, d + 1)) + f" PR {p.get('label', p['n'])} · {self.ih(p.get('title'), cx)}{self.sfx(p['id'], cx)}")
                lines = [f"{self.bold('Change:', cx)} {self.ih(p.get('change'), cx)}"]
                if p.get("rollback"):
                    lines.append(f"{self.bold('Rollback:', cx)} {self.ih(p['rollback'], cx)}")
                pf = self.pr_file.get(p.get("label", p["n"]))
                if cx["mode"] == "md" and pf:
                    lines.append(f"{self.bold('Deep dive:', cx)} [{pf}]({rel(pf, dirn(cx.get('file') or 'tdd.md'))})")
                out.append("\n".join(lines) if cx["mode"] == "md" else " ".join(lines))
                if cx.get("whole"):
                    out += self.bh(p.get("bodyHtml"), cx, (d + 2) - 2)
        return out

    # ------------------------------------------------------------------ slicing
    def anchors_under(self, x):
        found = []

        def walk(v):
            if isinstance(v, dict):
                if isinstance(v.get("id"), str) and v["id"] in self.defs:
                    found.append(v["id"])
                for k, w in v.items():
                    if k not in ("hotspots", "legend", "head"):
                        walk(w)
            elif isinstance(v, list):
                for w in v:
                    walk(w)
        walk(x)
        return found

    def unit_anchors(self, u):
        k, n = u["kind"], u["node"]
        if k == "head":
            return [n["id"]] if n.get("id") in self.defs else []
        if k == "rows":
            return [r["id"] for r in (n.get("rows") or [])[u["lo"]:u["hi"]] if r.get("id") in self.defs]
        if k == "items":
            return []
        return list(dict.fromkeys(self.anchors_under(n)))

    def render_unit(self, u, cx):
        k, n, d = u["kind"], u["node"], u["d"]
        if k == "node":
            return self.r_node(n, d, cx)
        if k == "head":
            return self.r_head(n, d, cx)
        if k == "card":
            return self.r_card(n, d, cx)
        cx["cur"] = self.near.get(id(n), cx.get("cur"))
        if k == "rows":
            return [self.r_table(n, cx, u["lo"], u["hi"])]
        if k == "items":
            return [self.r_list(n, cx, u["lo"], u["hi"])]
        return []

    def unit_tokens(self, u, sid):
        cx = {"mode": "md", "file": "sections/x.md", "sizing": True, "section": sid, "used": set()}
        text = "\n\n".join(self.render_unit(u, cx))
        return tok(text) + 25 * len(cx["used"])

    def expand(self, u):
        n, d, crumbs = u["node"], u["d"], u["crumbs"]
        t = n.get("type") if u["kind"] in ("node",) else None
        mk = lambda kind, node, dd, extra=None, cr=None: dict({"kind": kind, "node": node, "d": dd, "crumbs": cr if cr is not None else crumbs}, **(extra or {}))
        if t in ("group", "fold", "decision", "persona", "today"):
            title = plain(n.get("title") or n.get("question") or n.get("label") or "")
            if t == "decision":
                title = f"{n.get('did')} · {title}"
            cr = crumbs + [(clip(title, 60), n.get("id"))]
            if t == "persona":
                ch = [mk("node", x, d + 1, cr=cr) for x in (n.get("text") or []) + (n.get("media") or [])]
                if n.get("today"):
                    ch.append(mk("node", n["today"], d + 1, cr=cr))
            elif t == "today":
                ch = [mk("node", x, d, cr=cr) for x in (n.get("media") or []) + (n.get("blocks") or [])]
            else:
                ch = [mk("node", x, d + 1, cr=cr) for x in n.get("blocks") or []]
            return [mk("head", n, d)] + ch
        if t in ("decisions", "steps", "col", "box"):
            return [mk("node", x, d) for x in n.get("blocks") or []]
        if t == "duo":
            return [mk("node", x, d) for x in n.get("items") or []]
        if t == "cols":
            return [mk("node", x, d) for col in n.get("cols") or [] for x in col]
        if t == "cards":
            return [mk("card", c, d) for c in n.get("cards") or []]
        if t in ("table", "list"):
            key = "rows" if t == "table" else "items"
            seq = n.get(key) or []
            if len(seq) < 2:
                return None
            limit = self.budget * PACK
            chunks, lo, size = [], 0, 0
            for i in range(len(seq)):
                one = self.unit_tokens(mk(key, n, d, {"lo": i, "hi": i + 1}), "x")
                if i > lo and size + one > limit:
                    chunks.append((lo, i))
                    lo, size = i, 0
                size += one
            chunks.append((lo, len(seq)))
            if len(chunks) < 2:
                return None
            return [mk(key, n, d, {"lo": a, "hi": b}) for a, b in chunks]
        return None

    def fit(self, units, sid, limit):
        out = []
        for u in units:
            u["tokens"] = self.unit_tokens(u, sid)
            if u["tokens"] > limit:
                sub = self.expand(u)
                if sub:
                    out += self.fit(sub, sid, limit)
                    continue
            out.append(u)
        return out

    def slice_sections(self):
        limit = self.budget * PACK
        for s in self.c.get("sections") or []:
            sid, num = s["id"], s["num"]
            units = self.fit([{"kind": "node", "node": b, "d": 3, "crumbs": []} for b in s.get("blocks") or []], sid, limit)
            parts, cur, size = [], [], 400
            for u in units:
                if cur and size + u["tokens"] > limit:
                    carry = []
                    while cur and (cur[-1]["kind"] == "head" or (cur[-1]["kind"] == "node" and cur[-1]["node"].get("type") == "heading")):
                        carry.insert(0, cur.pop())
                    if cur:
                        parts.append(cur)
                    cur, size = carry, 400 + sum(x["tokens"] for x in carry)
                cur.append(u)
                size += u["tokens"]
            if cur or not parts:
                parts.append(cur)
            total = len(parts)
            for k, pu in enumerate(parts, 1):
                path = f"sections/{num:02d}-{sid}.md" if k == 1 else f"sections/{num:02d}-{sid}-part-{k}.md"
                anchors = ([sid] if k == 1 else []) + [a for u in pu for a in self.unit_anchors(u)]
                anchors = list(dict.fromkeys(anchors))
                for a in anchors:
                    self.anchor_file.setdefault(a, path)
                self.slices.append({"section": s, "part": k, "parts": total, "units": pu, "path": path, "anchors": anchors})

    def write_slices(self):
        for sl in self.slices:
            s, k, total, path = sl["section"], sl["part"], sl["parts"], sl["path"]
            sid = s["id"]
            cx = {"mode": "md", "file": path, "record": True, "section": sid, "used": set(), "cur": sid}
            title = self.section_title(s) + (f" (part {k})" if k > 1 else "")
            chip = f" [{s['chip']}]" if s.get("chip") else ""
            blocks = []
            if k == 1:
                blocks.append(f"## {self.section_title(s)}{chip} {{#{sid}}}")
            else:
                first = self.slices[self.slices.index(sl) - 1]["path"]
                blocks.append(f"## {title}")
                blocks.append(f"*Continues [part {k - 1}]({rel(first, 'sections')}) of section {s['num']} "
                              f"([#{sid}]({rel(self.anchor_file.get(sid, path), 'sections')}#{sid})).*")
            crumbs = sl["units"][0]["crumbs"] if sl["units"] else []
            if k > 1 and crumbs:
                trail = " › ".join(f"[{t}]({rel(self.anchor_file.get(a, path), 'sections')}#{a})" if a else t for t, a in crumbs)
                blocks.append(f"*Inside: {trail}.*")
            for u in sl["units"]:
                blocks += self.render_unit(u, cx)
            if k < total:
                nxt = self.slices[self.slices.index(sl) + 1]["path"]
                blocks.append(f"*Continued in [part {k + 1}]({rel(nxt, 'sections')}).*")
            ext = [a for a in sorted(cx["used"]) if a not in sl["anchors"] and a in self.defs]
            if ext:
                lines = [f"**Defined elsewhere** ({len(ext)}; one line each, follow the link for the full text):"]
                for a in ext[:60]:
                    df = self.defs[a]
                    f = self.anchor_file.get(a)
                    target = f"{rel(f, 'sections')}#{a}" if f else f"#{a}"
                    extra = ""
                    if df.get("kind") == "decision":
                        did = next((x["id"] for x in self.c.get("decisions") or [] if x["anchor"] == a), None)
                        if did:
                            extra = f" · [record](../decisions/{self.dec_file_name(did)})"
                    lines.append(f"- [{a}]({target}) · {df.get('kind')} · {clip(df.get('title'), 110)}{extra}")
                blocks.append("\n".join(lines))
            body = "\n\n".join(b for b in blocks if b)
            fm = {"id": sid, "title": title, "section": s["num"], "part": k, "parts": total, "tokens": 0,
                  "anchors": sl["anchors"], "generated": self.head}
            text = front(fm) + "\n\n" + body + "\n"
            fm["tokens"] = tok(text) + 2
            text = front(fm) + "\n\n" + body + "\n"
            sl["tokens"] = fm["tokens"]
            self.emit(path, text)

    # ------------------------------------------------------------------ whole document
    def write_whole(self):
        c, m = self.c, self.meta
        for mode, path in (("md", "tdd.md"), ("txt", "tdd.txt")):
            cx = {"mode": mode, "file": path, "whole": True, "record": False, "cur": None}
            md = mode == "md"
            blocks = [f"> {self.head}", f"# {c.get('titleText') or plain(c.get('title'))}"]
            facts = []
            if m.get("eyebrow"):
                facts.append(plain(m["eyebrow"]))
            proj = m.get("project")
            if isinstance(proj, dict) and proj.get("label"):
                lab = proj["label"]
                if md:
                    facts.append("Project " + (lab if re.match(r"^\[.*\]\(.*\)$", lab) else f"[{lab}]({proj.get('href')})" if proj.get("href") else lab))
                else:
                    facts.append("Project " + re.sub(r"^\[(.*?)\]\(.*\)$", r"\1", lab))
            for p in m.get("people") or []:
                facts.append(f"{p.get('role')} {p.get('name')}")
            facts.append(f"Updated {m.get('updated') or 'not stated'}" + (f" (rev {m['rev']}: {m.get('revNote')})" if m.get("rev") else ""))
            facts.append(f"Status: {m.get('status')}")
            if m.get("repoUrl") or self.sha:
                facts.append(f"Code base {m.get('repoUrl') or m.get('repoPath') or ''} at {self.sha or 'no sha'}" + (f" ({m['shaDate']})" if m.get("shaDate") else ""))
            blocks.append(" · ".join(facts))
            if m.get("pathAliases"):
                blocks.append("Path aliases in citations: " + ", ".join(f"{k} = {v}" for k, v in m["pathAliases"].items()) +
                              " (citations below are written out in full).")
            for p in c.get("lede") or []:
                blocks += self.r_node(p, 3, cx)
            toc = []
            for s in c.get("sections") or []:
                toc.append(f"- [{self.section_title(s)}](#{s['id']})" if md else f"- {self.section_title(s)}")
            blocks.append(("## Contents\n\n" if md else "## Contents\n") + "\n".join(toc))
            for s in c.get("sections") or []:
                chip = f" [{s['chip']}]" if s.get("chip") else ""
                blocks.append(f"## {self.section_title(s)}{chip} {{#{s['id']}}}")
                blocks += self.r_nodes(s.get("blocks"), 3, cx)
            if self.tables:
                blocks.append("## Schema tables")
                blocks += self.schema_blocks(cx, ids=True)
            if m.get("footer"):
                blocks += ["---", plain(m["footer"])]
            self.emit(path, ("\n\n" if md else "\n").join(b for b in blocks if b))

    # ------------------------------------------------------------------ decisions
    @staticmethod
    def dec_file_name(did):
        m = re.match(r"([A-Za-z]+)-?(\d+(?:\.\d+)?)$", did or "")
        return (f"{m.group(1).upper()}-{m.group(2)}" if m else slugify(did)) + ".md"

    def write_decisions(self):
        index = []
        refs_by = {}
        for l in self.ref_links:
            refs_by.setdefault(l["to"], set()).add(l["section"])
        for dc in self.c.get("decisions") or []:
            did, aid = dc["id"], dc["anchor"]
            path = "decisions/" + self.dec_file_name(did)
            n = self.dec_nodes.get(aid)
            cx = {"mode": "md", "file": path, "ids": False, "record": False, "cur": aid, "section": dc.get("section")}
            date = re.search(r"\d{4}-\d{2}-\d{2}", dc.get("status") or "")
            sec_file = self.anchor_file.get(aid)
            referenced = sorted(x for x in refs_by.get(aid, set()) if x and x != dc.get("section"))
            fm = {"id": did, "anchor": aid, "status": dc.get("status") or "not stated", "state": dc.get("state"),
                  "date": date.group(0) if date else None, "question": dc.get("question"), "section": dc.get("section"),
                  "section_file": sec_file, "referenced_from": referenced, "generated": self.head}
            blocks = [f"# {did} · {dc.get('question')}",
                      f"**Status:** {dc.get('status') or 'not stated'} · state: {dc.get('state')}" +
                      (f" · on the page in [{sec_file}#{aid}]({rel(sec_file, 'decisions')}#{aid})" if sec_file else "")]
            if n:
                blocks += self.r_nodes(n.get("blocks"), 2, cx)
            self.emit(path, front(fm) + "\n\n" + "\n\n".join(blocks))
            index.append({"id": did, "anchor": aid, "question": dc.get("question"), "status": dc.get("status"), "state": dc.get("state"),
                          "date": fm["date"], "section": dc.get("section"), "section_file": sec_file, "file": path, "referenced_from": referenced})
        if index:
            self.emit("decisions/decisions.json", dump_lines({"_generated": self.head, "decisions": index}))
        self.decision_index = index

    # ------------------------------------------------------------------ citations, refs
    def record_pr_bodies(self):
        """Section slices link PR deep dives instead of pasting them; record their citations and references here."""
        for p in self.prs:
            pr = p["pr"]
            cx = {"mode": "txt", "file": self.pr_file[pr.get("label", pr["n"])], "record": True, "section": p["section"], "cur": pr["id"]}
            self.bh(pr.get("bodyHtml"), cx, 0)

    def write_citations(self):
        occ = []
        for i, o in enumerate(self.cite_occ, 1):
            ct = self.cite_by_text.get(o["text"]) or {}
            occ.append({"id": f"CIT.{i:04d}", "text": o["text"], "path": ct.get("path") or o["text"], "from": ct.get("from"), "to": ct.get("to"),
                        "sha": self.sha or None, "href": ct.get("href"), "section": o["section"], "anchor": o["anchor"], "file": o["file"]})
        self.citations = occ
        distinct_texts = {o["text"] for o in self.cite_occ}
        not_rendered = sorted({ct["text"] for ct in self.c.get("cites") or []} - distinct_texts)
        self.emit("citations.json", dump_lines({"_generated": self.head, "sha": self.sha or None, "sha_date": self.meta.get("shaDate"),
                                                "path_aliases": self.meta.get("pathAliases") or {},
                                                "counts": {"on_page": len(occ), "distinct_paths": len({o['path'] for o in occ}),
                                                           "compiler_cites": len(self.c.get("cites") or []), "compiler_only": not_rendered},
                                                "citations": occ}))
        by = {}
        for o in occ:
            by.setdefault(o["path"], []).append(o)
        lines = [self.md_head(), "", f"# Citations by file ({len(occ)} on the page, {len(by)} files, at sha {self.sha or 'none'})", "",
                 "Re-read a cited file at the sha before editing (`git show " + (self.sha or "<sha>") + ":<path>`); if it drifted, report it."]
        for path in sorted(by):
            lines += ["", f"## {path}"]
            for o in by[path]:
                rng = (f"L{o['from']}" + (f"-{o['to']}" if o.get("to") else "")) if o.get("from") else "whole file"
                frag = f"#{o['anchor']}" if (o.get("anchor") and self.anchor_file.get(o["anchor"]) == o.get("file")) else ""
                where = f"[{o['file']}{frag}]({o['file']}{frag})" if o.get("file") else ""
                lines.append(f"- {rng} · section {o['section']} · {where}" + (f" · [source]({o['href']})" if o.get("href") else ""))
        self.emit("citations.md", "\n".join(lines))

    def write_refs(self):
        anchors = {}
        for a in self.c.get("anchors") or []:
            df = self.defs.get(a["id"], {})
            anchors[a["id"]] = {"kind": a.get("kind"), "title": a.get("title"), "text": df.get("text", ""),
                                "section": self.anchor_section.get(a["id"]), "file": self.anchor_file.get(a["id"])}
        tokens = {}
        for l in self.ref_links:
            t = tokens.setdefault(l["token"], {"anchor": l["to"], "file": self.anchor_file.get(l["to"]), "uses": 0})
            t["uses"] += 1
        self.token_index = tokens
        refs = self.c.get("refs") or {}
        self.emit("refs.json", dump_lines({"_generated": self.head, "linked": refs.get("linked"), "unlinked": refs.get("unlinked") or [],
                                           "families": self.meta.get("refs") or {}, "glossary": self.c.get("glossary") or {},
                                           "anchors": [dict({"id": k}, **v) for k, v in anchors.items()],
                                           "tokens": [dict({"token": k}, **v) for k, v in sorted(tokens.items())],
                                           "links": self.ref_links}))

    # ------------------------------------------------------------------ sql / code
    def write_code(self):
        totals = {}
        for b in self.codes:
            ext = b["file"].rsplit(".", 1)[-1]
            totals[b["file"].split("/")[0]] = totals.get(b["file"].split("/")[0], 0) + 1
        idx = {}
        for b in self.codes:
            folder = b["file"].split("/")[0]
            idx[folder] = idx.get(folder, 0) + 1
            ext = b["file"].rsplit(".", 1)[-1]
            s = next((x for x in self.c.get("sections") or [] if x["id"] == b["section"]), None)
            where = f"section {self.section_title(s)} (#{b['section']})" if s else f"section {b['section']}"
            info = f"{where} · anchor #{b['anchor']} · slice {self.anchor_file.get(b['anchor']) or self.anchor_file.get(b['section']) or '-'}"
            if b.get("source") != "page":
                info += f" · from {b['source']}"
            info += f" · block {idx[folder]} of {totals[folder]} · lang {b['lang'] or 'none'}"
            self.emit(b["file"], comment(ext, self.head) + "\n" + comment(ext, info) + "\n\n" + b["text"].strip("\n"))

    # ------------------------------------------------------------------ schema (D7)
    def schema_tables(self):
        tables = self.c.get("schema") or []
        if tables:
            return tables, "content.json"
        sdir = os.path.join(self.packet, "schema") if self.packet else None
        if sdir and os.path.isdir(sdir):
            raw = []
            for f in sorted(os.listdir(sdir)):
                if f.endswith(".json"):
                    try:
                        t = json.load(open(os.path.join(sdir, f), encoding="utf-8"))
                    except (OSError, ValueError) as e:
                        self.warn.append(f"schema/{f}: unreadable ({e})")
                        continue
                    t.setdefault("table", os.path.splitext(f)[0])
                    raw.append(t)
            if raw:
                self.warn.append(f"content.json has no schema tables but the packet has {len(raw)} schema/*.json: rebuild with build_tdd.py; copied the raw files")
            return raw, "packet schema/ (raw, content.json predates it)"
        return [], None

    def schema_blocks(self, cx, ids):
        out = []
        for t in self.tables:
            name, tid = t.get("table"), t.get("id")
            out.append(f"### {name} ({t.get('state', 'existing')})" + (f" {{#{tid}}}" if (ids and tid) else ""))
            if t.get("summary"):
                out.append(t["summary"])
            cols = t.get("columns") or []
            if cols:
                rows = ["| column | type | change | note | was |", "| --- | --- | --- | --- | --- |"]
                for col in cols:
                    rows.append("| " + " | ".join(str(col.get(k) or "").replace("|", "\\|").replace("\n", " ") for k in ("name", "type", "change", "note", "was")) + " |")
                out.append("\n".join(rows))
            for label, key, fields in (("Measured", "measured", ("value", "unit", "source", "date", "note")), ("Expected", "expected", ("value", "unit", "note"))):
                vals = t.get(key) or {}
                if vals:
                    rows = [f"| {label.lower()} | " + " | ".join(fields) + " |", "| --- | " + " | ".join("---" for _ in fields) + " |"]
                    for metric, v in vals.items():
                        v = v if isinstance(v, dict) else {"value": v}
                        rows.append(f"| {metric} | " + " | ".join(str(v.get(f, "") if v.get(f) is not None else "").replace("|", "\\|") for f in fields) + " |")
                    out.append(f"**{label}**\n\n" + "\n".join(rows))
                else:
                    out.append(f"**{label}:** not measured" if key == "measured" else f"**{label}:** not stated")
            if t.get("sample_rows"):
                out.append(f"Sample rows: {len(t['sample_rows'])} in schema/{slugify(name)}.json" + (f" (source: {t['sample_source']})" if t.get("sample_source") else ""))
            if t.get("ddl"):
                f = self.ddl_file.get(name)
                out.append(f"DDL: [{f}]({rel(f, dirn(cx.get('file') or 'tdd.md'))})" if f else "DDL: in the JSON")
            for note in t.get("notes") or []:
                out.append(f"- {note}")
        return out

    def write_schema(self):
        self.tables, origin = self.schema_tables()
        self.ddl_file = {}
        if not self.tables:
            return
        n_sql = sum(1 for b in self.codes if b["file"].startswith("sql/"))
        for t in self.tables:
            name = t.get("table")
            if t.get("ddl"):
                n_sql += 1
                f = f"sql/{n_sql:02d}-schema-{slugify(name)}.sql"
                self.ddl_file[name] = f
                self.emit(f, comment("sql", self.head) + "\n" + comment("sql", f"DDL of table {name} ({t.get('state')}) from schema/{slugify(name)}.json") + "\n\n" + t["ddl"].strip("\n"))
            self.emit(f"schema/{slugify(name)}.json", json.dumps(dict({"_generated": self.head + f" · from {origin}"}, **t), indent=1, ensure_ascii=False))
        cx = {"mode": "md", "file": "schema/schema.md"}
        self.emit("schema/schema.md", "\n\n".join([self.md_head(), f"# Schema tables ({len(self.tables)})",
                                                   "Change flags: new | changed | dropped | unchanged. A missing measurement reads \"not measured\"; nothing here is estimated by this tool. "
                                                   "Table anchors ({#tbl-...}) are defined here, not in sections/: on the page they open in the schema drawer."]
                                                  + self.schema_blocks(cx, ids=True)))

    # ------------------------------------------------------------------ PRs
    def write_prs(self):
        if not self.prs:
            return
        rows, idx = [], []
        pdir = os.path.join(self.packet, "prs") if self.packet else None
        for p in self.prs:
            pr, n = p["pr"], p["pr"].get("label", p["pr"]["n"])
            path = self.pr_file[n]
            src = os.path.join(pdir, os.path.basename(pr.get("file") or f"pr-{int(n):02d}.md")) if pdir else None
            if src and os.path.isfile(src):
                raw = open(src, encoding="utf-8").read()
                raw = re.sub(r"\]\(#([\w\-]+)\)", lambda m: f"]({rel(self.anchor_file[m.group(1)], 'prs')}#{m.group(1)})" if m.group(1) in self.anchor_file else m.group(0), raw)
                note = f"# generated copy of {pr.get('file') or f'prs/pr-{int(n):02d}.md'} (page links point into sections/) · {self.head}"
                if raw.startswith("---\n"):
                    text = "---\n" + note + "\n" + raw[4:]
                else:
                    text = f"> {note[2:]}\n\n" + raw
                origin = "packet copy"
            else:
                cx = {"mode": "md", "file": path, "ids": False}
                text = "\n\n".join([self.md_head(), f"# PR {n}: {plain(pr.get('title'))}"] + self.bh(pr.get("bodyHtml"), cx, 0))
                origin = "rendered from content.json"
            self.emit(path, text)
            row = {"n": n, "anchor": pr["id"], "milestone": p["ms"], "title": plain(pr.get("title")), "change": plain(pr.get("change")),
                   "rollback": plain(pr.get("rollback")) or "not stated", "file": path, "slice": self.anchor_file.get(pr["id"]), "origin": origin}
            idx.append(row)
            esc = lambda s: (s or "").replace("|", "\\|")
            rows.append(f"| [{n}]({rel(path, 'prs')}) | {esc(row['milestone'])} | {esc(row['title'])} | {esc(row['change'])} | {esc(row['rollback'])} |")
        self.pr_index = idx
        self.emit("prs/prs.md", "\n".join([self.md_head(), "", f"# PRs ({len(idx)}, in merge order)", "",
                                           "| PR | milestone | title | change | rollback |", "| --- | --- | --- | --- | --- |"] + rows))
        self.emit("prs/prs.json", dump_lines({"_generated": self.head, "prs": idx}))

    # ------------------------------------------------------------------ images
    def sidecar_path(self, n):
        pub = n.get("src") or ""
        return "images/" + os.path.splitext(pub)[0] + ".md" if pub else None

    def hotspot_boxes(self, key, src_abs):
        cands = []
        if self.packet:
            cands.append(os.path.join(self.packet, "diagrams", key + ".hotspots.json"))
        if src_abs:
            cands.append(os.path.join(os.path.dirname(src_abs), key + ".hotspots.json"))
        path = next((p for p in cands if os.path.isfile(p)), None)
        if not path:
            return None, []
        try:
            spec = json.load(open(path, encoding="utf-8"))
        except (OSError, ValueError):
            return path, []
        vb = spec.get("viewBox")
        out = []
        for b in spec.get("boxes") or []:
            box = None
            if vb and len(vb) == 4 and vb[2] and vb[3] and all(k in b for k in ("x", "y", "w", "h")):
                box = [round((b["x"] - vb[0]) / vb[2] * 100, 1), round((b["y"] - vb[1]) / vb[3] * 100, 1), round(b["w"] / vb[2] * 100, 1), round(b["h"] / vb[3] * 100, 1)]
            out.append({"label": b.get("label", ""), "kind": b.get("kind", ""), "tone": b.get("tone", ""), "text": b.get("text", ""), "box": box,
                        "href": b.get("href") or "", "table": b.get("table") or "", "note": b.get("note") or ""})
        return path, out

    def plan_images(self):
        self.files_planned = set()
        self.image_rows = []
        files = self.manifest.get("files") or {}
        for i, img in enumerate(self.images, 1):
            n, pub = img["node"], img["published"]
            src_abs = os.path.join(self.build, pub) if os.path.isfile(os.path.join(self.build, pub)) else files.get(pub)
            if not src_abs or not os.path.isfile(src_abs):
                self.warn.append(f"image {pub}: not found in the build")
                src_abs = None
            row = {"id": f"IMG.{i:02d}", "path": self.img_file.get(pub), "source": pub, "kind": img["kind"], "in_today_block": img["in_today"],
                   "title": plain(n.get("title")) if img["kind"] == "diagram" else plain(n.get("caption")),
                   "caption": plain(n.get("reading")) if img["kind"] == "diagram" else plain(n.get("caption")),
                   "alt": n.get("alt") or "", "section": img["section"], "anchor": img["anchor"],
                   "width": n.get("width") or None, "height": n.get("height") or None,
                   "format": os.path.splitext(pub)[1].lstrip(".").lower(),
                   "bytes": os.path.getsize(src_abs) if src_abs else None,
                   "sha256": sha256(open(src_abs, "rb").read()) if src_abs else None}
            if img["kind"] == "diagram":
                row["legend"] = [f"{plain(x.get('label'))} = {x.get('tone')}" for x in n.get("legend") or []]
                row["hotspots"] = [{"label": h.get("label", ""), "href": h.get("href", ""), "table": h.get("table", "")} for h in n.get("hotspots") or []]
                hs_path, boxes = self.hotspot_boxes(n.get("key") or os.path.splitext(os.path.basename(pub))[0], src_abs)
                row["boxes"] = len(boxes)
                row["labels"] = list(dict.fromkeys(clip(b["label"], 60) for b in boxes if b["label"]))
                if boxes:
                    side = self.sidecar_path(n)
                    row["sidecar"] = side
                    self.files_planned.add(side)
                    img["boxes"], img["hotspots_file"] = boxes, hs_path
            img["row"], img["src_abs"] = row, src_abs
            self.image_rows.append(row)

    def write_images(self):
        copied = set()
        for img in self.images:
            row, n = img["row"], img["node"]
            if self.copy_images and img["src_abs"] and row["path"] not in copied:
                self.files[row["path"]] = ("copy", img["src_abs"])
                copied.add(row["path"])
            if img.get("boxes"):
                side = row["sidecar"]
                here = dirn(side)
                slice_file = self.anchor_file.get(row["anchor"])
                lines = [self.md_head(), "", f"# Diagram: {row['title']}", "",
                         f"- File: [{os.path.basename(row['path'] or row['source'])}]({rel(row['path'], here) if row['path'] else row['source']}) "
                         f"({row['width']}×{row['height']} {row['format']}, {row['bytes']} bytes)",
                         f"- On the page: section {row['section']}, anchor #{row['anchor']}" + (f" in [{slice_file}]({rel(slice_file, here)}#{row['anchor']})" if slice_file else ""),
                         f"- Alt: {row['alt']}"]
                if row.get("caption"):
                    lines.append(f"- Reading: {row['caption']}")
                if row.get("legend"):
                    lines.append(f"- Legend: {' · '.join(row['legend'])}")
                lines.append(f"- Box source: {os.path.basename(img['hotspots_file'])}; box = x, y, w, h in percent of the image")
                lines += ["", f"## Boxes ({len(img['boxes'])})", "", "| label | kind | tone | text | box | link |", "| --- | --- | --- | --- | --- | --- |"]
                for b in img["boxes"]:
                    link = b["href"] or (f"table {b['table']}" if b["table"] else "") or b["note"]
                    cells = [b["label"], b["kind"], b["tone"], " / ".join(x for x in b["text"].split("\n") if x.strip()), ", ".join(str(v) for v in b["box"] or []), link]
                    lines.append("| " + " | ".join(str(x).replace("|", "\\|") for x in cells) + " |")
                self.emit(side, "\n".join(lines))
        if self.image_rows:
            self.emit("images/index.json", dump_lines({"_generated": self.head, "copied": self.copy_images,
                                                       "note": "paths are relative to agents/; images are byte copies (never re-encoded) unless copied is false",
                                                       "images": self.image_rows}))

    # ------------------------------------------------------------------ page strip (D8)
    def write_page(self):
        if not self.page_sha:
            return
        root = sp.parse(open(self.page, encoding="utf-8", errors="replace").read())
        content, title, removed = sp.strip(root)
        head = f"{self.head} · stripped by {sp.VERSION}"
        body = "".join(sp.to_html(ch) for ch in content.children).strip() if content.tag in ("body", "html", "#root") else sp.to_html(content).strip()
        body = re.sub(r"\n{3,}", "\n\n", body)
        self.emit("page/plain.html", f"<!doctype html>\n<!-- {head} -->\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>{title}</title></head>\n<body>\n{body}\n</body></html>")
        self.emit("page/outline.json", json.dumps({"_generated": head, "title": title, "removed": removed, "headings": sp.outline(content)}, indent=1, ensure_ascii=False))

    # ------------------------------------------------------------------ map
    def write_map(self):
        ent, edges = [], []
        E = lambda **kw: ent.append({k: v for k, v in kw.items() if v is not None})
        L = lambda a, r, b, **kw: edges.append(dict({"from": a, "rel": r, "to": b}, **{k: v for k, v in kw.items() if v is not None}))
        sec_title = {s["id"]: self.section_title(s) for s in self.c.get("sections") or []}
        for s in self.c.get("sections") or []:
            E(id=f"SEC.{s['id']}", kind="section", path=self.anchor_file.get(s["id"]), anchor=s["id"], title=sec_title[s["id"]])
        sec_ids = {s["id"] for s in self.c.get("sections") or []}
        def A(aid):
            return f"SEC.{aid}" if aid in sec_ids else f"ANC.{aid}"
        for a in self.c.get("anchors") or []:
            if a.get("kind") == "section":
                continue
            aid, f = a["id"], self.anchor_file.get(a["id"])
            E(id=f"ANC.{aid}", kind="anchor", subkind=a.get("kind"), path=f, anchor=aid, title=clip(a.get("title"), 120))
            if self.anchor_section.get(aid):
                L(f"SEC.{self.anchor_section[aid]}", "contains", f"ANC.{aid}")
            if f:
                L(f"ANC.{aid}", "in_file", f"FILE.{f}")
        for d in getattr(self, "decision_index", []):
            E(id=f"DEC.{d['id']}", kind="decision", path=d["file"], anchor=d["anchor"], state=d["state"], title=clip(d["question"], 120))
            L(f"DEC.{d['id']}", "anchored_at", A(d['anchor']))
            if d.get("section"):
                L(f"DEC.{d['id']}", "in_section", f"SEC.{d['section']}")
        for p in getattr(self, "pr_index", []):
            E(id=f"PR.{p.get('label', p['n'])}", kind="pr", path=p["file"], anchor=p["anchor"], milestone=p["milestone"], title=clip(p["title"], 120))
            L(f"PR.{p.get('label', p['n'])}", "anchored_at", A(p['anchor']))
            L(f"PR.{p['n']}", "documented_by", f"FILE.{p['file']}")
            sec = self.anchor_section.get(p["anchor"])
            if sec:
                L(f"PR.{p['n']}", "in_section", f"SEC.{sec}")
        for t in self.tables:
            tid = f"TBL.{t.get('table')}"
            E(id=tid, kind="table", path=f"schema/{slugify(t.get('table'))}.json", anchor=t.get("id"), state=t.get("state"))
            if t.get("id"):
                L(tid, "anchored_at", A(t['id']))
            L(tid, "documented_by", "FILE.schema/schema.md")
        tbl_by_id = {t.get("id"): f"TBL.{t.get('table')}" for t in self.tables}
        for img in self.images:
            row = img["row"]
            iid = f"IMG.{row['source']}"
            E(id=iid, kind="image", path=row["path"], anchor=row["anchor"], image_kind=row["kind"], title=clip(row["title"], 120))
            L(iid, "in_section", f"SEC.{row['section']}")
            if row["anchor"]:
                L(iid, "anchored_at" if row["kind"] == "diagram" else "appears_under", f"ANC.{row['anchor']}" if row["anchor"] != row["section"] else f"SEC.{row['section']}")
            for h in row.get("hotspots") or []:
                if h.get("table"):
                    L(iid, "hotspot", tbl_by_id.get(h["table"], A(h['table'])), note=h.get("label"))
                elif (h.get("href") or "").startswith("#"):
                    L(iid, "hotspot", A(h['href'][1:]), note=h.get("label"))
        repo = set()
        for o in self.citations:
            E(id=o["id"], kind="citation", path=o["file"], anchor=o["anchor"], repo_path=o["path"], lines=[o["from"], o["to"]] if o.get("from") else None)
            L(o["id"], "cites", f"REPO.{o['path']}", sha=self.sha or None)
            if o.get("anchor"):
                L(f"ANC.{o['anchor']}" if o["anchor"] != o["section"] else f"SEC.{o['section']}", "has_cite", o["id"])
            repo.add(o["path"])
        for pth in sorted(repo):
            E(id=f"REPO.{pth}", kind="repo_path", path=pth)
        for b in self.codes:
            kind = "sql" if b["file"].startswith("sql/") else "code"
            cid = f"{kind.upper()}.{os.path.basename(b['file'])}"
            E(id=cid, kind=kind, path=b["file"], anchor=b["anchor"], lang=b["lang"] or None)
            L(cid, "extracted_from", f"ANC.{b['anchor']}" if b["anchor"] != b["section"] else f"SEC.{b['section']}")
            L(cid, "in_section", f"SEC.{b['section']}")
        for t in self.tables:
            f = self.ddl_file.get(t.get("table"))
            if f:
                cid = f"SQL.{os.path.basename(f)}"
                E(id=cid, kind="sql", path=f, anchor=t.get("id"), lang="sql")
                L(cid, "extracted_from", f"TBL.{t.get('table')}")
        for tkn, v in sorted(self.token_index.items()):
            E(id=f"TOK.{tkn}", kind="token", anchor=v["anchor"], path=v["file"], uses=v["uses"])
            L(f"TOK.{tkn}", "defined_by", f"ANC.{v['anchor']}" if self.defs.get(v["anchor"], {}).get("kind") != "section" else f"SEC.{v['anchor']}")
        seen = {}
        for l in self.ref_links:
            src = l.get("from") or l.get("section")
            if not src:
                continue
            key = (src, l["to"])
            seen[key] = seen.get(key, 0) + 1
        for (a, b), cnt in seen.items():
            fa = f"SEC.{a}" if self.defs.get(a, {}).get("kind") == "section" else f"ANC.{a}"
            fb = f"SEC.{b}" if self.defs.get(b, {}).get("kind") == "section" else f"ANC.{b}"
            L(fa, "references", fb, count=cnt)
        for f in sorted(self.files):
            if not isinstance(self.files[f], tuple):
                E(id=f"FILE.{f}", kind="file", path=f, tokens=tok(self.files[f]))
        # ids that edges point at must resolve
        ids = {e["id"] for e in ent}
        dangling = sorted({x for e in edges for x in (e["from"], e["to"]) if x not in ids})
        if dangling:
            self.warn.append(f"map.json: {len(dangling)} edge end(s) without an entity, e.g. {dangling[:3]}")
        self.map_counts = {"entities": len(ent), "edges": len(edges), "dangling": len(dangling)}
        self.emit("map.json", dump_lines({"_generated": self.head, "schema_version": 1,
                                          "packet": {"slug": os.path.splitext(self.c.get("source") or "")[0], "rev": self.meta.get("rev"), "base_sha": self.sha or None},
                                          "kinds": ["section", "anchor", "decision", "pr", "table", "image", "citation", "repo_path", "sql", "code", "token", "file"],
                                          "rels": sorted({e["rel"] for e in edges}),
                                          "entities": ent, "edges": edges}))

    # ------------------------------------------------------------------ README, llms.txt
    def summary_text(self):
        for p in self.c.get("lede") or []:
            t = plain(p.get("html"))
            if t:
                return clip(t, 420)
        for s in self.c.get("sections") or []:
            for b in s.get("blocks") or []:
                if b.get("type") == "p":
                    return clip(plain(b.get("html")), 420)
        return ""

    def template(self, name, default):
        path = os.path.join(TEMPLATES, name)
        return open(path, encoding="utf-8").read() if os.path.isfile(path) else default

    def fill(self, tpl, values):
        out = re.sub(r"\{\{(\w+)\}\}", lambda m: str(values.get(m.group(1), "")), tpl)
        return re.sub(r"\n{3,}", "\n\n", out).strip() + "\n"

    def write_index(self):
        c, m = self.c, self.meta
        sec_lines, llm_secs = [], []
        for sl in self.slices:
            s = sl["section"]
            name = self.section_title(s) + (f" (part {sl['part']})" if sl["part"] > 1 else "")
            sec_lines.append(f"- [{name}]({sl['path']}) ~{sl['tokens']} tok · {len(sl['anchors'])} anchors")
            llm_secs.append(f"- [{name}]({sl['path']}): ~{sl['tokens']} tokens; anchors {', '.join(sl['anchors'][:6])}{' …' if len(sl['anchors']) > 6 else ''}")
        dec_lines = [f"- [{d['id']}]({d['file']}) {d['state']} · {clip(d['question'], 70)}" for d in getattr(self, "decision_index", [])]
        llm_dec = [f"- [{d['id']} · {clip(d['question'], 90)}]({d['file']}): {d['state']}; {clip(d['status'], 60)}" for d in getattr(self, "decision_index", [])]
        llm_prs = [f"- [PR {p['n']} · {clip(p['title'], 70)}]({p['file']}): {p['milestone']}; {clip(p['change'], 90)}" for p in getattr(self, "pr_index", [])]
        n_sql = sum(1 for f in self.files if f.startswith("sql/"))
        n_code = sum(1 for f in self.files if f.startswith("code/"))
        counts = (f"{len(c.get('sections') or [])} sections in {len(self.slices)} slices · {len(getattr(self, 'decision_index', []))} decisions · "
                  f"{len(getattr(self, 'pr_index', []))} PRs · {len(self.citations)} citations · {len(c.get('anchors') or [])} anchors · "
                  f"{len(self.ref_links)} reference links ({len((c.get('refs') or {}).get('unlinked') or [])} unlinked tokens) · "
                  f"{n_sql} sql · {n_code} code · {len(self.tables)} schema tables · {len(self.image_rows)} images")
        files = [("tdd.md", "whole document, markdown; ids as {#id}"), ("tdd.txt", "whole document, one paragraph per line"),
                 ("decisions/decisions.json", "decision index: id, state, status, date, slice"),
                 ("citations.md", "citations grouped by repo file"), ("citations.json", "every path:line with section, anchor, slice, href"),
                 ("refs.json", "anchor -> slice and definition; token -> anchor; every reference link"),
                 ("prs/prs.md", "PR order: milestone, title, change, rollback"), ("schema/schema.md", "tables: columns, change flags, measured/expected"),
                 ("images/index.json", "what each picture holds: title, caption, alt, size, boxes"), ("page/plain.html", "the built page, chrome stripped"),
                 ("map.json", "entity/edge graph for programs (jq), not for reading whole"), ("tokens.json", "bytes, sha256, ~tokens per file")]
        file_rows = "\n".join(f"| [{f}]({f}) | {what} | ~{tok(self.files[f]) if isinstance(self.files.get(f), str) else '-'} |" for f, what in files if f in self.files)
        allw = list(self.manifest.get("warnings") or [])
        notes = allw[:6] + ([f"and {len(allw) - 6} more in content.files.json"] if len(allw) > 6 else [])
        unl = (c.get("refs") or {}).get("unlinked") or []
        aliases = ", ".join(f"`{k}` = `{v}`" for k, v in (m.get("pathAliases") or {}).items())
        values = {
            "header": self.md_head(), "head_raw": self.head, "title": c.get("titleText") or plain(c.get("title")), "summary": self.summary_text(),
            "status": m.get("status") or "not stated", "rev": m.get("rev") or "-", "rev_note": m.get("revNote") or "", "updated": m.get("updated") or "not stated",
            "repo": m.get("repoUrl") or m.get("repoPath") or "no repo named", "sha": self.sha or "none", "sha_date": m.get("shaDate") or "date not stated",
            "source": c.get("source") or "", "generated": c.get("generated") or "", "budget": self.budget,
            "sections": "\n".join(sec_lines) or "- none", "decisions": "\n".join(dec_lines) or "- none recorded",
            "files": file_rows, "counts": counts, "aliases": aliases or "none",
            "notes": "\n".join(f"- build warning: {clip(w, 160)}" for w in notes) or "- none",
            "unlinked": ", ".join(unl) or "none",
            "llm_sections": "\n".join(llm_secs) or "- none", "llm_decisions": "\n".join(llm_dec) or "- none recorded",
            "llm_prs": "\n".join(llm_prs) or "- none", "n_sql": n_sql, "n_code": n_code,
            "tdd_md_tokens": tok(self.files.get("tdd.md", "")), "tdd_txt_tokens": tok(self.files.get("tdd.txt", "")),
        }
        readme = self.fill(self.template("README.template.md", DEFAULT_README), values)
        if len(readme.encode("utf-8")) > README_MAX:
            values["sections"] = "\n".join(f"- [{sl['path'].split('/')[-1]}]({sl['path']})" for sl in self.slices)
            values["decisions"] = "- see [decisions/decisions.json](decisions/decisions.json)"
            readme = self.fill(self.template("README.template.md", DEFAULT_README), values)
            self.warn.append("README.md over 8 KB with the full index; wrote the compact index")
        self.emit("README.md", readme)
        self.emit("llms.txt", self.fill(self.template("llms.template.txt", DEFAULT_LLMS), values))

    # ------------------------------------------------------------------ tokens.json
    def write_tokens(self):
        rows, over = {}, []
        for f in sorted(self.files):
            v = self.files[f]
            if isinstance(v, tuple):
                b = open(v[1], "rb").read()
                rows[f] = {"bytes": len(b), "sha256": sha256(b), "tokens": None, "kind": "binary copy"}
                continue
            b = v.encode("utf-8")
            t = tok(v)
            rows[f] = {"bytes": len(b), "sha256": sha256(b), "tokens": t}
            if f not in EXEMPT and t > self.budget:
                over.append(f)
        texts = {f: r for f, r in rows.items() if r["tokens"] is not None}
        slices = {f: r for f, r in texts.items() if f not in EXEMPT}
        largest = max(slices, key=lambda f: slices[f]["tokens"]) if slices else None
        self.over_budget, self.largest = over, (largest, slices[largest]["tokens"] if largest else 0)
        doc = {"_generated": self.head, "method": "tokens = ceil(chars / 4): an estimate, not a tokenizer; binary files have no token count",
               "budget": self.budget, "budget_exempt": sorted(EXEMPT), "over_budget": over,
               "largest_slice": {"path": largest, "tokens": self.largest[1]} if largest else None,
               "total_text_tokens": sum(r["tokens"] for r in texts.values()), "files": rows}
        self.files["tokens.json"] = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"

    # ------------------------------------------------------------------ run
    def run(self):
        self.annotate()
        self.plan_images()
        self.slice_sections()
        self.write_slices()
        self.write_schema()
        self.write_whole()
        self.record_pr_bodies()
        self.write_decisions()
        self.write_citations()
        self.write_refs()
        self.write_code()
        self.write_prs()
        self.write_images()
        self.write_page()
        self.write_map()
        self.write_index()
        self.write_tokens()
        return self


# ============================================================================ fallback: only tdd.html

class PageLane:
    """Reduced lane from a built page alone (no content.json): tdd.md/txt, sections, sql/code, images, refs, page/."""

    def __init__(self, build, out=None, budget=BUDGET):
        self.build = os.path.abspath(build)
        self.out = os.path.abspath(out or os.path.join(self.build, "agents"))
        self.budget = budget
        self.page = os.path.join(self.build, "tdd.html")
        raw = open(self.page, "rb").read()
        self.page_sha = sha256(raw)[:12]
        self.root = sp.parse(raw.decode("utf-8", "replace"))
        self.head = f"agents lane · from tdd.html (no content.json) · content page:{self.page_sha} · {GENERATOR} · generated, do not edit"
        self.files, self.warn = {}, []
        self.citations, self.slices, self.image_rows, self.tables = [], [], [], []

    def emit(self, path, text):
        self.files[path] = text if text.endswith("\n") else text + "\n"

    def run(self):
        decisions = []
        for e in self.root.elements():
            if "dec" in e.classes and e.get("id"):
                did = next((sp.norm(x.text()) for x in e.elements() if "id" in x.classes), e.get("id").upper())
                q = next((sp.norm(x.text()) for x in e.elements() if x.tag in sp.HEADINGS), "")
                st = next((sp.norm(x.text()) for x in e.elements() if set(x.classes) & {"chip", "pill"}), "")
                state = next((c for c in e.classes if c in ("done", "open", "held")), None)
                body = [ch for ch in e.children if not (isinstance(ch, sp.Node) and "head" in ch.classes)]
                decisions.append({"id": did, "anchor": e.get("id"), "question": q, "status": st, "state": state, "body": body})
        imgs = []
        for e in self.root.elements():
            if e.tag == "img" and e.get("src") and not e.get("src", "").startswith("data:"):
                fig = e.parent
                while fig is not None and fig.tag != "figure" and fig.tag != "#root":
                    fig = fig.parent
                cap = ""
                if fig is not None and fig.tag == "figure":
                    fc = next((x for x in fig.elements() if x.tag == "figcaption"), None)
                    cap = sp.norm(fc.text()) if fc is not None else ""
                imgs.append({"src": e.get("src"), "alt": e.get("alt") or "", "caption": cap, "kind": "diagram" if fig is not None and "diagram" in fig.classes else "image",
                             "width": e.get("width"), "height": e.get("height"), "anchor": fig.get("id") if fig is not None and fig.tag == "figure" else None})
        content, title, removed = sp.strip(self.root)
        codes = []
        pre_hook = lambda lang, text: codes.append({"lang": lang or self.guess(text), "text": text, "guessed": not lang})
        md_blocks = sp.render(content, "md", pre=pre_hook)
        sections = [e for e in content.elements() if e.tag == "section" and e.get("id")]
        sections = [s for s in sections if not any(a.tag == "section" for a in self.ancestors(s))]
        if not sections:
            self.warn.append("no <section id> elements: the page went into one slice")
            sections = [content]
        for i, s in enumerate(sections, 1):
            sid = s.get("id") or "page"
            h = next((x for x in s.elements() if x.tag in sp.HEADINGS), None)
            name = sp.norm(h.text()) if h is not None else sid
            blocks = []
            for ch in s.children:
                if isinstance(ch, str):
                    if ch.strip():
                        blocks.append(sp.norm(ch))
                    continue
                img_hook = lambda src, alt: f" ![{alt}]({rel(os.path.join(self.build, src), os.path.join(self.out, 'sections'))}) "
                blocks += sp.render(self.wrap(ch), "md", img=img_hook)
            if blocks and s is not content:
                blocks[0] = blocks[0] + (f" {{#{sid}}}" if "{#" not in blocks[0].split("\n")[0] else "")
            parts, cur, size = [], [], 300
            for b in blocks:
                if cur and size + tok(b) > self.budget * PACK:
                    parts.append(cur)
                    cur, size = [], 300
                cur.append(b)
                size += tok(b)
            parts.append(cur)
            for k, pb in enumerate(parts, 1):
                path = f"sections/{i:02d}-{slugify(sid)}.md" if k == 1 else f"sections/{i:02d}-{slugify(sid)}-part-{k}.md"
                body = "\n\n".join(pb)
                anchors = re.findall(r"\{#([^}\s]+)\}", body)
                fm = {"id": sid, "title": name + (f" (part {k})" if k > 1 else ""), "part": k, "parts": len(parts), "tokens": tok(body) + 60,
                      "anchors": anchors, "generated": self.head}
                self.emit(path, front(fm) + "\n\n" + body)
                self.slices.append({"path": path, "title": fm["title"], "tokens": fm["tokens"], "anchors": anchors})
        n_sql = n_code = 0
        for b in codes:
            ext = code_ext(b["lang"])
            if ext == "sql":
                n_sql += 1
                f = f"sql/{n_sql:02d}-page.sql"
            else:
                n_code += 1
                f = f"code/{n_code:02d}-page.{ext}"
            self.emit(f, comment(ext, self.head) + "\n" + comment(ext, f"block from tdd.html · lang {b['lang']}" + (" (guessed)" if b["guessed"] else "")) + "\n\n" + b["text"])
        self.emit("tdd.md", f"> {self.head}\n\n" + "\n\n".join(md_blocks))
        self.emit("tdd.txt", f"> {self.head}\n" + "\n".join(sp.render(content, "txt")))
        for i, im in enumerate(imgs, 1):
            self.image_rows.append(dict({"id": f"IMG.{i:02d}", "path": rel(os.path.join(self.build, im["src"]), self.out)}, **{k: v for k, v in im.items() if k != "src"}, source=im["src"]))
        if self.image_rows:
            self.emit("images/index.json", dump_lines({"_generated": self.head, "copied": False, "images": self.image_rows}))
        for d in decisions:
            fm = {"id": d["id"], "anchor": d["anchor"], "status": d["status"] or "not stated", "state": d["state"], "question": d["question"], "generated": self.head}
            w = sp.Node("div")
            w.children = d["body"]
            self.emit("decisions/" + Lane.dec_file_name(d["id"]), front(fm) + "\n\n" + f"# {d['id']} · {d['question']}\n\n**Status:** {d['status'] or 'not stated'} · state: {d['state']}\n\n" + "\n\n".join(sp.render(w, "md")))
        anchors = []
        for e in content.elements():
            if e.get("id"):
                f = next((s["path"] for s in self.slices if e.get("id") in s["anchors"]), None)
                anchors.append({"id": e.get("id"), "tag": e.tag, "text": clip(sp.norm(e.text()), 120), "file": f})
        self.emit("refs.json", dump_lines({"_generated": self.head, "anchors": anchors}))
        body = "".join(sp.to_html(ch) for ch in content.children).strip()
        self.emit("page/plain.html", f"<!doctype html>\n<!-- {self.head} -->\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>{title}</title></head>\n<body>\n{body}\n</body></html>")
        self.emit("page/outline.json", json.dumps({"_generated": self.head, "title": title, "removed": removed, "headings": sp.outline(content)}, indent=1, ensure_ascii=False))
        secs = "\n".join(f"- [{s['title']}]({s['path']}) ~{s['tokens']} tok" for s in self.slices)
        self.emit("README.md", f"> {self.head}\n\n# {title} · agents lane (from the page)\n\nBuilt from tdd.html because content.json was missing: "
                               "no decisions index beyond the page's decision cards, no citation index, no map. Rebuild with build_tdd.py for the full lane.\n\n"
                               "Eval scores, when the eval has run, are in `../eval/output.*.json`. If you change the packet, rebuild, rerun the eval "
                               "(`eval_packet.py`) and leave your own scores in your return; never pass `--push`.\n\n"
                               f"## Sections\n{secs}\n\n## Files\n- [tdd.md](tdd.md), [tdd.txt](tdd.txt): the whole page\n- [page/plain.html](page/plain.html), [page/outline.json](page/outline.json)\n"
                               f"- sql/ ({n_sql}), code/ ({n_code}), decisions/ ({len(decisions)}), [images/index.json](images/index.json) ({len(self.image_rows)})\n- [refs.json](refs.json): every id and its slice\n")
        self.emit("llms.txt", f"# {title}\n\n> Agents lane built from the page alone. {self.head}\n\n## Sections\n" +
                  "\n".join(f"- [{s['title']}]({s['path']}): ~{s['tokens']} tokens" for s in self.slices) + "\n\n## Optional\n- [tdd.md](tdd.md): the whole page\n")
        Lane.write_tokens(self)
        self.counts = {"sql": n_sql, "code": n_code, "decisions": len(decisions)}
        return self

    @staticmethod
    def ancestors(e):
        p = e.parent
        while p is not None:
            yield p
            p = p.parent

    @staticmethod
    def wrap(el):
        w = sp.Node("div")
        w.children = [el]
        return w

    @staticmethod
    def guess(text):
        head = text.lstrip()[:200]
        if re.match(r"(?i)(create|alter|select|insert|update|delete|with|drop|begin)\b", head):
            return "sql"
        if re.match(r"(//|export |import |interface |type |const |function )", head):
            return "ts"
        return ""


# ============================================================================ write / check

def _lane_files(out):
    tj = os.path.join(out, "tokens.json")
    if not os.path.isfile(tj):
        return None
    try:
        return json.load(open(tj, encoding="utf-8")).get("files") or {}
    except (OSError, ValueError):
        return None


def _looks_like_lane(out):
    if not os.path.isdir(out) or not os.listdir(out):
        return True
    readme = os.path.join(out, "README.md")
    if os.path.isfile(readme):
        first = open(readme, encoding="utf-8", errors="replace").readline()
        if "agents lane" in first:
            return True
    return _lane_files(out) is not None


def write(build_dir, packet=None, out=None, budget=BUDGET, copy_images=True, force=False):
    build = os.path.abspath(build_dir)
    if os.path.isfile(os.path.join(build, "content.json")):
        lane = Lane(build, packet, out, budget, copy_images).run()
        mode = "content.json"
    elif os.path.isfile(os.path.join(build, "tdd.html")):
        lane = PageLane(build, out, budget).run()
        mode = "tdd.html fallback"
    else:
        raise SystemExit(f"{build}: no content.json and no tdd.html")
    out = lane.out
    if os.path.exists(out) and not _looks_like_lane(out) and not force:
        raise SystemExit(f"{out}: exists and is not an agents lane (no README header, no tokens.json); pass --force to overwrite")
    old = _lane_files(out) or {}
    for f in old:   # remove what the previous run generated; never touch unlisted files
        p = os.path.join(out, f)
        if f not in lane.files and os.path.isfile(p):
            os.remove(p)
    for f, v in lane.files.items():
        p = os.path.join(out, f)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        if isinstance(v, tuple):
            shutil.copyfile(v[1], p)
        else:
            open(p, "w", encoding="utf-8").write(v)
    for dp, dns, fns in sorted(os.walk(out), key=lambda x: -len(x[0])):
        if dp != out and not os.listdir(dp):
            os.rmdir(dp)
    return {"lane": lane, "mode": mode, "out": out}


def check(build_dir, out=None):
    """Problems that make the agents/ lane stale or untrustworthy; empty when it is current. For lint_packet.py."""
    build = os.path.abspath(build_dir)
    out = os.path.abspath(out or os.path.join(build, "agents"))
    cpath, page = os.path.join(build, "content.json"), os.path.join(build, "tdd.html")
    page_sha = sha256(open(page, "rb").read())[:12] if os.path.isfile(page) else None
    if os.path.isfile(cpath):
        try:
            gen = json.load(open(cpath, encoding="utf-8")).get("generated")
        except (OSError, ValueError) as e:
            return [f"{cpath}: unreadable ({e})"]
        want = [f"content {gen} ", f"page {page_sha or 'none'} "]
    elif page_sha:
        want = [f"content page:{page_sha} "]
    else:
        return [f"{build}: no content.json or tdd.html; build the page first"]
    readme = os.path.join(out, "README.md")
    if not os.path.isfile(readme):
        return [f"{readme}: missing; run write_agents.py {build}"]
    first = open(readme, encoding="utf-8", errors="replace").readline().strip()
    problems = []
    for w in want:
        if w not in first:
            found = re.search(re.escape(w.split(" ")[0]) + r" (\S+)", first)
            problems.append(f"agents/ is stale: README header has '{w.split(' ')[0]} {found.group(1) if found else '?'}', the build has '{w.strip()}'; rerun write_agents.py {build}")
    if GENERATOR not in first:
        problems.append(f"agents/ was written by another generator version (want {GENERATOR}); rerun write_agents.py")
    listed = _lane_files(out)
    if listed is None:
        problems.append(f"{out}/tokens.json: missing or unreadable")
        return problems
    for f, rec in listed.items():
        p = os.path.join(out, f)
        if not os.path.isfile(p):
            problems.append(f"agents/{f}: listed in tokens.json but missing")
        elif sha256(open(p, "rb").read()) != rec.get("sha256"):
            problems.append(f"agents/{f}: changed since it was generated (hand edit?)")
    for dp, _, fns in os.walk(out):
        for fn in fns:
            r = rel(os.path.join(dp, fn), out)
            if r != "tokens.json" and r not in listed:
                problems.append(f"agents/{r}: not generated by write_agents.py")
    return problems


# ============================================================================ default templates (templates/agents/ overrides)

DEFAULT_README = """{{header}}

# {{title}} · agents lane

> {{summary}}

Status **{{status}}** · rev {{rev}} ({{rev_note}}) · updated {{updated}} · code base {{repo}} at `{{sha}}` ({{sha_date}}). Built from `{{source}}` at {{generated}}. Every file here is generated: edit the TDD source and rebuild, never these files.

## Read this first
1. This file. 2. Only the slice your task needs (table below). 3. Follow `{#id}` links when a slice points elsewhere.
- Citations are `path:line` at sha `{{sha}}`: re-read the file at that sha (`git show {{sha}}:<path>`) before editing; if it drifted, report it, do not guess.
- A decision whose state is `open` needs its owner. Do not build past it; stop and ask.
- Slices are under ~{{budget}} tokens (estimate: chars/4). `tokens.json` has the count for every file.

## Scores
The eval lane scores this packet into `build/eval/` (`../eval/` from this folder) when it has run: `output.artifact.json`, `output.packet.json`, `output.implementation.json`, `output.qa.json`, `output.bugs.json`, `output.tokens.json`, plus `probe.json` and `history.jsonl` (one line per run). Read the ones your task touches before you start. If you change the packet, rebuild, rerun `write_agents.py`, then run `~/.local/share/tdd-skill/venv/bin/python -I <tdd skill>/scripts/eval_packet.py <packet>` (`<tdd skill>`: the folder the tdd skill is installed in) and leave your own scores: the summary line and every scorer that moved, in your return. A reviewer returns the score diff. Never pass `--push`.

## Which file for which task
| Task | Read |
| --- | --- |
| Build a PR | [prs/prs.md](prs/prs.md), then `prs/pr-NN.md`, its `decisions/`, the `sql/` and `code/` it names |
| Iterate on a section | `sections/NN-<id>.md` (front matter lists its anchors; footer defines what it references) |
| Semantic search, embeddings | [tdd.txt](tdd.txt) (one paragraph per line) or `sections/*.md` |
| Find a fact | `grep -rn "<term>" sections/`; anchors are written `{#id}` |
| SQL review | `sql/*.sql` ({{n_sql}}), [schema/schema.md](schema/schema.md) when present |
| Image review | [images/index.json](images/index.json), then `images/diagrams/<key>.md` (every box's text) |
| Check a citation | [citations.md](citations.md) (by file), [citations.json](citations.json) |
| Follow D3, E1, PR 7 | [refs.json](refs.json): token -> anchor -> slice |
| Program over the design | [map.json](map.json) (entities and edges) |

## Sections
{{sections}}

## Decisions
{{decisions}}

## Files
| File | What | ~tokens |
| --- | --- | --- |
{{files}}

## Counts
{{counts}}

Path aliases used on the page (written out in full here): {{aliases}}. Tokens with no definition on the page: {{unlinked}}.

## Build notes
{{notes}}
"""

DEFAULT_LLMS = """# {{title}}

> {{summary}} Status {{status}}, rev {{rev}}, code base at {{sha}}. Generated: {{head_raw}}

The agents lane of a technical design document. Read README.md first, then only the slice your task needs. Citations are path:line at sha {{sha}}. Eval scores, when the eval has run, are in ../eval/output.*.json; an agent that changes the packet reruns the eval and reports its own scores (README.md, Scores).

## Sections
{{llm_sections}}

## Decisions
{{llm_decisions}}

## PRs
{{llm_prs}}

## Reference
- [README.md](README.md): which file for which task
- [prs/prs.md](prs/prs.md): PR order with change and rollback
- [citations.md](citations.md): every path:line by file
- [refs.json](refs.json): reference token -> anchor -> slice
- [images/index.json](images/index.json): what each diagram and mock shows
- [map.json](map.json): entities and edges

## Optional
- [tdd.md](tdd.md): the whole document, ~{{tdd_md_tokens}} tokens
- [tdd.txt](tdd.txt): the whole document as plain text, ~{{tdd_txt_tokens}} tokens
- [page/plain.html](page/plain.html): the built page with scripts, styles and overlays removed
"""


def main(argv):
    a = {"packet": None, "out": None, "budget": BUDGET, "check": False, "link": False, "force": False, "quiet": False}
    pos, i = [], 0
    while i < len(argv):
        x = argv[i]
        if x in ("--packet", "--out", "--budget-tokens"):
            if i + 1 >= len(argv):
                sys.exit(f"{x} needs a value")
            key = {"--packet": "packet", "--out": "out", "--budget-tokens": "budget"}[x]
            a[key] = int(argv[i + 1]) if key == "budget" else argv[i + 1]
            i += 2
        elif x in ("--check", "--link-images", "--force", "--quiet"):
            a[{"--check": "check", "--link-images": "link", "--force": "force", "--quiet": "quiet"}[x]] = True
            i += 1
        elif x.startswith("--"):
            sys.exit(f"unknown option {x}\n{__doc__}")
        else:
            pos.append(x)
            i += 1
    if len(pos) != 1:
        sys.exit(__doc__)
    if not os.path.isdir(pos[0]):
        sys.exit(f"{pos[0]}: no such directory")
    if a["check"]:
        problems = check(pos[0], a["out"])
        for p in problems:
            print("stale:", p)
        if problems:
            sys.exit(1)
        print(f"agents lane current: {os.path.abspath(a['out'] or os.path.join(pos[0], 'agents'))}")
        return
    r = write(pos[0], a["packet"], a["out"], a["budget"], not a["link"], a["force"])
    lane = r["lane"]
    for w in lane.warn:
        print("warning:", w, file=sys.stderr)
    texts = [f for f, v in lane.files.items() if not isinstance(v, tuple)]
    if isinstance(lane, Lane):
        largest = lane.largest
        print(f"wrote {r['out']} ({r['mode']}): {len(lane.files)} files ({len(lane.files) - len(texts)} image copies), "
              f"{len(lane.slices)} section slices for {len(lane.c.get('sections') or [])} sections, {len(lane.decision_index)} decisions, "
              f"{len(getattr(lane, 'pr_index', []))} PRs, {len(lane.citations)} citations, {sum(1 for f in texts if f.startswith('sql/'))} sql, "
              f"{sum(1 for f in texts if f.startswith('code/'))} code, {len(lane.tables)} schema tables, {len(lane.image_rows)} images, "
              f"map {lane.map_counts['entities']} entities/{lane.map_counts['edges']} edges; largest slice {largest[0]} ~{largest[1]} tokens; "
              f"{len(lane.over_budget)} over the {lane.budget}-token budget; README {len(lane.files['README.md'].encode())} bytes")
    else:
        print(f"wrote {r['out']} ({r['mode']}): {len(lane.files)} files, {len(lane.slices)} section slices, {lane.counts['sql']} sql, "
              f"{lane.counts['code']} code, {lane.counts['decisions']} decisions, {len(lane.image_rows)} images; {len(lane.over_budget)} over budget")


if __name__ == "__main__":
    main(sys.argv[1:])
