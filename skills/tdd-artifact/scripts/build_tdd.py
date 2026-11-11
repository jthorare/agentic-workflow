#!/usr/bin/env python3
"""Compile a technical design document from a markdown source in the house TDD template into content.json: the
structured document the React renderer (../app) turns into the claude.ai artifact page and write_agents.py turns into
the agents/ lane.

Usage:
  build_tdd.py <source.md> <out-dir> [--meta <meta.json>] [--diagrams <dir>] [--prs <dir>] [--schema <dir>] [--no-copy] [--strict]

Reuses ~/.claude/skills/system-design/scripts/build_doc.py (the markdown subset, inline renderer, assets) and
build_prs.py (the prs/pr-NN.md loader) as libraries; nothing in system-design is changed. Set SYSTEM_DESIGN_SKILL to
point at another copy of that skill.

Source conventions (templates/tdd-template.md explains each with an example):
  # Title                                        the page title (meta.json carries the header fields)
  ## Section title {chip: text; #id; lead: no}   numbered 1..n; the first paragraph is the lead unless lead: no
  ### Subsection {#id}                           a navigable subsection
  ::: kind Title · one-liner {attrs} ... :::     containers: cards, persona, duo, today, fold, group, decisions,
                                                 decision, options, steps, cols, col, glossary, callout (nest with ::::)
  [diagram: key · Title {fit; #id}]              a figure; the next [legend: label = tone | ...] and [reading: ...] lines
                                                 belong to it; diagrams/<key>.hotspots.json adds hover hotspots
  ![alt](mockups/x.png "Caption")                a mock shot (inside a persona box) or a still
  `LEG/File.ts:12-40`                            a path:line citation; linked to repo_url at sha when meta.json has them
  D7, E3, F3.2, R2, PR 7, OS-01, API             reference tokens, linked to their definition when the page defines one
  Trailing {small}, {muted}, {lead}, {#id} on a paragraph set its class or id.  [chip:tone text] is an inline chip.

Output: <out-dir>/content.json (meta, nav, sections as a block tree with inline HTML strings, defs for hover cards,
decisions, citations, glossary, schema tables, reference tokens linked and unlinked) and <out-dir>/content.files.json
(the publish manifest: published path -> source for every image and diagram, plus missing and warnings). Referenced
files are copied into <out-dir> at their published paths unless --no-copy. Exit 0; 1 on bad input, or any warning with
--strict."""
import datetime, html, importlib.util, json, os, re, sys
sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.normpath(os.path.join(HERE, ".."))

def sd_root():
    """The system-design skill: $SYSTEM_DESIGN_SKILL, else the sibling skills/system-design, else ~/.claude/skills."""
    env = os.environ.get("SYSTEM_DESIGN_SKILL")
    sibling = os.path.normpath(os.path.join(SKILL, "..", "system-design"))
    vendor = os.path.join(SKILL, "vendor", "system-design")
    for cand in (env, sibling, os.path.expanduser("~/.claude/skills/system-design"), vendor):
        if cand and os.path.isfile(os.path.join(cand, "scripts", "build_doc.py")):
            return cand
    return env or vendor

def _load_sd(name):
    path = os.path.join(sd_root(), "scripts", name)
    if not os.path.isfile(path):
        sys.exit(f"{path}: the system-design skill is needed (set SYSTEM_DESIGN_SKILL)")
    spec = importlib.util.spec_from_file_location(name[:-3], path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod

bd = _load_sd("build_doc.py")
bp = _load_sd("build_prs.py")
bd.MARK = re.compile(r"^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$", re.I)
esc, slug, strip_tags = bd.esc, bd.slug, bd.strip_tags

# ----------------------------------------------------------------------------- source parsing

BOX = re.compile(r"^(:{3,})\s*([a-z][\w-]*)?\s*(.*?)\s*$")
ATTRS = re.compile(r"\s*\{([^{}]*)\}\s*$")

def parse_attrs(text):
    """'Title {chip: new; #fe-ranked; wide}' -> ('Title', {'chip': 'new', 'id': 'fe-ranked', 'wide': True})."""
    attrs = {}
    m = ATTRS.search(text)
    if not m:
        return text, attrs
    for part in m.group(1).split(";"):
        part = part.strip()
        if not part:
            continue
        if part.startswith("#"):
            attrs["id"] = part[1:].strip()
        elif ":" in part:
            k, v = part.split(":", 1)
            attrs[k.strip().lower()] = v.strip()
        else:
            attrs[part.lower()] = True
    return text[:m.start()].rstrip(), attrs

def parse_ext(lines):
    """build_doc's block parser plus ::: containers (nesting by fence length)."""
    out, run, i, n = [], [], 0, len(lines)
    def flush():
        if run:
            out.extend(bd.parse_blocks(run))
            run.clear()
    while i < n:
        m = BOX.match(lines[i])
        if m and m.group(2):
            flush()
            fence, kind, rest = m.group(1), m.group(2).lower(), m.group(3)
            title, attrs = parse_attrs(rest)
            depth, body = 1, []
            i += 1
            while i < n:
                mm = BOX.match(lines[i])
                if mm and len(mm.group(1)) == len(fence):
                    if mm.group(2):
                        depth += 1
                    else:
                        depth -= 1
                        if depth == 0:
                            break
                body.append(lines[i])
                i += 1
            out.append({"t": "box", "kind": kind, "title": title, "attrs": attrs, "blocks": parse_ext(body)})
            i += 1
            continue
        run.append(lines[i])
        i += 1
    flush()
    return out

def split_title(text):
    parts = re.split(r"\s+·\s+", text, maxsplit=1)
    return (parts[0].strip(), parts[1].strip()) if len(parts) == 2 else (text.strip(), "")

# ----------------------------------------------------------------------------- reference tokens

TOKEN = re.compile(r"(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)")

def token_id(tok):
    t = tok.strip().lower().replace(" ", "-")
    t = re.sub(r"^pr(\d)", r"pr-\1", t)
    return t.replace(".", "-")

SKIP_TAGS = {"a", "code", "pre", "h1", "h2", "h3", "h4", "h5", "h6", "summary", "script", "template", "style", "button", "dt", "title"}
TAG = re.compile(r"<(/?)([a-zA-Z][\w-]*)([^>]*)>")

ABBR_TERM = re.compile(r"^[A-Z][A-Z0-9][A-Z0-9.-]*$")

def link_refs(html_body, ids, families, glossary, stats, seen=None):
    """Link reference tokens and glossary abbreviations in an inline-HTML string, outside links, code and headings."""
    if not html_body or ("<" not in html_body and not TOKEN.search(html_body) and not glossary):
        pass
    out, pos, stack = [], 0, []
    gl = sorted(glossary, key=len, reverse=True)
    gl_re = re.compile(r"(?<![\w-])(" + "|".join(re.escape(g) for g in gl) + r")(?![\w-])") if gl else None
    def tok(m):
        raw = m.group(1)
        tid = token_id(raw)
        fam = re.match(r"[A-Z]+", raw).group(0)
        links = stats.setdefault("links", [])
        if tid in ids:
            stats["linked"] += 1
            links.append({"token": raw, "family": fam, "section": stats.get("section"), "anchor": tid, "linked": True})
            return f'<a class="ref" href="#{tid}" data-ref="{tid}">{raw}</a>'
        if fam in families:
            stats["linked"] += 1
            links.append({"token": raw, "family": fam, "section": stats.get("section"), "anchor": families[fam].lstrip("#"), "linked": True})
            return f'<a class="ref ref-family" href="{families[fam]}" data-ref="{families[fam].lstrip("#")}">{raw}</a>'
        stats["unlinked"].append(raw)
        links.append({"token": raw, "family": fam, "section": stats.get("section"), "anchor": None, "linked": False})
        return f'<span class="ref-missing" data-ref="{tid}">{raw}</span>'
    def ab(m):
        g = m.group(1)
        if seen is not None and not ABBR_TERM.match(g):
            if g in seen:
                return g
            seen.add(g)
        stats["linked"] += 1
        stats.setdefault("links", []).append({"token": g, "family": "glossary", "section": stats.get("section"), "anchor": glossary[g], "linked": True})
        return f'<a class="ref ref-abbr" href="#{glossary[g]}" data-ref="{glossary[g]}">{g}</a>'
    def repl_text(text):
        text = TOKEN.sub(tok, text)
        if gl_re:
            parts = re.split(r"(<[^>]+>)", text)
            depth = 0
            for k, p in enumerate(parts):
                if p.startswith("<"):
                    depth += 1 if p.startswith("<a") else -1 if p.startswith("</a") else 0
                elif depth == 0 and p:
                    parts[k] = gl_re.sub(ab, p)
            text = "".join(parts)
        return text
    for m in TAG.finditer(html_body):
        text = html_body[pos:m.start()]
        out.append(repl_text(text) if text and not stack else text)
        out.append(m.group(0))
        pos = m.end()
        closing, name, attrs = m.group(1) == "/", m.group(2).lower(), m.group(3)
        if attrs.rstrip().endswith("/") or name in ("img", "br", "hr", "input", "source"):
            continue
        if stack:
            # inside a skipped element: track every tag so nesting ends exactly at its own close
            if closing:
                if name in stack:
                    while stack and stack.pop() != name:
                        pass
            else:
                stack.append(name)
            continue
        skip = name in SKIP_TAGS or (name == "span" and bool(re.search(r'class="(id|ref-missing|fig-title)\b', attrs)))
        if skip and not closing:
            stack.append(name)
    tail = html_body[pos:]
    out.append(repl_text(tail) if tail and not stack else tail)
    return "".join(out)

# ----------------------------------------------------------------------------- the compiler

SQL_KW = r"\b(CREATE TABLE|CREATE( UNIQUE)? INDEX|ALTER TABLE|PRIMARY KEY|FOREIGN KEY|REFERENCES|NOT NULL|NULL|CHECK|CONSTRAINT|UNIQUE|DEFAULT|ON DELETE|ON CONFLICT|CASCADE|SET NULL|NO ACTION|GENERATED ALWAYS AS|STORED|DO UPDATE SET|WHERE|SELECT|FROM|INSERT INTO|VALUES|UPDATE|DELETE|AND|OR|IN|IS|DISTINCT FROM|TRIGGER|BEFORE|AFTER|FOR EACH ROW|EXECUTE|FUNCTION|RETURNS|BEGIN|END|IF|THEN|RAISE|RETURN|ADD COLUMN|DROP|TEXT|UUID|INTEGER|SMALLINT|NUMERIC|BOOLEAN|JSONB|DATE|TIMESTAMPTZ)\b"
CITE = re.compile(r"^((?:[A-Z]{2,5}/)?[\w@./-]+\.(?:ts|tsx|js|mjs|cjs|py|sql|json|md|cs|yaml|yml|toml|rb|go|rs|sh|css|html|txt|graphql|proto))(?::(\d+)(?:-(\d+))?)?$")
LEGEND_TONES = {"new": "green", "changed": "green", "green": "green", "app": "grey", "grey": "grey", "gray": "grey", "existing": "grey",
                "engine": "blue", "blue": "blue", "toe": "blue", "data": "amber", "amber": "amber", "alert": "red", "red": "red", "hurts": "red",
                "violet": "violet", "purple": "violet", "dashed": "dashed", "proposed": "orange dashed", "orange": "orange dashed", "orange-dashed": "orange dashed"}

PR_FILE = re.compile(r"pr-(\d+)([a-z]?)\.md")

def load_prs(prs_dir):
    """prs/pr-NN.md and sub-PRs prs/pr-NNa.md (a split of PR NN), sorted NN, NNa, NNb, NN+1. Front matter as build_prs.py
    (milestone, milestone_note, title, change, rollback) plus depends_on: a list or a comma-separated string of PR labels."""
    prs = []
    for f in sorted(os.listdir(prs_dir)):
        m = PR_FILE.fullmatch(f)
        if not m:
            continue
        raw = open(os.path.join(prs_dir, f), encoding="utf-8").read()
        meta, body = bp.front(raw)
        n, sub = int(m.group(1)), m.group(2)
        label = f"{n}{sub}"
        h1 = re.search(r"^# (.+)$", body, re.M)
        title = meta.get("title") or (re.sub(r"^PR\s*\d+[a-z]?\s*[:.\-·]?\s*", "", h1.group(1)) if h1 else f"PR {label}")
        sm = re.search(r"^## Summary\s*\n(.*?)(?=^## |\Z)", body, re.S | re.M)
        summ = " ".join(sm.group(1).split()) if sm else ""
        rb = re.search(r"Rollback:\s*(.+?)(?:\.\s|\.?$)", summ)
        ms = meta.get("milestone") or (f"Milestone {t.group(1)}" if (t := re.search(r"milestone-(\w+)", raw)) else "PRs")
        deps = meta.get("depends_on") or ""
        deps = [d.strip() for d in re.split(r"[,;]\s*", deps) if d.strip()] if isinstance(deps, str) else [str(d).strip() for d in deps]
        deps = [d if d.upper().startswith("PR") else f"PR {d}" for d in deps]
        prs.append(dict(n=n, sub=sub, label=label, title=title, ms=ms, ms_note=meta.get("milestone_note", ""), body=re.sub(r"\A# .*\n", "", body.lstrip()),
                        change=meta.get("change") or bp.first_sentence(summ), rollback=meta.get("rollback") or (rb.group(1) if rb else "not stated"),
                        depends_on=deps, file=f"prs/{f}"))
    return sorted(prs, key=lambda q: (q["n"], q["sub"]))


# -- HTML guard. Author-supplied inline HTML goes through Raw (dangerouslySetInnerHTML) in the page, so every tag is
# rebuilt attribute by attribute: event handlers are dropped wherever they sit (including right after a closing quote,
# where HTML still parses them), URL attributes are allowed by scheme (http, https, mailto, a fragment or a relative
# path) after control characters, whitespace and entities are stripped, and every value is re-quoted with " escaped.
_SCRIPT_TAG = re.compile(r"<\s*/?\s*(script|iframe|object|embed|base|meta|link|style|template)\b[^>]*>", re.I)
_TAG = re.compile(r"<([a-zA-Z][\w:-]*)((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>")
_ATTR = re.compile(r"([^\s=>/\"']+)(?:\s*=\s*(\"[^\"]*\"|'[^']*'|[^\s>]+))?")
_URL_ATTRS = {"href", "src", "action", "formaction", "xlink:href", "poster", "data", "srcset", "cite", "longdesc", "background"}
_CTRL = re.compile(r"[\x00-\x20\x7f]+")
_OK_SCHEME = re.compile(r"^(https?|mailto):", re.I)

def _safe_url(v):
    raw = html.unescape(v)
    flat = _CTRL.sub("", raw)
    if ":" in flat.split("/", 1)[0].split("#", 1)[0].split("?", 1)[0]:
        return raw if _OK_SCHEME.match(flat) else "#blocked:"
    return raw

def _rebuild_tag(m):
    name, attrs = m.group(1), m.group(2) or ""
    out = [name]
    close = attrs.rstrip().endswith("/")
    for a in _ATTR.finditer(attrs):
        key, val = a.group(1), a.group(2)
        k = key.lower()
        if k == "/" or k.startswith("on") or k in ("style", "srcdoc"):
            continue
        if val is None:
            out.append(key)
            continue
        if val[:1] in "\"'" and val[-1:] == val[:1]:
            val = val[1:-1]
        if k in _URL_ATTRS:
            val = _safe_url(val)
        out.append(f'{key}="{val.replace("&", "&amp;").replace(chr(34), "&quot;") if "&" not in val or ";" not in val else val.replace(chr(34), "&quot;")}"')
    return "<" + " ".join(out) + (" /" if close else "") + ">"

def sanitize_html(h):
    if not h or "<" not in h:
        return h
    prev = None
    while h != prev:  # until stable: stripping an inner tag can reassemble an outer one (<ifr<script>ame ...>)
        prev, h = h, _SCRIPT_TAG.sub("", h)
    return _TAG.sub(_rebuild_tag, h)


class Compiler:
    def __init__(self, a):
        self.a = a
        self.warn = []
        self.md_dir = os.path.dirname(os.path.abspath(a.src))
        self.out_dir = os.path.abspath(a.out)
        self.inline0 = bd.Inline()
        self.assets = bd.Assets(self.out_dir, False, not a.no_copy)
        meta_path = a.meta or (os.path.join(self.md_dir, "meta.json") if os.path.isfile(os.path.join(self.md_dir, "meta.json")) else None)
        self.meta = bd.load_json(meta_path, "--meta") if meta_path else {}
        proj = self.meta.get("project")
        if isinstance(proj, dict) and isinstance(proj.get("label"), str):
            m = re.match(r"\s*\[([^\]]+)\]\(([^)]+)\)\s*$", proj["label"])
            if m:
                proj["label"], proj["href"] = m.group(1), proj.get("href") or m.group(2)
        self.diagram_dir = a.diagrams or next((d for d in (os.path.join(self.md_dir, "diagrams"),) if os.path.isdir(d)), None)
        self.prs_dir = a.prs or (os.path.join(self.md_dir, "prs") if os.path.isdir(os.path.join(self.md_dir, "prs")) else None)
        self.schema_dir = a.schema or (os.path.join(self.md_dir, "schema") if os.path.isdir(os.path.join(self.md_dir, "schema")) else None)
        self.ids, self.defs, self.anchors, self.cites, self.decisions, self.glossary = set(), {}, [], [], [], {}
        self.fig_no = self.pr_count = 0
        self.pending_hrefs = []
        self.cur_anchor = None
        self._measuring = False
        self.aliases = self.meta.get("path_aliases", {})
        self.cur_section = None
        self.schema = self.load_schema()

    # -- ids and definitions
    def uid(self, base):
        base = base or "x"
        i, cand = 2, base
        while cand in self.ids:
            cand = f"{base}-{i}"
            i += 1
        self.ids.add(cand)
        return cand

    def define(self, did, kind, title, text=""):
        t = strip_tags(self.inline(re.sub(r"\[chip:\w[\w-]*\s+([^\]]+)\]", r"\1", title or ""), links=False)).strip()
        x = strip_tags(self.inline(text, links=False) if "<" not in (text or "") else text).strip()[:600]
        sec = self.cur_section["id"] if self.cur_section else None
        self.defs[did] = {"kind": kind, "title": t, "text": x, "section": sec}
        self.anchors.append({"id": did, "kind": kind, "title": t, "section": sec})
        self.cur_anchor = did

    def text_of(self, s):
        self._measuring = True
        try:
            return strip_tags(self.inline(s, links=False))
        finally:
            self._measuring = False

    # -- inline
    def sanitize(self, h):
        """The source author is trusted, but a stray <script>, on* handler or javascript: URL must never reach a viewer."""
        return sanitize_html(h)

    def inline(self, s, links=True):
        h = self.sanitize(self.inline0(s, links=links))
        h = re.sub(r"\[chip:(\w[\w-]*)\s+([^\]]+)\]", lambda m: f'<span class="chip{" " + esc(m.group(1).lower()) if m.group(1).lower() != "plain" else ""}">{m.group(2)}</span>', h)
        return re.sub(r"<code>([^<]+)</code>", self.cite_code, h)

    def cite_code(self, m):
        text = html.unescape(m.group(1))
        c = CITE.match(text)
        if not c:
            return m.group(0)
        path, a, b = c.group(1), c.group(2), c.group(3)
        full = path
        for alias, real in self.aliases.items():
            if full.startswith(alias + "/"):
                full = real.rstrip("/") + full[len(alias):]
                break
        cite = {"text": text, "path": full, "from": int(a) if a else None, "to": int(b) if b else None,
                "section": self.cur_section["id"] if self.cur_section else None, "anchor": self.cur_anchor}
        if not getattr(self, "_measuring", False):
            self.cites.append(cite)
        code = f'<code class="cite">{m.group(1)}</code>'
        url, sha = self.meta.get("repo_url"), self.meta.get("sha")
        if url and sha and a:
            frag = f"#L{a}" + (f"-L{b}" if b else "")
            href = f'{url.rstrip("/")}/blob/{sha}/{full}{frag}'
            cite["href"] = href
            return f'<a class="cite-link" href="{esc(href)}" target="_blank" rel="noopener">{code}</a>'
        return code

    # -- schema lane (D7)
    def load_schema(self):
        tables = []
        if not self.schema_dir:
            return tables
        for f in sorted(os.listdir(self.schema_dir)):
            if f.endswith(".json"):
                t = bd.load_json(os.path.join(self.schema_dir, f), f"schema/{f}")
                t.setdefault("table", os.path.splitext(f)[0])
                t.setdefault("state", "existing")
                tid = self.uid("tbl-" + slug(t["table"]))
                t["id"] = tid
                self.define(tid, "table", f'{t["table"]} ({t["state"]})', t.get("summary", ""))
                for c in t.get("columns", []):
                    c.setdefault("change", "new" if t["state"] == "new" else "unchanged")
                tables.append(t)
        return tables

    # -- E4: draw_diagrams.mjs (system-design, unchanged per D1) paints every box solid, while the legend swatch for a
    # proposed box is dashed. Dash the tldraw-orange strokes (the proposed tone) in every copied diagram SVG.
    PROPOSED_STROKE = re.compile(r'<path\b(?![^>]*stroke-dasharray)([^>]*\bstroke="#e16919"[^>]*)/>')

    def dash_proposed(self, files):
        for published in files:
            if not published.endswith(".svg") or not published.startswith("diagrams/"):
                continue
            dest = os.path.join(self.out_dir, published)
            if not os.path.isfile(dest):
                continue
            svg = open(dest, encoding="utf-8").read()
            out, n = self.PROPOSED_STROKE.subn(r'<path stroke-dasharray="9 6"\1/>', svg)
            if n:
                open(dest, "w", encoding="utf-8").write(out)

    # -- figures
    def diagram_src(self, key):
        cands = [os.path.join(d, key + ext) for d in (self.diagram_dir, os.path.join(self.md_dir, "diagrams")) if d for ext in (".svg", ".png")]
        src = next((c for c in cands if os.path.isfile(c)), None)
        ext = os.path.splitext(src)[1] if src else ".svg"
        return f"diagrams/{key}{ext}", [src] if src else []

    def node_diagram(self, key, title, attrs, legend=None, reading=None):
        published, cands = self.diagram_src(key)
        url, src = self.assets.ref(published, cands)
        fid = self.uid(attrs.get("id") or f"fig-{slug(key)}")
        self.fig_no += 1
        if not src:
            self.warn.append(f"diagram {key}: no {published} in {self.diagram_dir or 'diagrams/'}")
            return {"type": "placeholder", "id": fid, "html": f"Diagram {esc(key)} is missing: {self.inline(title)}"}
        size = bd.img_size(src) or [0, 0]
        node = {"type": "diagram", "id": fid, "key": key, "src": url, "width": size[0], "height": size[1], "title": self.inline(title),
                "alt": attrs.get("alt") or self.text_of(title), "fit": bool(attrs.get("fit")) and attrs.get("fit") != "no",
                "legend": self.legend_items(legend) if legend else [], "reading": self.inline(reading) if reading else "", "hotspots": self.hotspots(key, src, size)}
        self.define(fid, "diagram", title, reading or "")
        if attrs.get("id"):
            self.nav_sub(fid, self.text_of(title), attrs)
        return node

    def legend_items(self, arg):
        items = []
        for part in re.split(r"\s*\|\s*", arg.strip()):
            if not part:
                continue
            label, tone = part.rsplit("=", 1) if "=" in part else (part, part)
            cls = " ".join(dict.fromkeys(" ".join(LEGEND_TONES.get(t.strip().lower(), t.strip().lower()) for t in tone.strip().split()).split()))
            items.append({"label": self.inline(label.strip().lstrip("*").strip()), "tone": cls})
        return items

    def hotspots(self, key, src, size):
        path = os.path.join(os.path.dirname(src), key + ".hotspots.json")
        if not os.path.isfile(path):
            return []
        spec = bd.load_json(path, "hotspots")
        vb = spec.get("viewBox")
        if not vb:
            head = open(src, "rb").read(4096).decode("utf-8", "ignore")
            m = re.search(r'viewBox="([\d.\-]+)[ ,]+([\d.\-]+)[ ,]+([\d.]+)[ ,]+([\d.]+)"', head)
            vb = [float(x) for x in m.groups()] if m else ([0, 0, size[0], size[1]] if size[0] else None)
        if not vb:
            self.warn.append(f"hotspots {key}: no viewBox")
            return []
        x0, y0, w, h = vb
        out = []
        tables = {t["table"]: t["id"] for t in self.schema}
        for b in spec.get("boxes", []):
            if not (b.get("href") or b.get("image") or b.get("note") or b.get("table")):
                continue
            hs = {"label": b.get("label", ""), "x": round((b["x"] - x0) / w * 100, 2), "y": round((b["y"] - y0) / h * 100, 2),
                  "w": round(b["w"] / w * 100, 2), "h": round(b["h"] / h * 100, 2), "href": b.get("href", ""), "note": b.get("note", ""), "tone": b.get("tone", "")}
            if b.get("table"):
                if b["table"] in tables:
                    hs["table"] = tables[b["table"]]
                else:
                    self.warn.append(f"hotspot {key}/{b.get('label', '')!r}: table {b['table']} has no schema/{b['table']}.json")
            if b.get("image"):
                published = bd.published_for(b["image"], "images")
                url, s = self.assets.ref(published, [os.path.join(self.md_dir, b["image"]), os.path.join(self.out_dir, published)])
                if not s:
                    self.warn.append(f"hotspot image {b['image']} ({key}): not found")
                hs["image"] = url
            if hs["href"].startswith("#") and hs["href"][1:]:
                self.pending_hrefs.append((key, b.get("label", ""), hs["href"][1:]))
            out.append(hs)
        return out

    def node_shot(self, b, size_mode):
        published = bd.published_for(b["src"], "images")
        cands = [os.path.join(self.md_dir, b["src"]), os.path.join(self.out_dir, published)] + ([b["src"]] if os.path.isabs(b["src"]) else [])
        url, s = self.assets.ref(published, cands)
        if not s:
            self.warn.append(f"image {b['src']}: not found")
        size = bd.img_size(s) or self.jpeg_size(s) or [0, 0]
        cls = size_mode
        if cls == "auto":
            w, h = size
            cls = ("phone" if (w <= 420 and h > w) else "col" if w and h / w >= 1.2 else "wide" if w >= 1000 else "mid") if w else "mid"
        return {"type": "shot", "src": url, "alt": strip_tags(b["alt"] or ""), "caption": self.inline(b["title"]) if b["title"] else "", "size": cls, "width": size[0], "height": size[1]}

    @staticmethod
    def jpeg_size(path):
        if not path or not path.lower().endswith((".jpg", ".jpeg")):
            return None
        try:
            d = open(path, "rb").read()
        except OSError:
            return None
        i = 2
        while i < len(d) and d[i] == 0xFF:
            marker, L = d[i + 1], int.from_bytes(d[i + 2:i + 4], "big")
            if marker in (0xC0, 0xC1, 0xC2):
                return [int.from_bytes(d[i + 7:i + 9], "big"), int.from_bytes(d[i + 5:i + 7], "big")]
            i += 2 + L
        return None

    # -- blocks
    def nodes(self, blocks, ctx):
        out, i = [], 0
        while i < len(blocks):
            b = blocks[i]
            if b["t"] == "marker" and b["kind"] == "diagram":
                key, title = bd.split_marker(b["arg"])
                title, attrs = parse_attrs(title)
                legend = reading = None
                j = i + 1
                while j < len(blocks) and blocks[j]["t"] == "marker" and blocks[j]["kind"] in ("legend", "reading"):
                    if blocks[j]["kind"] == "legend":
                        legend = blocks[j]["arg"]
                    else:
                        reading = blocks[j]["arg"]
                    j += 1
                out.append(self.node_diagram(key, title, attrs, legend, reading))
                i = j
                continue
            n = self.node(b, ctx)
            if n:
                out.append(n)
            i += 1
        return out

    IMG_ONLY = re.compile(r'^(?:\s*!\[[^\]]*\]\([^)]*\)\s*)+$')
    IMG = re.compile(r'!\[([^\]]*)\]\(\s*(\S+?)(?:\s+"([^"]*)")?\s*\)')

    def para(self, text, ctx, default_cls=""):
        if self.IMG_ONLY.match(text) and len(self.IMG.findall(text)) > 1:
            shots = [self.node_shot({"alt": a, "src": src, "title": t}, ctx.get("shots", "auto")) for a, src, t in self.IMG.findall(text)]
            if ctx.get("grid"):
                for sh in shots:
                    sh["size"] = "col"
            return {"type": "shots", "grid": ctx.get("grid", ""), "media": shots}
        text, attrs = parse_attrs(text)
        cls = [c for c in (default_cls or "").split() if c]
        cls += [k for k in ("small", "muted", "lead") if attrs.get(k)]
        if attrs.get("class"):
            cls += attrs["class"].split()
        node = {"type": "p", "html": self.inline(text), "cls": list(dict.fromkeys(cls))}
        if attrs.get("id"):
            node["id"] = self.uid(attrs["id"])
            self.define(node["id"], "paragraph", text[:80], text)
        if re.fullmatch(r"\*\*[^*]+\*\*", text.strip()):
            node["cls"].append("lead-label")
        return node

    def node(self, b, ctx):
        t = b["t"]
        if t == "p":
            return self.para(b["text"], ctx, ctx.get("p_class", ""))
        if t == "box":
            return self.node_box(b, ctx)
        if t == "h":
            text, attrs = parse_attrs(b["text"])
            lvl = min(max(b["level"], 3), 6)
            hid = attrs.get("id") or (slug(self.text_of(text)) if lvl == 3 else None)
            node = {"type": "heading", "level": lvl, "html": self.inline(text)}
            if hid:
                node["id"] = self.uid(hid)
                self.define(node["id"], "heading", text)
                if lvl == 3 and ctx.get("top"):
                    self.nav_sub(node["id"], self.text_of(text), attrs)
            return node
        if t == "list":
            return self.node_list(b, ctx)
        if t == "table":
            return self.node_table(b)
        if t == "code":
            return self.node_code(b)
        if t == "quote":
            return {"type": "callout", "tone": b.get("kind") or "quote", "blocks": self.nodes(b["blocks"], dict(ctx, p_class="", top=False))}
        if t == "hr":
            return {"type": "hr"}
        if t == "comment":
            if "PR-CARDS" in b["text"]:
                return self.node_prs()
            return None
        if t == "img":
            return self.node_shot(b, ctx.get("shots", "auto"))
        if t == "marker":
            kind, arg = b["kind"], b["arg"]
            if kind == "legend":
                return {"type": "legend", "items": self.legend_items(arg)}
            if kind == "reading":
                return self.para(arg, ctx, "small muted")
            if kind in ("image", "embedded content"):
                return {"type": "placeholder", "html": f"Image: {self.inline(arg)}"}
            self.warn.append(f"unsupported marker [{kind}: ...] in a TDD page")
            return None
        return None

    def node_list(self, b, ctx, steps=False):
        checks = all(it["check"] is not None for it in b["items"])
        items = []
        for it in b["items"]:
            text, cls = it["text"], []
            m = re.match(r"^\\?\[(new|changed|done|open|held)\\?\]\s+", text)
            if m:
                cls.append(m.group(1))
                text = text[m.end():]
            items.append({"html": self.inline(text), "cls": cls, "check": it["check"], "children": [self.node_list(c, ctx) for c in it["children"]]})
        return {"type": "list", "ordered": bool(b["ordered"]), "start": b["start"], "steps": steps, "checks": checks, "items": items}

    def node_table(self, b):
        ncol = max([len(b["head"])] + [len(r) for r in b["rows"]])
        head = b["head"] + [""] * (ncol - len(b["head"]))
        rows = [r + [""] * (ncol - len(r)) for r in b["rows"]]
        def numeric(c):
            t = self.text_of(c)
            return bool(re.fullmatch(r"[~≈<>]?\s*[\d][\d.,%\s×x+\-–/:]*", t)) and len(t) < 24
        num_col = [all(numeric(r[j]) for r in rows if r[j].strip()) and any(r[j].strip() for r in rows) for j in range(ncol)]
        status_col = [bool(re.fullmatch(r"(state|status)", self.text_of(h).lower())) for h in head]
        node = {"type": "table", "cols": ncol, "head": [{"html": self.inline(h), "align": (b["align"][j] if j < len(b["align"]) else None)} for j, h in enumerate(head)], "rows": []}
        for r in rows:
            first = self.text_of(r[0]).strip()
            rid, tm = None, TOKEN.fullmatch(first)
            if tm and token_id(first) not in self.ids:
                rid = token_id(first)
                self.ids.add(rid)
                self.define(rid, "row", first + " · " + self.text_of(r[1] if ncol > 1 else ""), self.text_of(r[2] if ncol > 2 else (r[1] if ncol > 1 else "")))
            cells = []
            for j, c in enumerate(r):
                cls = (["num"] if num_col[j] else []) + (["tok"] if j == 0 and tm else [])
                content = self.inline(c)
                if status_col[j] and c.strip() and '<span class="chip' not in content:
                    k = bd.STATUS_WORDS.get(re.split(r"[\s,;(]+", strip_tags(content).lower())[0])
                    if k:
                        content = f'<span class="chip {k}">{content}</span>'
                cells.append({"html": content, "cls": cls, "align": (b["align"][j] if j < len(b["align"]) and not num_col[j] else None)})
            node["rows"].append({"id": rid, "cells": cells})
        return node

    def node_code(self, b):
        lang, raw = b["lang"], b["text"]
        text = esc(raw, quote=False)
        if lang in ("sql", "psql", "postgres"):
            text = re.sub(r"(--[^\n]*)", r'<span class="c">\1</span>', text)
            text = re.sub(SQL_KW, lambda m: f'<span class="k">{m.group(0)}</span>', text)
            text = re.sub(r'<span class="c">(.*?)</span>', lambda m: '<span class="c">' + re.sub(r"</?span[^>]*>", "", m.group(1)) + "</span>", text)
        elif lang in ("ts", "tsx", "js", "typescript", "javascript", "mjs"):
            text = re.sub(r"(//[^\n]*)", r'<span class="c">\1</span>', text)
        elif lang in ("py", "python", "sh", "bash", "yaml", "yml", "toml"):
            text = re.sub(r"(?m)(#[^\n]*)$", r'<span class="c">\1</span>', text)
        return {"type": "code", "lang": lang, "text": raw, "html": text, "section": self.cur_section["id"] if self.cur_section else None, "anchor": self.cur_anchor}

    # -- containers
    def node_box(self, b, ctx):
        kind, title, attrs, blocks = b["kind"], b["title"], b["attrs"], b["blocks"]
        inner = dict(ctx, top=False)
        if kind == "cards":
            return self.box_cards(blocks, attrs, inner)
        if kind == "persona":
            return self.box_persona(title, attrs, blocks, inner)
        if kind == "duo":
            return {"type": "duo", "items": self.nodes(blocks, inner)}
        if kind == "today":
            return self.box_today(title, attrs, blocks, inner)
        if kind == "fold":
            return self.box_fold(title, attrs, blocks, inner)
        if kind == "group":
            t, sub = split_title(title)
            gid = self.uid(attrs["id"]) if attrs.get("id") else None
            ctx["has_group"] = True
            if self.cur_section is not None:
                self.cur_section["has_group"] = True
            return {"type": "group", "id": gid, "title": self.inline(t), "sub": self.inline(sub), "blocks": self.nodes(blocks, dict(inner, p_class=""))}
        if kind == "decisions":
            return {"type": "decisions", "blocks": self.nodes(blocks, dict(inner, p_class="small muted"))}
        if kind == "decision":
            return self.box_decision(title, attrs, blocks, inner)
        if kind == "options":
            return self.box_options(blocks, inner)
        if kind == "steps":
            lists = [self.node_list(x, inner, steps=True) for x in blocks if x["t"] == "list"]
            rest = self.nodes([x for x in blocks if x["t"] != "list"], inner)
            return {"type": "steps", "blocks": rest + lists}
        if kind == "cols":
            return {"type": "cols", "cols": [self.nodes(x["blocks"], inner) if x["t"] == "box" and x["kind"] == "col" else [self.node(x, inner)] for x in blocks]}
        if kind == "col":
            return {"type": "col", "blocks": self.nodes(blocks, inner)}
        if kind == "glossary":
            return self.box_glossary(blocks)
        if kind == "callout":
            tone = attrs.get("tone") or (title.strip().lower() if title.strip().lower() in ("note", "warn", "good", "danger", "violet", "blue") else "note")
            return {"type": "callout", "tone": tone, "blocks": self.nodes(blocks, dict(inner, p_class=""))}
        self.warn.append(f"unknown container ::: {kind}; rendered as a plain block")
        return {"type": "box", "kind": kind, "blocks": self.nodes(blocks, inner)}

    def box_cards(self, blocks, attrs, ctx):
        cards, cur = [], None
        for x in blocks:
            if x["t"] == "h":
                text, hattrs = parse_attrs(x["text"])
                cur = {"title": text, "attrs": hattrs, "blocks": []}
                cards.append(cur)
            elif cur is not None:
                cur["blocks"].append(x)
            else:
                cur = {"title": "", "attrs": {}, "blocks": [x]}
                cards.append(cur)
        out = []
        for c in cards:
            node = {"title": self.inline(c["title"]), "blocks": self.nodes(c["blocks"], dict(ctx, p_class=""))}
            if c["attrs"].get("id"):
                node["id"] = self.uid(c["attrs"]["id"])
                self.define(node["id"], "card", c["title"], " ".join(strip_tags(n.get("html", "")) for n in node["blocks"]))
            out.append(node)
        return {"type": "cards", "four": bool(attrs.get("four")), "stat": bool(attrs.get("stat")), "cards": out}

    def box_persona(self, title, attrs, blocks, ctx):
        pid = self.uid(attrs.get("id") or "fe-" + slug(self.text_of(title)))
        shots_mode = attrs.get("shots", "auto")
        text, media, today = [], [], []
        for x in blocks:
            (media if x["t"] == "img" else today if (x["t"] == "box" and x["kind"] == "today") else text).append(x)
        per_shot = shots_mode if shots_mode in ("phone", "mid", "wide", "col") else "auto"
        shots = [self.node_shot(x, per_shot) for x in media]
        grid = shots_mode if shots_mode in ("cols3", "cols2") else ""
        if not grid and shots_mode == "auto" and len(shots) >= 3 and all(s["size"] in ("col", "phone") for s in shots):
            grid = "cols3"
        if grid:
            for s in shots:
                s["size"] = "col"
        body = self.nodes(text, dict(ctx, p_class="small"))
        first_p = next((strip_tags(n["html"]) for n in body if n["type"] == "p"), "")
        self.define(pid, "persona", title, first_p)
        self.nav_sub(pid, self.text_of(title), attrs)
        node = {"type": "persona", "id": pid, "title": self.inline(title), "chip": attrs.get("chip", ""), "chipTone": attrs.get("tone", "new"), "grid": grid, "text": body, "media": shots, "today": None}
        if today:
            td = today[0]
            node["today"] = self.box_today(td["title"], td["attrs"], td["blocks"], dict(ctx, shots=per_shot, grid=grid))
        return node

    def box_today(self, title, attrs, blocks, ctx):
        shots = [self.node_shot(x, ctx.get("shots", "auto")) for x in blocks if x["t"] == "img"]
        if ctx.get("grid"):
            for s in shots:
                s["size"] = "col"
        rest = self.nodes([x for x in blocks if x["t"] != "img"], dict(ctx, p_class="small"))
        for n in [n for n in rest if n.get("type") == "shots"]:
            shots.extend(n["media"]); rest.remove(n)
        return {"type": "today", "label": attrs.get("label", "Today, for comparison"), "summary": self.inline(title), "blocks": rest, "media": shots, "grid": ctx.get("grid", "")}

    def box_fold(self, title, attrs, blocks, ctx):
        t, one = split_title(title)
        explicit = bool(attrs.get("id"))
        fid = self.uid(attrs.get("id") or "fold-" + slug(self.text_of(t)))
        self.define(fid, "fold", t, one)
        if explicit:
            self.nav_sub(fid, self.text_of(t), attrs)
        return {"type": "fold", "id": fid, "title": self.inline(t), "one": self.inline(one), "open": bool(attrs.get("open")), "blocks": self.nodes(blocks, dict(ctx, p_class=""))}

    def box_decision(self, title, attrs, blocks, ctx):
        did, question = split_title(title)
        status = attrs.get("status", "")
        state = attrs.get("state")
        if not state:
            low = status.lower()
            state = "done" if (re.search(r"\d{4}-\d{2}-\d{2}", status) and not re.match(r"(open|provisional|proposed|waiting)", low)) else "open"
            if re.match(r"(held|blocked|rejected)", low):
                state = "held"
        anchor = self.uid(attrs.get("id") or token_id(did))
        body = self.nodes(blocks, dict(ctx, p_class=""))
        paras = [html.unescape(strip_tags(n["html"])) for n in body if n["type"] == "p"]
        first_p = paras[0] if paras else ""
        reading = next((t for t in paras if t.lower().startswith("read as")), "")
        quote = first_p if first_p != reading else ""
        status_text = self.text_of(status)
        m_date = re.search(r"\d{4}-\d{2}-\d{2}", status_text)
        by = re.split(r"[,·]", status_text, 1)[0].strip() if status_text else ""
        self.define(anchor, "decision", f"{did} · {question}", first_p)
        self.decisions.append({"id": did, "anchor": anchor, "question": self.text_of(question), "status": status_text, "state": state, "section": self.cur_section["id"] if self.cur_section else None,
                               "quote": quote, "reading": reading, "decidedBy": by, "decidedOn": m_date.group(0) if m_date else None})
        self.nav_sub(anchor, f"{did} · {self.text_of(question)}", attrs)
        return {"type": "decision", "id": anchor, "did": did, "question": self.inline(question), "status": self.inline(status), "state": state, "wide": bool(attrs.get("wide")), "blocks": body}

    def box_options(self, blocks, ctx):
        opts, cur = [], None
        for x in blocks:
            if x["t"] == "h":
                text, hattrs = parse_attrs(x["text"])
                cur = {"title": self.inline(text), "rec": bool(hattrs.get("rec")), "blocks": []}
                opts.append(cur)
            elif cur is not None:
                cur["blocks"].append(self.node(x, ctx))
        return {"type": "options", "options": opts}

    def box_glossary(self, blocks):
        items = []
        for x in blocks:
            if x["t"] != "list":
                continue
            for it in x["items"]:
                m = re.match(r"^\*\*([^*]+)\*\*\s*[:—–-]?\s*(.*)$", it["text"])
                if not m:
                    continue
                term, definition = m.group(1).strip(), m.group(2).strip()
                gid = self.uid("g-" + slug(term))
                self.glossary[term] = gid
                self.define(gid, "glossary", term, definition)
                items.append({"id": gid, "term": term, "html": self.inline(definition)})
        return {"type": "glossary", "items": items}

    def node_prs(self):
        if not self.prs_dir:
            self.warn.append("<!-- PR-CARDS --> but no prs/ folder (pass --prs)")
            return {"type": "placeholder", "html": "PR cards: no prs/pr-NN.md yet."}
        prs = load_prs(self.prs_dir)
        if not prs:
            self.warn.append(f"--prs {self.prs_dir}: no pr-NN.md files")
            return None
        groups = []
        for p in prs:
            if not groups or groups[-1]["ms"] != p["ms"]:
                head = re.split(r"\s+·\s+|:\s+", p["ms"], maxsplit=1)[0].strip()
                mid = None
                if TOKEN.fullmatch(head) and token_id(head) not in self.ids:
                    mid = token_id(head)
                    self.ids.add(mid)
                    self.define(mid, "milestone", p["ms"], p["ms_note"] or "")
                groups.append({"ms": esc(p["ms"]), "id": mid, "note": esc(p["ms_note"] or ""), "prs": []})
            pid = self.uid(f"pr-{p['label']}")
            body = bp.markdown(p["body"]).replace("<table>", '<div class="tbl"><table>').replace("</table>", "</table></div>")
            body = re.sub(r"^\s*<h2>Summary</h2>\s*", "", body)
            body = re.sub(r"\[([^\]]+)\]\((#[\w.-]+)\)", r'<a href="\2">\1</a>', body)
            body = self.sanitize(re.sub(r"<code>([^<]+)</code>", self.cite_code, body))
            mentioned = sorted(set(m.group(1) for m in TOKEN.finditer(strip_tags(body))), key=lambda t: (re.match(r"[A-Z]+", t).group(0), float(re.sub(r"[^\d.]", "", t) or 0), t))
            paths = sorted(set(c.group(1) for c in (CITE.match(html.unescape(x)) for x in re.findall(r"<code>([^<]+)</code>", bp.markdown(p["body"]))) if c))
            deps_html = ("Depends on " + ", ".join(esc(d) for d in p["depends_on"])) if p["depends_on"] else ""
            groups[-1]["prs"].append({"n": p["n"], "sub": p["sub"], "label": p["label"], "id": pid, "title": self.sanitize(bp.inline(p["title"])), "change": self.sanitize(bp.inline(p["change"])),
                                      "dependsOn": p["depends_on"], "deps": deps_html, "decisions": [t for t in mentioned if t.startswith("D")], "tokens": mentioned, "paths": paths,
                                      "rollback": self.sanitize(bp.inline(p["rollback"])) if p.get("rollback") and p["rollback"] != "not stated" else "", "bodyHtml": body, "file": p["file"]})
            self.define(pid, "pr", f"PR {p['label']} · {p['title']}", p["change"])
        self.pr_count = len(prs)
        return {"type": "prs", "groups": groups}

    def nav_sub(self, sid, label, attrs):
        if attrs.get("nav") in ("no", False, "none") or self.cur_section is None:
            return
        if isinstance(attrs.get("nav"), str) and attrs["nav"] not in ("yes", "no"):
            label = attrs["nav"]
        self.cur_section["subs"].append({"id": sid, "title": label})

    # -- page
    def structure(self, text):
        text = text.replace("\r\n", "\n").replace("&#91;", "[").replace("&#93;", "]")
        blocks = parse_ext(text.split("\n"))
        title, pre, sections, cur = None, [], [], None
        for b in blocks:
            if b["t"] == "h" and b["level"] == 1 and title is None:
                title = b["text"]
                continue
            if b["t"] == "h" and b["level"] == 2:
                t, attrs = parse_attrs(b["text"])
                cur = {"title": t, "attrs": attrs, "blocks": [], "subs": [], "has_group": False}
                sections.append(cur)
                continue
            (cur["blocks"] if cur else pre).append(b)
        for n, s in enumerate(sections, 1):
            s["num"], s["id"] = n, self.uid(s["attrs"].get("id") or slug(self.text_of(s["title"])))
        return title, pre, sections

    LINK_KEYS = {"html", "caption", "reading", "one", "sub", "bodyHtml", "summary", "status", "change", "rollback", "question", "title", "label", "ms", "note", "deps"}

    def link_tree(self, node, families, stats, seen=None):
        if isinstance(node, list):
            for x in node:
                self.link_tree(x, families, stats, seen)
        elif isinstance(node, dict):
            if node.get("type") == "glossary":
                return  # the definitions themselves are not re-linked
            for k, v in node.items():
                if k in ("subs", "hotspots", "nav"):
                    continue  # rendered as plain text by the app (rail, hotspot cards)
                if k in self.LINK_KEYS and isinstance(v, str) and v:
                    node[k] = link_refs(v, self.ids, families, self.glossary, stats, seen)
                elif isinstance(v, (list, dict)):
                    self.link_tree(v, families, stats, seen)

    def build(self):
        a, meta = self.a, self.meta
        src_text = open(a.src, encoding="utf-8").read()
        title, pre, sections = self.structure(src_text)
        h1 = meta.get("h1") or title or os.path.splitext(os.path.basename(a.src))[0]
        tab_title = meta.get("title") or bd.page_title(self.text_of(h1))
        out_sections = []
        for s in sections:
            self.cur_section = s
            ctx = {"section": s["id"], "p_class": "", "top": True}
            blocks = list(s["blocks"])
            nodes = []
            if s["attrs"].get("lead") not in ("no", False) and blocks and blocks[0]["t"] == "p":
                nodes.append(self.para(blocks.pop(0)["text"], ctx, "lead"))
            nodes += self.nodes(blocks, ctx)
            self.define(s["id"], "section", f"{s['num']} · {s['title']}", "")
            out_sections.append({"id": s["id"], "num": s["num"], "title": self.inline(s["title"]), "titleText": self.text_of(s["title"]),
                                 "chip": s["attrs"].get("chip", ""), "chipTone": s["attrs"].get("tone", "draft"), "hasGroup": s["has_group"], "blocks": nodes, "subs": s["subs"]})
        self.cur_section = None
        status = meta.get("status", "Draft for review")
        low = status.lower()
        tone = meta.get("status_tone") or ("draft" if low.startswith("draft") else "done" if low.startswith(("as built", "reviewed", "approved", "accepted")) else "held" if low.startswith(("superseded", "withdrawn")) else "proposed" if "review" in low else "draft")
        author = next((p["name"] for p in meta.get("people", []) if p["role"].lower() == "author"), "")
        footer = meta.get("footer") or ((f"Draft by {author}, {meta.get('updated', '')}. " if author else "") + "Decisions get a D-number only once the owner answers; everything else is marked as a proposal.")
        for key, label, anchor in self.pending_hrefs:
            if anchor not in self.ids:
                self.warn.append(f"hotspot {key}/{label!r}: anchor #{anchor} is not defined on the page")
        stats = {"linked": 0, "unlinked": []}
        families = dict(meta.get("refs", {}))
        for fam, href in families.items():
            if href.startswith("#") and href[1:] not in self.ids:
                self.warn.append(f"refs family {fam} points at {href}, which is not an anchor on the page")
        for sec in out_sections:
            stats["section"] = sec.get("id")
            self.link_tree(sec, families, stats, seen=set())
        lede = [self.para(b["text"], {"p_class": ""}, "lead") for b in pre if b["t"] == "p"]
        stats["section"] = "lede"
        self.link_tree(lede, families, stats, seen=set())
        words = len(re.findall(r"[A-Za-z0-9'’]+", re.sub(r"```.*?```|\]\([^)]*\)|[`*_#|\[\]()>{}-]", " ", src_text, flags=re.S)))
        for u in sorted(set(stats["unlinked"])):
            self.warn.append(f"reference token {u} has no definition on the page (no element with id {token_id(u)}, no refs family in meta.json)")
        files = self.assets.finish()
        self.dash_proposed(files)
        content = {
            "template": "tdd/1", "generated": os.environ.get("TDD_GENERATED") or datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "source": os.path.basename(a.src),
            "title": self.inline(h1), "titleText": self.text_of(h1), "tabTitle": tab_title,
            "meta": {"eyebrow": self.inline(meta.get("eyebrow", "Technical design")), "project": meta.get("project"), "people": meta.get("people", []),
                     "updated": meta.get("updated", ""), "rev": meta.get("rev"), "revNote": meta.get("rev_note", ""), "status": status, "statusTone": tone,
                     "repoUrl": meta.get("repo_url"), "sha": meta.get("sha"), "shaDate": meta.get("sha_date"), "repoPath": home_rel(meta.get("repo_path")),
                     "pathAliases": self.aliases, "refs": families, "footer": self.inline(footer),
                     "dataSources": meta.get("data_sources") or meta.get("dataSources") or []},
            "lede": lede,
            "nav": [{"id": s["id"], "num": s["num"], "title": s["titleText"], "subs": s["subs"]} for s in out_sections],
            "sections": out_sections, "defs": self.defs, "anchors": self.anchors, "decisions": self.decisions, "cites": self.cites,
            "glossary": self.glossary, "schema": self.schema, "refs": {"linked": stats["linked"], "unlinked": sorted(set(stats["unlinked"])), "links": stats.get("links", [])},
            "stats": {"sections": len(out_sections), "diagrams": self.fig_no, "prs": self.pr_count, "decisions": len(self.decisions), "words": words, "readingMinutes": max(1, round(words / 230))},
        }
        os.makedirs(self.out_dir, exist_ok=True)
        cpath = os.path.join(self.out_dir, "content.json")
        json.dump(content, open(cpath, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
        manifest = {"page": "tdd.html", "root": self.out_dir, "title": tab_title, "template": "tdd/1", "content": "content.json",
                    "files": files, "missing": self.assets.missing, "over_15mb": [p for p, s in files.items() if os.path.getsize(s) > bd.FILE_LIMIT], "warnings": self.warn}
        json.dump(manifest, open(os.path.join(self.out_dir, "content.files.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
        for w in self.warn:
            print("warning:", w, file=sys.stderr)
        print(f"wrote {cpath}: {len(out_sections)} sections, {self.fig_no} diagrams, {len(self.decisions)} decisions, {self.pr_count} PRs, {len(self.schema)} schema tables, "
              f"{stats['linked']} reference links ({len(set(stats['unlinked']))} unlinked), {len(self.cites)} citations, {len(files)} files ({len(self.assets.missing)} missing)")
        if a.strict and self.warn:
            sys.exit(f"--strict: {len(self.warn)} warning(s)")

def home_rel(p):
    """~/… instead of an absolute home path, so published pages and lanes carry no user path."""
    if not isinstance(p, str):
        return p
    home = os.path.expanduser("~")
    return "~" + p[len(home):] if p.startswith(home + os.sep) or p == home else p

class Args:
    pass

def parse_args(argv):
    a = Args()
    flags = {"--meta": "meta", "--diagrams": "diagrams", "--prs": "prs", "--schema": "schema"}
    bools = {"--no-copy": "no_copy", "--strict": "strict"}
    for v in flags.values():
        setattr(a, v, None)
    for v in bools.values():
        setattr(a, v, False)
    pos, i = [], 0
    while i < len(argv):
        x = argv[i]
        if x in flags:
            if i + 1 >= len(argv):
                sys.exit(f"{x} needs a value")
            setattr(a, flags[x], argv[i + 1])
            i += 2
        elif x in bools:
            setattr(a, bools[x], True)
            i += 1
        elif x.startswith("--"):
            sys.exit(f"unknown option {x}\n{__doc__}")
        else:
            pos.append(x)
            i += 1
    if len(pos) != 2:
        sys.exit(__doc__)
    a.src, a.out = pos
    if not os.path.isfile(a.src):
        sys.exit(f"{a.src}: no such file")
    return a

if __name__ == "__main__":
    Compiler(parse_args(sys.argv[1:])).build()
