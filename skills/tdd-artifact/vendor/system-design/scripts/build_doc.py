#!/usr/bin/env python3
"""Build a claude.ai HTML artifact page from a markdown technical spec, styled by the house doc system.

Usage:
  build_doc.py <source.md> <out.html> [--diagrams <dir>] [--diagram-map <json>] [--video-map <json>]
               [--videos <dir>] [--media <media.json>] [--modules <modules.json>] [--prs <dir>]
               [--css <doc-system.css>] [--project-tokens <design-tokens.json>] [--meta <json>]
               [--inline] [--no-copy] [--strict]
  build_doc.py --sync-css  [<tokens.json> [<doc-system.css>]]   rewrite the FONTS and TOKENS blocks of the CSS from tokens.json
  build_doc.py --check-css [<tokens.json> [<doc-system.css>]]   exit 1 if the CSS token blocks drifted from tokens.json

Output is page content only (no doctype/html/head/body): <title>, the viewport-fit-sides meta, one <style>, the markup,
one <script>. Next to it, <out stem>.files.json lists every relative file the page references (published path -> source)
for the Artifact tool's `files`, plus `missing`, `skipped` (licensed-reference media) and warnings.
Referenced files are copied into the output folder at their published paths unless --no-copy. --inline embeds images and
posters as data: URIs (videos stay files). Order with PR cards: build_doc.py, then build_prs.py <out.html> <prs-dir>
(or pass --prs here to render the cards natively). Shapes of every JSON input: templates/doc-system/README.md.
Exit 0 on success (warnings printed); 1 on bad input, or on any warning with --strict."""
import base64, html, importlib.util, json, mimetypes, os, re, shutil, struct, sys
sys.dont_write_bytecode = True  # importing build_prs.py must not leave __pycache__ in the skill folder

HERE = os.path.dirname(os.path.abspath(__file__))
SYSDIR = os.path.normpath(os.path.join(HERE, "..", "templates", "doc-system"))
DEFAULT_CSS = os.path.join(SYSDIR, "doc-system.css")
DEFAULT_TOKENS = os.path.join(SYSDIR, "tokens.json")
FILE_LIMIT = 15 * 1024 * 1024  # Artifact publish limit per binary file
esc = html.escape

# ----------------------------------------------------------------------------- tokens -> CSS

def _fmt_stack(stack):
    return ", ".join(f'"{f}"' if (" " in f and not f.startswith(('"', "'"))) else f for f in stack)

def fonts_import(tokens):
    fams = [f["google"] for f in tokens.get("type", {}).get("families", {}).values() if f.get("google")]
    if not fams:
        return ""
    return '@import url("https://fonts.googleapis.com/css2?' + "&".join("family=" + g for g in fams) + '&display=swap");'

def tokens_to_doc_css(tokens):
    """The three token blocks (bare :root with every token, the guarded prefers-color-scheme dark block, the
    data-theme="dark" block) plus the non-color tokens on bare :root. Pure function of tokens.json."""
    light, dark = [], []
    for t in tokens["color"]["tokens"]:
        light.append(f"  --{t['name']}: {t['value']['light']};")
        dark.append(f"--{t['name']}: {t['value']['dark']};")
    ty = tokens.get("type", {})
    other = []
    for role, f in ty.get("families", {}).items():
        other.append(f"  --font-{role}: {_fmt_stack(f['stack'])};")
    for k, v in ty.get("scale", {}).items():
        other.append(f"  --fs-{k}: {v};")
    for k, v in ty.get("leading", {}).items():
        other.append(f"  --lh-{k}: {v};")
    for k, v in ty.get("tracking", {}).items():
        other.append(f"  --tr-{k}: {v};")
    if ty.get("measure"):
        other.append(f"  --measure: {ty['measure']};")
    for k, v in tokens.get("spacing", {}).get("scale", {}).items():
        other.append(f"  --sp-{k}: {v};")
    for k, v in tokens.get("radius", {}).items():
        other.append(f"  --r-{k}: {v};")
    for k in ("nav-w", "content-max"):
        if k in tokens.get("layout", {}):
            other.append(f"  --{k}: {tokens['layout'][k]};")
    d_in = "\n".join("    " + x for x in dark)
    d_out = "\n".join("  " + x for x in dark)
    return (f"/* TOKENS:START generated from tokens.json ({tokens.get('name', '')} {tokens.get('version', '')}) by build_doc.py --sync-css; edit tokens.json, not this block */\n"
            ":root {\n  /* color: light values; every token is defined here first */\n" + "\n".join(light) + "\n  color-scheme: light;" +
            "\n  /* type, spacing, radius, layout */\n" + "\n".join(other) + "\n}\n"
            "@media (prefers-color-scheme: dark) {\n  :root:not([data-theme=\"light\"]) {\n" + d_in + "\n    color-scheme: dark;\n  }\n}\n"
            ":root[data-theme=\"dark\"] {\n" + d_out + "\n  color-scheme: dark;\n}\n/* TOKENS:END */")

def _replace_block(css, start, end, block):
    a, b = css.find(start), css.find(end)
    if a < 0 or b < 0:
        sys.exit(f"CSS has no {start} ... {end} markers")
    return css[:a] + block + css[b + len(end):]

def sync_css(tokens_path, css_path, check=False):
    tokens = json.load(open(tokens_path, encoding="utf-8"))
    css = open(css_path, encoding="utf-8").read()
    new = _replace_block(css, "/* FONTS:START", "/* FONTS:END */", "/* FONTS:START from tokens.json type.families */\n" + fonts_import(tokens) + "\n/* FONTS:END */")
    new = _replace_block(new, "/* TOKENS:START", "/* TOKENS:END */", tokens_to_doc_css(tokens))
    if check:
        if new != css:
            sys.exit(f"{css_path}: token blocks differ from {tokens_path}; run build_doc.py --sync-css")
        print("css token blocks match", tokens_path)
        return
    open(css_path, "w", encoding="utf-8").write(new)
    print("synced", css_path, "from", tokens_path, f"({len(tokens['color']['tokens'])} color tokens)")

# DTCG (W3C design tokens) -> house token names. First hit wins.
DTCG_LIGHT = {
    "bg": ["color.background", "color.bg"], "surface": ["color.surface", "color.card"],
    "surface-2": ["color.surface-alt", "color.surface-muted", "color.panel"], "code-bg": ["color.code-bg", "color.code"],
    "fg": ["color.text", "color.foreground", "color.ink", "color.fg"], "muted": ["color.text-muted", "color.muted", "color.text-secondary"],
    "line": ["color.border", "color.line", "color.divider"], "accent": ["color.accent", "color.primary", "color.brand"],
    "secondary": ["color.secondary", "color.proposed"], "good": ["color.success", "color.built", "color.positive"],
    "warn": ["color.warning", "color.default", "color.caution"], "danger": ["color.danger", "color.held", "color.error"],
    "ruled": ["color.ruled"],
}
DTCG_DARK = {
    "bg": ["color.dark.background", "color.dark.bg"], "surface": ["color.dark.surface"], "surface-2": ["color.dark.surface-alt"],
    "code-bg": ["color.dark.code-bg"], "fg": ["color.dark.text", "color.dark.foreground"], "muted": ["color.dark.text-muted", "color.dark.muted"],
    "line": ["color.dark.border", "color.dark.line"], "accent": ["color.dark.accent", "color.dark.primary"],
    "secondary": ["color.dark.proposed", "color.dark.secondary"], "good": ["color.dark.built", "color.dark.success"],
    "warn": ["color.dark.default", "color.dark.warning"], "danger": ["color.dark.held", "color.dark.danger"], "ruled": ["color.dark.ruled"],
}
DTCG_FONT = {"display": ["font.family-display"], "body": ["font.family-body", "font.family-sans", "font.family"], "mono": ["font.family-mono"]}
SYSTEM_FONTS = {"system-ui", "sans-serif", "serif", "monospace", "ui-monospace", "ui-sans-serif", "-apple-system", "helvetica neue",
                "helvetica", "arial", "segoe ui", "sf mono", "menlo", "consolas", "avenir next", "georgia", "times new roman", "roboto"}

def _flat(node, prefix=""):
    for k, v in node.items():
        if k.startswith("$"):
            continue
        if isinstance(v, dict) and "$value" in v:
            yield prefix + k, v["$value"]
        elif isinstance(v, dict):
            yield from _flat(v, prefix + k + ".")

def _lin(c):
    c /= 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def contrast(a, b):
    def lum(h):
        h = h.lstrip("#")
        if len(h) == 3:
            h = "".join(ch * 2 for ch in h)
        r, g, b_ = (int(h[i:i + 2], 16) for i in (0, 2, 4))
        return 0.2126 * _lin(r) + 0.7152 * _lin(g) + 0.0722 * _lin(b_)
    la, lb = sorted((lum(a), lum(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)

def project_token_css(path, house):
    """Override blocks (same three-block shape) from a DTCG design-tokens.json. Returns (css, extra_imports, notes)."""
    try:
        flat = dict(_flat(json.load(open(path, encoding="utf-8"))))
    except (OSError, ValueError) as e:
        sys.exit(f"{path}: {e}")
    def val(v, seen=()):
        if isinstance(v, list):
            return v
        v = str(v)
        if v.startswith("{") and v.endswith("}") and v[1:-1] in flat and v[1:-1] not in seen:
            return val(flat[v[1:-1]], seen + (v[1:-1],))
        return v
    pick = lambda names: next((val(flat[n]) for n in names if n in flat), None)
    light = {k: pick(n) for k, n in DTCG_LIGHT.items() if pick(n)}
    dark = {k: pick(n) for k, n in DTCG_DARK.items() if pick(n)}
    notes = []
    if dark.keys() != light.keys():
        notes.append("light overrides without a dark value keep the house dark value: " + ", ".join(sorted(set(light) - set(dark))) if set(light) - set(dark) else "")
    notes = [n for n in notes if n]
    hv = {t["name"]: t["value"] for t in house["color"]["tokens"]}
    for theme, over in (("light", light), ("dark", dark)):
        merged = {k: over.get(k, hv[k][theme]) for k in hv}
        for fgk in ("fg", "muted", "accent", "secondary", "good", "warn", "danger"):
            for bgk in ("bg", "surface"):
                a, b = merged[fgk], merged[bgk]
                if a.startswith("#") and b.startswith("#") and contrast(a, b) < 4.5:
                    notes.append(f"{theme}: --{fgk} {a} on --{bgk} {b} is {contrast(a, b):.2f}:1 (< 4.5)")
    fonts, imports = {}, []
    house_fams = {f["stack"][0].lower() for f in house.get("type", {}).get("families", {}).values()}
    for role, names in DTCG_FONT.items():
        v = pick(names)
        if not v:
            continue
        stack = v if isinstance(v, list) else [s.strip().strip("\"'") for s in str(v).split(",")]
        fonts[role] = _fmt_stack(stack)
        first = stack[0]
        if first.lower() not in SYSTEM_FONTS and first.lower() not in house_fams:
            imports.append(f'@import url("https://fonts.googleapis.com/css2?family={first.replace(" ", "+")}:wght@400;500;600;700&display=swap");')
    radius = pick(["radius.md", "radius.base", "radius.default"])
    lines = [f"  --{k}: {v};" for k, v in light.items()] + [f"  --font-{k}: {v};" for k, v in fonts.items()]
    if radius:
        lines.append(f"  --r-md: {radius};")
    dk = [f"--{k}: {v};" for k, v in dark.items()]
    css = f"/* PROJECT-TOKENS:START from {os.path.basename(path)}; redefines house tokens only */\n"
    css += ":root {\n" + "\n".join(lines) + "\n}\n"
    if dk:
        css += "@media (prefers-color-scheme: dark) {\n  :root:not([data-theme=\"light\"]) {\n" + "\n".join("    " + x for x in dk) + "\n    color-scheme: dark;\n  }\n}\n"
        css += ":root[data-theme=\"dark\"] {\n" + "\n".join("  " + x for x in dk) + "\n  color-scheme: dark;\n}\n"
    css += "/* PROJECT-TOKENS:END */\n"
    unmapped = [k for k in DTCG_LIGHT if k not in light]
    print(f"project tokens: {len(light)} light, {len(dark)} dark, {len(fonts)} fonts mapped; house value kept for: {', '.join(unmapped) or 'none'}")
    return css, imports, notes

# ----------------------------------------------------------------------------- markdown subset

MARK = re.compile(r"^\s*\\?\[(embedded content|diagram|video|media|image)\s*:\s*(.*?)\s*\\?\]\s*$", re.I)
HEAD = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
FENCE = re.compile(r"^\s*(`{3,}|~{3,})\s*([\w+-]*)")
LI = re.compile(r"^(\s*)([-*+]|\d+[.)])\s+(.*)$")
HR = re.compile(r"^\s*([-*_])(\s*\1){2,}\s*$")
TSEP = re.compile(r"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$")
COMMENT = re.compile(r"^\s*<!--.*-->\s*$")

def is_block_start(l):
    return bool(HEAD.match(l) or FENCE.match(l) or LI.match(l) or HR.match(l) or MARK.match(l) or COMMENT.match(l)
                or l.lstrip().startswith(">") or l.lstrip().startswith("|"))

def split_row(r):
    r = r.strip()
    if r.startswith("|"):
        r = r[1:]
    if r.endswith("|") and not r.endswith("\\|"):
        r = r[:-1]
    cells, cur, i = [], "", 0
    while i < len(r):
        if r[i] == "\\" and i + 1 < len(r) and r[i + 1] == "|":
            cur += "|"
            i += 2
            continue
        if r[i] == "|":
            cells.append(cur.strip())
            cur = ""
        else:
            cur += r[i]
        i += 1
    cells.append(cur.strip())
    return cells

def indent_of(l):
    return len(l) - len(l.lstrip(" \t"))

def parse_list(lines, i, base):
    m = LI.match(lines[i])
    ordered = m.group(2)[0].isdigit()
    start = int(re.match(r"\d+", m.group(2)).group(0)) if ordered else 1
    items, n = [], len(lines)
    while i < n:
        m = LI.match(lines[i])
        if not m or len(m.group(1)) != base or m.group(2)[0].isdigit() != ordered:
            break
        text, check = m.group(3), None
        cm = re.match(r"^\\?\[([ xX])\\?\]\s+(.*)$", text)
        if cm:
            check, text = cm.group(1).lower() == "x", cm.group(2)
        item = {"text": text, "check": check, "children": []}
        items.append(item)
        i += 1
        while i < n:
            l = lines[i]
            if not l.strip():
                j = i
                while j < n and not lines[j].strip():
                    j += 1
                mm = LI.match(lines[j]) if j < n else None
                if mm and len(mm.group(1)) >= base:
                    i = j
                    continue
                if j < n and indent_of(lines[j]) > base and not is_block_start(lines[j].strip()):
                    i = j
                    continue
                break
            mm = LI.match(l)
            if mm:
                if len(mm.group(1)) > base:
                    sub, i = parse_list(lines, i, len(mm.group(1)))
                    item["children"].append(sub)
                    continue
                break
            if is_block_start(l.strip()) and indent_of(l) <= base:
                break
            item["text"] += " " + l.strip()
            i += 1
    return {"t": "list", "ordered": ordered, "start": start, "items": items}, i

def parse_blocks(lines):
    blocks, i, n = [], 0, len(lines)
    while i < n:
        l = lines[i]
        if not l.strip():
            i += 1
            continue
        fm = FENCE.match(l)
        if fm:
            fence, lang, buf = fm.group(1), fm.group(2).lower(), []
            i += 1
            while i < n and not lines[i].strip().startswith(fence):
                buf.append(lines[i])
                i += 1
            blocks.append({"t": "code", "lang": lang, "text": "\n".join(buf)})
            i += 1
            continue
        hm = HEAD.match(l)
        if hm:
            blocks.append({"t": "h", "level": len(hm.group(1)), "text": hm.group(2)})
            i += 1
            continue
        mk = MARK.match(l)
        if mk:
            blocks.append({"t": "marker", "kind": mk.group(1).lower(), "arg": mk.group(2)})
            i += 1
            continue
        if COMMENT.match(l):
            blocks.append({"t": "comment", "text": l.strip()})
            i += 1
            continue
        if HR.match(l):
            blocks.append({"t": "hr"})
            i += 1
            continue
        if l.lstrip().startswith(">"):
            buf = []
            while i < n and lines[i].lstrip().startswith(">"):
                buf.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            kind = None
            am = re.match(r"^\s*\\?\[!(\w+)\\?\]\s*$", buf[0]) if buf else None
            if am:
                kind = {"note": "note", "info": "note", "important": "note", "tip": "good", "good": "good", "success": "good",
                        "warning": "warn", "warn": "warn", "caution": "danger", "danger": "danger"}.get(am.group(1).lower(), "note")
                buf = buf[1:]
            blocks.append({"t": "quote", "kind": kind, "blocks": parse_blocks(buf)})
            continue
        if l.lstrip().startswith("|") and i + 1 < n and TSEP.match(lines[i + 1]):
            head = split_row(l)
            align = []
            for c in split_row(lines[i + 1]):
                c = c.strip()
                align.append("center" if c.startswith(":") and c.endswith(":") else "right" if c.endswith(":") else None)
            rows, i = [], i + 2
            while i < n and lines[i].lstrip().startswith("|"):
                rows.append(split_row(lines[i]))
                i += 1
            blocks.append({"t": "table", "head": head, "align": align, "rows": rows})
            continue
        lm = LI.match(l)
        if lm:
            b, i = parse_list(lines, i, len(lm.group(1)))
            blocks.append(b)
            continue
        para = [l.strip()]
        i += 1
        while i < n and lines[i].strip() and not is_block_start(lines[i]):
            para.append(lines[i].strip())
            i += 1
        text = " ".join(para)
        im = re.fullmatch(r"!\[([^\]]*)\]\(([^)\s]+)(?:\s+\"([^\"]*)\")?\)", text)
        if im:
            blocks.append({"t": "img", "alt": im.group(1), "src": im.group(2), "title": im.group(3) or ""})
        else:
            blocks.append({"t": "p", "text": text})
    return blocks

def split_marker(arg):
    """'key · caption' (or 'key | caption') -> (key, caption)."""
    m = re.match(r"\s*(.*?)\s*(?:·|\|)\s*(.*)$", arg)
    return (m.group(1).strip(), m.group(2).strip()) if m else (arg.strip(), "")

def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")[:48].strip("-") or "section"

# ----------------------------------------------------------------------------- inline

class Inline:
    def __init__(self):
        self.anchor_map = {}

    def __call__(self, s, links=True):
        codes, escs = [], []
        def code_sub(m):
            codes.append(m.group(2))
            return f"\x00C{len(codes) - 1}\x00"
        s = re.sub(r"(`+)(.+?)\1", code_sub, s)
        def esc_sub(m):
            escs.append(m.group(1))
            return f"\x00E{len(escs) - 1}\x00"
        s = re.sub(r"\\([\\`*_{}\[\]()#+\-.!|<>~&:])", esc_sub, s)
        s = esc(s, quote=False)
        def img_sub(m):
            return f'<img src="{m.group(2).replace(chr(34), "&quot;")}" alt="{m.group(1)}" loading="lazy">'
        s = re.sub(r"!\[([^\]]*)\]\(([^)\s]+)\)", img_sub, s)
        def link_sub(m):
            text, href = m.group(1), m.group(2)
            if not links:
                return text
            if href.startswith("#"):
                href = "#" + self.anchor_map.get(href[1:], href[1:])
            ext = re.match(r"https?://", href)
            attrs = ' target="_blank" rel="noopener"' if ext else ""
            return f'<a href="{href.replace(chr(34), "&quot;")}"{attrs}>{text}</a>'
        s = re.sub(r"\[([^\]\n]+)\]\(([^)\s]+)(?:\s+&quot;[^&]*&quot;)?\)", link_sub, s)
        s = re.sub(r"&lt;(https?://[^\s&]+)&gt;", (lambda m: f'<a href="{m.group(1)}" target="_blank" rel="noopener">{m.group(1)}</a>') if links else r"\1", s)
        s = re.sub(r"\*\*(?=\S)(.+?)(?<=\S)\*\*", r"<strong>\1</strong>", s)
        s = re.sub(r"(?<![\w])__(?=\S)(.+?)(?<=\S)__(?![\w])", r"<strong>\1</strong>", s)
        s = re.sub(r"(?<![\w*])\*(?=[^\s*])([^*\n]+?)(?<=\S)\*(?![\w*])", r"<em>\1</em>", s)
        s = re.sub(r"(?<![\w])_(?=\S)([^_\n]+?)(?<=\S)_(?![\w])", r"<em>\1</em>", s)
        s = re.sub(r"~~(?=\S)(.+?)(?<=\S)~~", r"<del>\1</del>", s)
        s = re.sub("\x00E(\\d+)\x00", lambda m: esc(escs[int(m.group(1))], quote=False), s)
        s = re.sub("\x00C(\\d+)\x00", lambda m: "<code>" + esc(codes[int(m.group(1))].strip(), quote=False) + "</code>", s)
        return s

def strip_tags(h):
    return html.unescape(re.sub(r"<[^>]+>", "", h)).strip()

def first_sentence(text, limit=180):
    m = re.match(r"(.+?[.!?])(?=\s+[A-Z(\[`*]|\s*$)", text)
    s = m.group(1) if m else text
    if len(s) > limit:
        s = s[:limit].rsplit(" ", 1)[0].rstrip(",;:") + "…"
    return s

# ----------------------------------------------------------------------------- assets

def svg_size(path):
    try:
        head = open(path, "rb").read(4096).decode("utf-8", "ignore")
    except OSError:
        return None
    tag = re.search(r"<svg\b[^>]*>", head, re.S)
    if not tag:
        return None
    w = re.search(r'\bwidth="([\d.]+)(px)?"', tag.group(0))
    h = re.search(r'\bheight="([\d.]+)(px)?"', tag.group(0))
    if w and h:
        return round(float(w.group(1))), round(float(h.group(1)))
    vb = re.search(r'viewBox="[\d.\-]+[ ,]+[\d.\-]+[ ,]+([\d.]+)[ ,]+([\d.]+)"', tag.group(0))
    return (round(float(vb.group(1))), round(float(vb.group(2)))) if vb else None

def png_size(path):
    try:
        with open(path, "rb") as f:
            head = f.read(24)
        if head[:8] == b"\x89PNG\r\n\x1a\n":
            return struct.unpack(">II", head[16:24])
    except OSError:
        pass
    return None

def img_size(path):
    if not path:
        return None
    if path.lower().endswith(".svg"):
        return svg_size(path)
    if path.lower().endswith(".png"):
        return png_size(path)
    return None

class Assets:
    """Every file the page references: published path -> source path. Copies into the output folder at the end."""
    def __init__(self, out_dir, inline, copy):
        self.out_dir, self.inline, self.copy = out_dir, inline, copy
        self.files, self.missing, self.order = {}, [], []

    def ref(self, published, candidates, embed=False):
        """Record a reference; return the URL to write in the markup and the resolved source (or None)."""
        published = published.replace(os.sep, "/").lstrip("./") if not published.startswith("../") else published
        src = next((os.path.abspath(c) for c in candidates if c and os.path.isfile(c)), None)
        if published not in self.files:
            self.order.append(published)
        self.files[published] = src
        if src is None and published not in self.missing:
            self.missing.append(published)
        if embed and self.inline and src:
            mime = "image/svg+xml" if src.endswith(".svg") else (mimetypes.guess_type(src)[0] or "application/octet-stream")
            return "data:" + mime + ";base64," + base64.b64encode(open(src, "rb").read()).decode(), src
        return published, src

    def finish(self):
        out = {}
        for p in self.order:
            src = self.files[p]
            if src is None:
                continue
            dest = os.path.join(self.out_dir, p)
            if self.copy and os.path.abspath(dest) != src:
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                shutil.copy2(src, dest)
                out[p] = os.path.abspath(dest)
            else:
                out[p] = src
        return out

def published_for(path, prefix):
    """Published path for a referenced file: keep a clean relative path, else <prefix>/<basename>."""
    if os.path.isabs(path) or path.startswith(".."):
        return f"{prefix}/{os.path.basename(path)}"
    return path.replace(os.sep, "/")

# ----------------------------------------------------------------------------- page model

STATUS_WORDS = {"ruled": "ruled", "binding": "ruled", "accepted": "ruled", "merged": "built", "built": "built", "done": "built",
                "shipped": "built", "landed": "built", "live": "built", "draft": "proposed", "proposed": "proposed", "open": "proposed",
                "wip": "proposed", "planned": "proposed", "held": "held", "blocked": "held", "rejected": "held", "closed": "held",
                "default": "default", "pending": "default", "backlog": "default"}
CALLOUTS = [["where it stands", "good"], ["waiting on", "warn"], ["open", "warn"], ["blocked", "danger"], ["held", "danger"],
            ["refused", "note"], ["note", "note"], ["warning", "warn"], ["caution", "danger"], ["danger", "danger"], ["tip", "good"]]
LADDER = {0: "concept", 1: "doc", 2: "source", 3: "native render", 4: "native play", 5: "accepted"}
MODULE_GROUPS = ["game systems"]
MODULE_SECTIONS = ["architecture", "data model", "rendering", "content pipeline", "rollout"]
GENERIC_TAILS = [(r"\s+technical\s+spec(ification)?$", " Spec"), (r"\s+technical\s+design(\s+doc(ument)?)?$", " TDD"),
                 (r"\s+design\s+doc(ument)?$", " Design Doc"), (r"\s+system\s+design$", " Design")]

def page_title(h1):
    t = re.split(r"\s+[—–|:]\s+|:\s+", h1, maxsplit=1)[0].strip()
    if len(t.split()) > 4:
        for pat, rep in GENERIC_TAILS:
            t2 = re.sub(pat, rep, t, flags=re.I)
            if t2 != t:
                t = t2
                break
    words = t.split()
    return " ".join(words[:4]) if len(words) > 4 else t

def load_json(path, what):
    if not path:
        return None
    try:
        return json.load(open(path, encoding="utf-8"))
    except (OSError, ValueError) as e:
        sys.exit(f"{what} {path}: {e}")

class Builder:
    def __init__(self, a):
        self.a = a
        self.warn = []
        self.md_dir = os.path.dirname(os.path.abspath(a.src))
        self.out_dir = os.path.dirname(os.path.abspath(a.out))
        self.inline = Inline()
        self.assets = Assets(self.out_dir, a.inline, not a.no_copy)
        self.meta = load_json(a.meta, "--meta") or {}
        self.meta_dir = os.path.dirname(os.path.abspath(a.meta)) if a.meta else self.md_dir
        dmap = load_json(a.diagram_map, "--diagram-map") or {}
        self.dmap = [(k.lower(), v) for k, v in dmap.items()]
        self.vmap = load_json(a.video_map, "--video-map") or {}
        self.vmap_dir = os.path.dirname(os.path.abspath(a.video_map)) if a.video_map else self.md_dir
        self.media, self.media_dir, self.skipped = [], self.md_dir, []
        if a.media:
            raw = load_json(a.media, "--media")
            raw = raw.get("media", raw.get("entries", [])) if isinstance(raw, dict) else raw
            self.media_dir = os.path.dirname(os.path.abspath(a.media))
            for e in raw:
                if e.get("rights") == "licensed-reference":
                    self.skipped.append({"id": e.get("id"), "file": e.get("file"), "module": e.get("module"), "rights": "licensed-reference",
                                         "reason": "licensed-reference media inform the design and are never published"})
                else:
                    self.media.append(e)
        self.media_by_id = {e.get("id"): e for e in self.media}
        self.skipped_ids = {s["id"] for s in self.skipped}
        self.media_placed = set()
        self.diagram_dir = a.diagrams or next((d for d in (os.path.join(self.out_dir, "diagrams"), os.path.join(self.md_dir, "diagrams")) if os.path.isdir(d)), None)
        self.callouts = self.meta.get("callouts", CALLOUTS)
        if isinstance(self.callouts, dict):
            self.callouts = list(self.callouts.items())
        self.fig_no = 0
        self.ids = set()
        self.ladder = dict(LADDER)

    def uid(self, base):
        i, cand = 2, base
        while cand in self.ids:
            cand = f"{base}-{i}"
            i += 1
        self.ids.add(cand)
        return cand

    # -- structure
    def structure(self, text):
        text = text.replace("\r\n", "\n").replace("&#91;", "[").replace("&#93;", "]")
        blocks = parse_blocks(text.split("\n"))
        title, pre, sections, cur, sub = None, [], [], None, None
        for b in blocks:
            if b["t"] == "h" and b["level"] == 1 and title is None:
                title = b["text"]
                continue
            if b["t"] == "h" and b["level"] == 2:
                cur = {"title": b["text"], "blocks": [], "subs": []}
                sections.append(cur)
                sub = None
                continue
            if b["t"] == "h" and b["level"] == 3 and cur is not None:
                sub = {"title": b["text"], "blocks": []}
                cur["subs"].append(sub)
                continue
            (sub["blocks"] if sub else cur["blocks"] if cur else pre).append(b)
        # skip the source's own **Contents** block (label + list of in-doc links); harvest its anchors
        contents_titles = {}
        def harvest(lst):
            for it in lst["items"]:
                m = re.fullmatch(r"\[([^\]]+)\]\(#([^)]+)\)", it["text"].strip())
                if not m:
                    return False
                contents_titles[m.group(2)] = m.group(1)
                for ch in it["children"]:
                    if not harvest(ch):
                        return False
            return True
        def drop_contents(bl):
            out, i = [], 0
            while i < len(bl):
                b = bl[i]
                if b["t"] == "p" and re.fullmatch(r"\**\s*(Contents|Table of contents)\s*\**:?", b["text"].strip(), re.I) and i + 1 < len(bl) and bl[i + 1]["t"] == "list" and harvest(bl[i + 1]):
                    i += 2
                    continue
                if b["t"] == "list" and all(re.fullmatch(r"\[[^\]]+\]\(#[^)]+\)", it["text"].strip()) for it in b["items"]) and harvest(b) and i == 0 and not out:
                    i += 1
                    continue
                out.append(b)
                i += 1
            return out
        pre = drop_contents(pre)
        for s in sections:
            s["blocks"] = drop_contents(s["blocks"])
        # ids, numbers
        for n, s in enumerate(sections, 1):
            s["num"], s["id"] = n, self.uid(slug(strip_tags(self.inline(s["title"], links=False))))
            for sb in s["subs"]:
                sb["id"] = self.uid(s["id"] + "--" + slug(strip_tags(self.inline(sb["title"], links=False))))
        by_title = {}
        for s in sections:
            by_title.setdefault(strip_tags(self.inline(s["title"], links=False)).lower(), s["id"])
            for sb in s["subs"]:
                by_title.setdefault(strip_tags(self.inline(sb["title"], links=False)).lower(), sb["id"])
        for anchor, t in contents_titles.items():
            if t.lower() in by_title:
                self.inline.anchor_map[anchor] = by_title[t.lower()]
        # byline + lede from the preamble
        byline, lede = None, []
        for b in pre:
            if b["t"] == "p" and byline is None and not lede and len(b["text"]) <= 140 and ("·" in b["text"] or "@" in b["text"] or re.search(r"\b(19|20)\d\d\b", b["text"])):
                byline = b["text"]
            elif b["t"] == "p":
                lede.append(b["text"])
        # proof ladder labels defined by the doc itself ("L1 doc, L2 source-qualified, ...")
        for s in sections:
            for b in s["blocks"]:
                if b["t"] == "p" and re.search(r"proof ladder", b["text"], re.I):
                    for lv, name in re.findall(r"\bL([0-5])\s+([a-z][\w\- ]*?)(?=,|\.|;| and |$)", strip_tags(self.inline(b["text"], links=False))):
                        self.ladder[int(lv)] = name.strip()
        return title, byline, lede, sections

    # -- figures
    def diagram_key(self, kind, arg):
        if kind == "diagram":
            return split_marker(arg)
        low = arg.lower()
        key = next((v for k, v in self.dmap if k in low), None)
        if key is None and self.diagram_dir:
            cand = slug(re.split(r"\s+·\s+", arg)[0])
            if any(os.path.isfile(os.path.join(self.diagram_dir, cand + ext)) for ext in (".svg", ".png")):
                key = cand
        return key, arg

    def diagram_src(self, key):
        cands = []
        for d in (self.diagram_dir, os.path.join(self.out_dir, "diagrams"), os.path.join(self.md_dir, "diagrams")):
            if d:
                cands += [os.path.join(d, key + ".svg"), os.path.join(d, key + ".png")]
        src = next((c for c in cands if os.path.isfile(c)), None)
        ext = os.path.splitext(src)[1] if src else ".svg"
        return f"diagrams/{key}{ext}", [src] if src else []

    def caption_html(self, cap):
        parts = re.split(r"\s+·\s+", cap, maxsplit=1)
        if len(parts) == 2:
            return f'<span class="fig-title">{self.inline(parts[0])}</span> <span class="fig-text">{self.inline(parts[1])}</span>'
        return f'<span class="fig-text">{self.inline(cap)}</span>'

    def figure_diagram(self, key, cap, numbered=True, extra_class="", fig_id=None):
        published, cands = self.diagram_src(key)
        url, src = self.assets.ref(published, cands, embed=True)
        if not src:
            self.warn.append(f"diagram {key}: no {published} in {self.diagram_dir or 'any diagrams folder'}")
            return f'<p class="placeholder">Diagram {esc(key)} is missing: {self.inline(cap)}</p>'
        size = img_size(src)
        wh = f' width="{size[0]}" height="{size[1]}"' if size else ""
        fid = fig_id or self.uid(f"fig-{slug(key)}")
        no = ""
        if numbered:
            self.fig_no += 1
            no = f'<span class="fig-no">Fig. {self.fig_no}</span> '
        alt = esc(strip_tags(self.inline(cap, links=False)) or key)
        data_src = f' data-src="{published}"' if url != published else ""
        return (f'<figure class="diagram{extra_class}" id="{fid}" data-key="{esc(key)}">'
                f'<div class="plate" tabindex="0" role="region" aria-label="Diagram: {alt}"><img src="{url}"{data_src} alt="{alt}"{wh} loading="lazy" decoding="async"></div>'
                f'<figcaption><span class="cap">{no}{self.caption_html(cap)}</span>'
                f'<button type="button" class="zoom" aria-pressed="false">Zoom</button></figcaption></figure>')

    def video_paths(self, src, base_dirs):
        published = published_for(src, "video")
        cands = [os.path.join(self.out_dir, published)]
        if os.path.isabs(src):
            cands.insert(0, src)
        for d in base_dirs:
            if d:
                cands += [os.path.join(d, src), os.path.join(d, os.path.basename(src))]
        return published, cands

    def figure_video(self, src, poster, cap, fig_id, cls="video", base_dirs=()):
        dirs = [self.a.videos, *base_dirs, self.md_dir]
        published, cands = self.video_paths(src, dirs)
        url, vsrc = self.assets.ref(published, cands)
        if not vsrc:
            self.warn.append(f"video {published}: file not found (looked in {', '.join(d for d in dirs if d)})")
        elif os.path.getsize(vsrc) > FILE_LIMIT:
            self.warn.append(f"video {published}: {os.path.getsize(vsrc) / 1e6:.1f} MB is over the 15 MB per-file publish limit; use a smaller cut")
        pattr = ""
        if poster:
            pp, pc = self.video_paths(poster, dirs)
            purl, psrc = self.assets.ref(pp, pc, embed=True)
            if psrc:
                pattr = f' poster="{purl}"'
            else:
                self.warn.append(f"poster {pp}: file not found")
        mime = "video/webm" if published.endswith(".webm") else "video/mp4"
        return (f'<figure class="{cls}" id="{fig_id}"><div class="frame"><video controls preload="metadata" playsinline{pattr}>'
                f'<source src="{url}" type="{mime}"><p class="placeholder">This clip needs a browser that plays {mime[6:].upper()}.</p></video></div>'
                f'<figcaption><span class="cap">{self.caption_html(cap) if cap else ""}</span></figcaption></figure>')

    def video_marker(self, arg):
        name, cap = split_marker(arg)
        src = name if re.search(r"\.(mp4|webm|mov)$", name) else f"video/{name}.mp4"
        if "/" not in src:
            src = "video/" + src
        poster = re.sub(r"\.(mp4|webm|mov)$", ".png", src)
        dirs = [self.a.videos, self.md_dir, self.out_dir]
        has_poster = any(os.path.isfile(c) for c in self.video_paths(poster, dirs)[1])
        return self.figure_video(src, poster if has_poster else None, cap.strip(), self.uid("clip-" + slug(name)))

    def media_figure(self, e):
        kind = e.get("kind", "still")
        mid = self.uid("media-" + slug(str(e.get("id", "x"))))
        src = e.get("file", "")
        bits = [x for x in (e.get("source"), f"captured {e['captured']}" if e.get("captured") else None, e.get("rights")) if x]
        src_line = f'<span class="src">{self.inline(" · ".join(bits))}</span>' if bits else ""
        label = {"concept": "Concept art", "clip": "Proof clip", "still": "Still"}.get(kind, kind.title())
        cap = f'<span class="kind">{label}</span> <span class="cap">{self.inline(e.get("caption", ""))}</span>{src_line}'
        if kind == "clip":
            fig = self.figure_video(src, e.get("poster"), "", mid, cls="clip", base_dirs=(self.media_dir,))
            return fig.replace('<figcaption><span class="cap"></span></figcaption>', f"<figcaption>{cap}</figcaption>")
        published = published_for(src, "media")
        cands = [os.path.join(self.media_dir, src), os.path.join(self.out_dir, published), os.path.join(self.md_dir, src)]
        if os.path.isabs(src):
            cands.insert(0, src)
        url, s = self.assets.ref(published, cands, embed=True)
        if not s:
            self.warn.append(f"media {e.get('id')}: {src} not found")
        size = img_size(s)
        wh = f' width="{size[0]}" height="{size[1]}"' if size else ""
        alt = esc(strip_tags(self.inline(e.get("caption", ""), links=False)))
        cls = "concept" if kind == "concept" else "still"
        return f'<figure class="{cls}" id="{mid}"><div class="media-frame"><img src="{url}" alt="{alt}"{wh} loading="lazy" decoding="async"></div><figcaption>{cap}</figcaption></figure>'

    # -- blocks
    def callout_kind(self, text):
        m = re.match(r"^\*\*(.+?)\*\*[:.]?\s*(.*)$", text)
        if not m or not m.group(2).strip():
            return None
        lab = strip_tags(self.inline(m.group(1), links=False)).lower().rstrip(":. ")
        for prefix, kind in self.callouts:
            if lab.startswith(prefix.lower()):
                return kind
        return None

    def render_list(self, b):
        checks = all(it["check"] is not None for it in b["items"])
        tag = "ol" if b["ordered"] and not checks else "ul"
        attrs = ' class="checks"' if checks else ""
        if b["ordered"] and b["start"] != 1 and not checks:
            attrs += f' start="{b["start"]}"'
        out = [f"<{tag}{attrs}>"]
        for it in b["items"]:
            kids = "".join(self.render_list(c) for c in it["children"])
            if it["check"] is not None:
                state = "done" if it["check"] else "open"
                out.append(f'<li class="check" data-state="{state}"><span class="box" aria-hidden="true"></span><div><span class="sr-only">{"Done" if it["check"] else "Open"}: </span>{self.inline(it["text"])}{kids}</div></li>')
            else:
                out.append(f"<li>{self.inline(it['text'])}{kids}</li>")
        out.append(f"</{tag}>")
        return "".join(out)

    def render_table(self, b):
        ncol = max(len(b["head"]), *(len(r) for r in b["rows"])) if b["rows"] else len(b["head"])
        head = b["head"] + [""] * (ncol - len(b["head"]))
        rows = [r + [""] * (ncol - len(r)) for r in b["rows"]]
        status_col = [bool(re.fullmatch(r"(state|status)", strip_tags(self.inline(h, links=False)).lower())) for h in head]
        def numeric(c):
            t = strip_tags(self.inline(c, links=False))
            return bool(re.fullmatch(r"[~≈<>]?\s*[\d][\d.,%\s×x+\-–/:]*", t)) and len(t) < 24
        num_col = [all(numeric(r[j]) for r in rows if r[j].strip()) and any(r[j].strip() for r in rows) for j in range(ncol)]
        def cell(tag, c, j):
            cls = []
            if num_col[j]:
                cls.append("num")
            al = b["align"][j] if j < len(b["align"]) else None
            style = f' style="text-align:{al}"' if al and not num_col[j] else ""
            content = self.inline(c)
            if tag == "td" and status_col[j] and c.strip():
                w = strip_tags(self.inline(c, links=False)).lower()
                k = STATUS_WORDS.get(re.split(r"[\s,;(]+", w)[0])
                if k:
                    content = f'<span class="chip {k}">{content}</span>'
            return f'<{tag}{" class=" + chr(34) + " ".join(cls) + chr(34) if cls else ""}{style}>{content}</{tag}>'
        label = esc(", ".join(strip_tags(self.inline(h, links=False)) for h in head if h.strip())[:120])
        wide = ' data-wide=""' if ncol >= 4 else ""
        t = [f'<div class="tbl" role="region" tabindex="0" aria-label="Table: {label}"><table data-cols="{ncol}"{wide}><thead><tr>']
        t += [cell("th", h, j) for j, h in enumerate(head)]
        t.append("</tr></thead><tbody>")
        for r in rows:
            t.append("<tr>" + "".join(cell("td", c, j) for j, c in enumerate(r)) + "</tr>")
        t.append("</tbody></table></div>")
        return "".join(t)

    def render_block(self, b, ctx):
        t = b["t"]
        if t == "p":
            text = b["text"]
            if re.fullmatch(r"\*\*[^*]+\*\*", text.strip()):
                return f'<p class="lead-label">{self.inline(text)}</p>'
            k = self.callout_kind(text)
            if k:
                return f'<div class="callout {k}"><p>{self.inline(text)}</p></div>'
            return f"<p>{self.inline(text)}</p>"
        if t == "h":
            lvl = min(max(b["level"], 4), 6)
            return f"<h{lvl}>{self.inline(b['text'])}</h{lvl}>"
        if t == "list":
            return self.render_list(b)
        if t == "table":
            return self.render_table(b)
        if t == "code":
            if b["lang"] == "mermaid":
                return f'<pre class="mermaid">{esc(b["text"], quote=False)}</pre>'
            cls = f' class="language-{esc(b["lang"])}"' if b["lang"] else ""
            return f'<pre tabindex="0"><code{cls}>{esc(b["text"], quote=False)}</code></pre>'
        if t == "quote":
            inner = "".join(self.render_block(x, ctx) for x in b["blocks"])
            return f'<div class="callout {b["kind"]}">{inner}</div>' if b["kind"] else f"<blockquote>{inner}</blockquote>"
        if t == "hr":
            return "<hr>"
        if t == "comment":
            if "PR-CARDS" in b["text"]:
                ctx["pr_marker"] = True
                return self.pr_slot_inner()
            return b["text"] if re.fullmatch(r"<!--[^<>]*-->", b["text"]) else ""
        if t == "img":
            published = published_for(b["src"], "images")
            url, s = self.assets.ref(published, [os.path.join(self.md_dir, b["src"]), os.path.join(self.out_dir, published)] + ([b["src"]] if os.path.isabs(b["src"]) else []), embed=True)
            if not s and not re.match(r"https?:|data:", b["src"]):
                self.warn.append(f"image {b['src']}: not found")
            if re.match(r"https?:|data:", b["src"]):
                url = b["src"]
                self.assets.files.pop(published, None)
                self.assets.order = [p for p in self.assets.order if p != published]
                if published in self.assets.missing:
                    self.assets.missing.remove(published)
            size = img_size(s)
            wh = f' width="{size[0]}" height="{size[1]}"' if size else ""
            cap = b["title"] or b["alt"]
            return (f'<figure class="still"><div class="media-frame"><img src="{esc(url)}" alt="{esc(b["alt"])}"{wh} loading="lazy" decoding="async"></div>'
                    + (f'<figcaption><span class="cap">{self.inline(cap)}</span></figcaption>' if cap else "") + "</figure>")
        if t == "marker":
            kind, arg = b["kind"], b["arg"]
            if kind in ("embedded content", "diagram"):
                key, cap = self.diagram_key(kind, arg)
                if key:
                    return self.figure_diagram(key, cap)
                self.warn.append(f"unmapped diagram marker: {arg[:70]}")
                return f'<p class="placeholder">Diagram not mapped: {self.inline(arg)}</p>'
            if kind == "video":
                return self.video_marker(arg)
            if kind == "media":
                mid = arg.strip()
                if mid in self.media_by_id:
                    self.media_placed.add(mid)
                    return self.media_figure(self.media_by_id[mid])
                if mid in self.skipped_ids:
                    return ""
                self.warn.append(f"media marker {mid}: no such entry in --media")
                return f'<p class="placeholder">Media {esc(mid)} is not in media.json</p>'
            return f'<p class="placeholder">Image: {self.inline(arg)}</p>'
        return ""

    def is_diagram_marker(self, b):
        return b["t"] == "marker" and b["kind"] in ("embedded content", "diagram") and self.diagram_key(b["kind"], b["arg"])[0]

    def container(self, cid, blocks, ctx, sources=False):
        """One section intro or subsection: diagram first, then its clip, then its gallery, then prose (house order)."""
        blocks = list(blocks)
        lead = []
        di = next((i for i, b in enumerate(blocks) if self.is_diagram_marker(b)), None)
        if di is not None:
            lead.append(self.render_block(blocks.pop(di), ctx))
        ctx["first_fig"][cid] = re.search(r'<figure class="diagram[^"]*" id="([^"]+)"', lead[0]).group(1) if lead and "<figure" in lead[0] else None
        v = self.vmap.get(cid)
        if v:
            lead.append(self.figure_video(v["src"], v.get("poster"), v.get("caption", ""), self.uid("clip-" + cid), base_dirs=(self.vmap_dir,)))
            ctx["clip"][cid] = "clip-" + cid
        else:
            vi = next((i for i, b in enumerate(blocks) if b["t"] == "marker" and b["kind"] == "video"), None)
            if vi is not None:
                fig = self.render_block(blocks.pop(vi), ctx)
                lead.append(fig)
                m = re.search(r'id="([^"]+)"', fig)
                ctx["clip"][cid] = m.group(1) if m else None
        inline_ids = {b["arg"].strip() for b in blocks if b["t"] == "marker" and b["kind"] == "media"}
        entries = [e for e in self.media if e.get("module") == cid and e.get("id") not in inline_ids]
        if entries:
            figs = [self.media_figure(e) for e in entries]
            for e in entries:
                self.media_placed.add(e.get("id"))
            if cid not in ctx["clip"]:
                clip = next((e for e in entries if e.get("kind") == "clip"), None)
                if clip:
                    m = re.search(r'id="(media-[^"]+)"', figs[entries.index(clip)])
                    ctx["clip"][cid] = m.group(1) if m else None
            lead.append(f'<div class="gallery">{"".join(figs)}</div>')
        body, i = [], 0
        while i < len(blocks):
            b = blocks[i]
            if sources and b["t"] == "p":
                items = []
                while i < len(blocks) and blocks[i]["t"] == "p":
                    items.append(f"<li>{self.inline(blocks[i]['text'])}</li>")
                    i += 1
                body.append(f'<ul class="sources">{"".join(items)}</ul>')
                continue
            body.append(self.render_block(b, ctx))
            i += 1
        return "\n".join(lead + body)

    # -- PR cards
    def pr_slot_inner(self):
        self.pr_slot_used = True
        if not self.a.prs:
            return "<!-- PR-CARDS -->"
        return self.native_prs()

    def native_prs(self):
        spec = importlib.util.spec_from_file_location("build_prs", os.path.join(HERE, "build_prs.py"))
        bp = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(bp)
        prs = bp.load(self.a.prs)
        if not prs:
            sys.exit(f"--prs {self.a.prs}: no pr-NN.md files")
        nolink = lambda s: re.sub(r"</?a\b[^>]*>", "", bp.inline(s))
        groups = {}
        for p in prs:
            groups.setdefault(p["ms"], []).append(p)
        out = ['<div class="pr-cards">']
        for name, items in groups.items():
            note = next((p["ms_note"] for p in items if p["ms_note"]), "")
            chip = f'<span class="chip default">{esc(note)}</span>' if note else ""
            out.append(f'<div class="pr-ms"><div class="pr-ms-head"><h4>{esc(name)}</h4>{chip}<span class="pr-count">{len(items)} PR{"s" if len(items) != 1 else ""}</span></div><ol class="pr-list">')
            for p in items:
                out.append(f'<li><button type="button" class="pr-card" data-pr="{p["n"]}" aria-haspopup="dialog"><span class="pr-num">{p["n"]}</span>'
                           f'<span class="pr-main"><span class="pr-title">{esc(p["title"])}</span><span class="pr-change">{nolink(p["change"])}</span><span class="pr-open">Open the deep dive</span></span>'
                           f'<span class="pr-rb"><span class="pr-rb-label">Rollback</span>{nolink(p["rollback"])}</span></button></li>')
            out.append("</ol></div>")
        out.append("</div>")
        tpl = []
        for p in prs:
            body = bp.markdown(p["body"]).replace("<table>", '<div class="tbl"><table>').replace("</table>", "</table></div>")
            tpl.append(f'<template id="pr-tpl-{p["n"]}" data-title="PR {p["n"]} · {esc(p["title"])}">{body}</template>')
        self.pr_detail = ("\n".join(tpl) + '\n<dialog class="pr-dive" id="pr-dive" aria-labelledby="pr-dive-title"><div class="pr-dive-head">'
                          '<h2 id="pr-dive-title">PR</h2><button type="button" class="pr-dive-close">Close</button></div><div class="pr-dive-body"></div></dialog>')
        self.pr_count = len(prs)
        return "\n".join(out)

    # -- modules
    def modules(self, sections, ctx):
        mods = self.meta.get("modules")
        if isinstance(mods, str):
            mods = load_json(os.path.join(self.meta_dir, mods), "meta.modules")
        if mods is None and self.a.modules:
            mods = load_json(self.a.modules, "--modules")
        if mods is None and os.path.isfile(os.path.join(self.md_dir, "modules.json")):
            mods = load_json(os.path.join(self.md_dir, "modules.json"), "modules.json")
        if isinstance(mods, dict):
            mods = mods.get("modules", [])
        if mods is None:
            mods = []
            for s in sections:
                low = strip_tags(self.inline(s["title"], links=False)).lower()
                if any(low.startswith(g) for g in MODULE_GROUPS):
                    for sb in s["subs"]:
                        mods.append(self.derive_module(sb["id"], sb["title"], sb["blocks"]))
                elif any(low.startswith(g) for g in MODULE_SECTIONS):
                    allb = s["blocks"] + [b for sb in s["subs"] for b in sb["blocks"]]
                    mods.append(self.derive_module(s["id"], s["title"], s["blocks"] or allb, allb))
        cards = []
        for m in mods:
            mid = m.get("id", "")
            status = (m.get("status") or "default").lower()
            cls = status if status in ("ruled", "proposed", "built", "held", "default", "none") else STATUS_WORDS.get(status, "default")
            label = m.get("status_label") or m.get("status") or "no level"
            cls = "" if cls == "none" else " " + cls
            dk = m.get("diagram")
            own = ctx["first_fig"].get(mid) or next((v for k, v in ctx["first_fig"].items() if k.startswith(mid + "--") and v), None)
            diag = ("#" + dk.lstrip("#")) if dk and dk.startswith("#") else (f"#{own}" if not dk and own else (f"#{self.find_fig(dk)}" if dk and self.find_fig(dk) else None))
            ck = m.get("clip")
            clip = ("#" + ck.lstrip("#")) if ck and ck.startswith("#") else (f"#{ctx['clip'][mid]}" if ctx["clip"].get(mid) else (f"#clip-{slug(ck)}" if ck and f"clip-{slug(ck)}" in self.ids else None))
            links = []
            for lab, href in (("diagram", diag), ("clip", clip), ("section", f"#{mid}" if mid in self.ids else None)):
                links.append(f'<li><a href="{href}">{lab}</a></li>' if href else f'<li><span class="off">{lab}</span></li>')
            title = self.inline(m.get("title", mid))
            cards.append(f'<article class="module" id="mod-{esc(mid)}"><div class="module-top"><span class="chip{cls}">{esc(label)}</span>'
                         f'<h3 class="module-title">{title}</h3></div><p class="module-summary">{self.inline(m.get("summary", ""))}</p>'
                         f'<ul class="module-links" aria-label="Jump to">{"".join(links)}</ul></article>')
        return cards

    def find_fig(self, key):
        k = f"fig-{slug(key)}"
        return k if k in self.ids else None

    def derive_module(self, mid, title, blocks, status_blocks=None):
        first = next((b["text"] for b in blocks if b["t"] == "p" and not re.fullmatch(r"\*\*[^*]+\*\*", b["text"].strip())), "")
        run_in = re.match(r"^\*\*[^*]{1,40}\*\*[:.]?\s+(?=\S)", first)
        summary = first_sentence(strip_tags(self.inline(first[run_in.end():] if run_in else first, links=False)))
        status, label = "none", "no level"
        for b in (status_blocks or blocks):
            if b["t"] == "p" and re.match(r"^\*\*where it stands", b["text"], re.I):
                m = re.search(r"\bL([0-5])\b", b["text"])
                if m:
                    lv = int(m.group(1))
                    status = "proposed" if lv <= 1 else "ruled" if lv == 5 else "built"
                    label = f"L{lv} · {self.ladder.get(lv, '')}".rstrip(" ·")
                break
        return {"id": mid, "title": strip_tags(self.inline(title, links=False)), "summary": esc(summary, quote=False), "status": status, "status_label": label}

    # -- page
    def build(self):
        a = self.a
        src_text = open(a.src, encoding="utf-8").read()
        title, byline, lede_src, sections = self.structure(src_text)
        h1 = self.meta.get("h1") or title or os.path.splitext(os.path.basename(a.src))[0]
        tab_title = self.meta.get("title") or page_title(strip_tags(self.inline(h1, links=False)))
        ctx = {"first_fig": {}, "clip": {}, "pr_marker": False}
        self.pr_slot_used, self.pr_detail, self.pr_count = False, None, 0
        # sections
        sec_html, nav = [], []
        for s in sections:
            low = strip_tags(self.inline(s["title"], links=False)).lower()
            is_src = bool(re.fullmatch(r"(sources|references|bibliography)", low))
            parts = [self.container(s["id"], s["blocks"], ctx, sources=is_src)]
            subnav = []
            for sb in s["subs"]:
                parts.append(f'<h3 id="{sb["id"]}">{self.inline(sb["title"])}</h3>')
                parts.append(self.container(sb["id"], sb["blocks"], ctx, sources=is_src))
                subnav.append((sb["id"], strip_tags(self.inline(sb["title"], links=False))))
            s["html_parts"], s["subnav"], s["is_src"] = parts, subnav, is_src
        # PR slot: the source's marker, else the end of the rollout section (or meta.prs_section)
        if not ctx["pr_marker"]:
            target = self.meta.get("prs_section")
            sec = next((s for s in sections if s["id"] == target), None) if target else None
            sec = sec or next((s for s in sections if strip_tags(self.inline(s["title"], links=False)).lower().startswith("rollout")), None)
            if sec is None and a.prs:
                sec = next((s for s in reversed(sections) if not s["is_src"]), None)
            if sec is not None:
                hid = self.uid(sec["id"] + "--pr-deep-dives")
                heading = esc(self.meta.get("prs_heading", "PR deep dives"))
                sec["html_parts"].append(f'<div class="pr-slot"><h3 id="{hid}">{heading}</h3>\n{self.pr_slot_inner()}\n</div>')
                sec["subnav"].append((hid, self.meta.get("prs_heading", "PR deep dives")))
        for s in sections:
            cls = "doc-section sources-section" if s["is_src"] else "doc-section"
            sec_html.append(f'<section id="{s["id"]}" class="{cls}" aria-labelledby="{s["id"]}-h">\n<div class="sec-head"><span class="sec-no" aria-hidden="true">{s["num"]}</span>'
                            f'<h2 id="{s["id"]}-h">{self.inline(s["title"])}</h2></div>\n' + "\n".join(p for p in s["html_parts"] if p) + "\n</section>")
            nav.append((s["id"], s["num"], strip_tags(self.inline(s["title"], links=False)), s["subnav"]))
        # glance
        glance = self.glance(sections, ctx)
        mods = self.modules(sections, ctx)
        # header
        eyebrow = self.meta.get("eyebrow", "Technical design")
        lede = self.meta.get("lede")
        lede_html = f'<p class="lede">{self.inline(lede)}</p>' if lede else "".join(f'<p class="lede">{self.inline(x)}</p>' for x in lede_src)
        by = self.meta.get("byline", byline)
        links = self.meta.get("links", [])
        link_html = "".join(f'<li><a href="{esc(l["href"])}"{" target=" + chr(34) + "_blank" + chr(34) + " rel=" + chr(34) + "noopener" + chr(34) if re.match("https?://", l["href"]) else ""}>{esc(l["label"])}</a></li>' for l in links)
        prose = re.sub(r"\]\([^)]*\)|https?://\S+|```.*?```", " ", src_text, flags=re.S)
        words = len(re.findall(r"[A-Za-z0-9'’]+", re.sub(r"[`*_#|\[\]()>-]", " ", prose)))
        facts = self.meta.get("facts")
        if facts is None:
            facts = [{"label": "Sections", "value": str(len(sections))}, {"label": "Diagrams", "value": str(self.fig_no)}]
            if self.pr_count:
                facts.append({"label": "PRs", "value": str(self.pr_count)})
            facts.append({"label": "Reading", "value": f"{max(1, round(words / 230))} min"})
            dm = re.search(r"\b([A-Z][a-z]{2,8}\.? \d{1,2}, \d{4}|\d{4}-\d{2}-\d{2})\b", by or "")
            if dm:
                facts.append({"label": "Updated", "value": dm.group(1)})
        facts_html = "".join(f'<div><dt>{esc(f["label"])}</dt><dd>{esc(str(f["value"]))}</dd></div>' for f in facts)
        header = (f'<header class="doc-header"><p class="eyebrow">{self.inline(eyebrow)}</p><h1>{self.inline(h1)}</h1>{lede_html}'
                  + (f'<div class="byline">{"<span>" + self.inline(by) + "</span>" if by else ""}{"<ul class=" + chr(34) + "links" + chr(34) + ">" + link_html + "</ul>" if link_html else ""}</div>' if by or link_html else "")
                  + (f'<dl class="meta">{facts_html}</dl>' if facts_html else "") + "</header>")
        navlist = self.nav_html(nav)
        sidenav = f'<nav class="sidenav" aria-label="Contents"><p class="nav-label">Contents</p>{navlist}</nav>'
        topnav = f'<details class="topnav"><summary><span>Contents</span><span class="count">{len(sections)} sections</span></summary><nav aria-label="Contents">{navlist}</nav></details>'
        mod_html = ""
        if mods:
            mt = esc(self.meta.get("modules_title", "Modules"))
            ml = self.meta.get("modules_lede")
            mod_html = (f'<div class="modules-block" aria-labelledby="modules-title"><div class="block-head"><h2 id="modules-title">{mt}</h2>'
                        + (f"<p>{self.inline(ml)}</p>" if ml else "") + f'</div><div class="modules">{"".join(mods)}</div></div>')
        detail = self.pr_detail if self.pr_detail else "<!-- PR-DETAIL:START -->\n<!-- PR-DETAIL:END -->"
        # media never placed
        for e in self.media:
            if e.get("id") not in self.media_placed:
                self.warn.append(f"media {e.get('id')}: module {e.get('module')!r} matches no section or subsection id; not rendered")
        # style
        css = open(a.css, encoding="utf-8").read() if a.css else open(DEFAULT_CSS, encoding="utf-8").read()
        notes = []
        if a.project_tokens:
            house = json.load(open(DEFAULT_TOKENS, encoding="utf-8"))
            pcss, imports, notes = project_token_css(a.project_tokens, house)
            css = "\n".join(imports) + ("\n" if imports else "") + css.rstrip() + "\n" + pcss
        self.warn += notes
        markup = (f'<div class="doc" lang="en">\n{sidenav}\n<main class="doc-main" id="top">\n{header}\n{topnav}\n{glance}\n{mod_html}\n'
                  + "\n".join(sec_html) + f"\n</main>\n{detail}\n</div>")
        page = (f"<title>{esc(tab_title)}</title>\n<meta name=\"claude:viewport-fit-sides\" content=\"cover\">\n<style>\n{css.rstrip()}\n</style>\n"
                f"{markup}\n<script>\n{SCRIPT.strip()}\n</script>\n")
        files = self.assets.finish()
        os.makedirs(self.out_dir, exist_ok=True)
        open(a.out, "w", encoding="utf-8").write(page)
        big = [p for p, s in files.items() if os.path.getsize(s) > FILE_LIMIT]
        manifest = {
            "page": os.path.basename(a.out), "root": self.out_dir, "title": tab_title, "inline": bool(a.inline),
            "files": {p: s for p, s in files.items() if not (a.inline and re.search(r"\.(svg|png|jpe?g|webp|gif)$", p))},
            "origin": {p: self.assets.files[p] for p in files}, "missing": self.assets.missing, "skipped": self.skipped,
            "over_15mb": big, "pr_cards": "native" if self.pr_detail else ("pending: run build_prs.py <page> <prs-dir>" if self.pr_slot_used else "none"),
            "warnings": self.warn,
        }
        mpath = os.path.splitext(a.out)[0] + ".files.json"
        json.dump(manifest, open(mpath, "w", encoding="utf-8"), indent=2)
        for w in self.warn:
            print("warning:", w, file=sys.stderr)
        print(f"wrote {a.out} ({len(page) / 1e3:.0f} kB): {len(sections)} sections, {self.fig_no} diagrams, {len(mods)} module cards, "
              f"{len(files)} files ({len(self.assets.missing)} missing, {len(self.skipped)} skipped); manifest {mpath}")
        if len(page.encode()) > 16 * 1024 * 1024:
            sys.exit("page is over the 16 MB artifact limit; drop --inline")
        if a.strict and self.warn:
            sys.exit(f"--strict: {len(self.warn)} warning(s)")

    def glance(self, sections, ctx):
        g = self.meta.get("glance", {}) or {}
        key = g.get("diagram")
        if not key and self.diagram_dir:
            key = next((k for k in ("system-overview", "overview", "system-map", "architecture")
                        if any(os.path.isfile(os.path.join(self.diagram_dir, k + e)) for e in (".svg", ".png"))), None)
        top = self.vmap.get("_top")
        if not key and not top:
            return ""
        cap = g.get("caption") or (key.replace("-", " ").capitalize() if key else "")
        parts = []
        if key:
            parts.append(self.figure_diagram(key, cap, numbered=False, extra_class=" glance-fig", fig_id=self.uid("fig-glance")))
        if top:
            parts.append(self.figure_video(top["src"], top.get("poster"), top.get("caption", ""), self.uid("clip-top"), cls="video glance-clip", base_dirs=(self.vmap_dir,)))
        title = esc(g.get("title", "System at a glance"))
        lede = f'<p>{self.inline(g["lede"])}</p>' if g.get("lede") else ""
        solo = "" if (key and top) else " solo"
        return f'<div class="glance{solo}" aria-labelledby="glance-title"><div class="block-head"><h2 id="glance-title">{title}</h2>{lede}</div>{"".join(parts)}</div>'

    def nav_html(self, nav):
        out = ['<ol class="nav-list">']
        for sid, num, label, subs in nav:
            sub = ""
            if subs:
                sub = '<ol class="nav-sub">' + "".join(f'<li><a href="#{i}">{esc(t)}</a></li>' for i, t in subs) + "</ol>"
            out.append(f'<li class="nav-top"><a href="#{sid}"><span class="nav-no">{num}</span><span class="nav-text">{esc(label)}</span></a>{sub}</li>')
        out.append("</ol>")
        return "".join(out)

SCRIPT = r"""
(function () {
  "use strict";
  var side = document.querySelector(".sidenav");
  var links = [].slice.call(document.querySelectorAll(".sidenav a[href^='#'], .topnav a[href^='#']"));
  var byId = {}, targets = [], current = null, ticking = false;
  links.forEach(function (a) {
    var id = decodeURIComponent(a.getAttribute("href").slice(1)), el = document.getElementById(id);
    if (!el) return;
    // never hide entries on load: the artifact viewer may measure the page while it is not yet visible
    if (!byId[id]) { byId[id] = []; targets.push(el); }
    byId[id].push(a);
  });
  targets.sort(function (x, y) { return x.compareDocumentPosition(y) & 4 ? -1 : 1; });
  function setActive(id) {
    if (id === current) return;
    current = id;
    links.forEach(function (a) { a.classList.remove("active"); a.removeAttribute("aria-current"); });
    [].forEach.call(document.querySelectorAll(".nav-top.current"), function (li) { li.classList.remove("current"); });
    (byId[id] || []).forEach(function (a) {
      a.classList.add("active"); a.setAttribute("aria-current", "location");
      var top = a.closest(".nav-top"); if (top) top.classList.add("current");
    });
    var act = side && side.querySelector("a.active");
    if (act && side.scrollHeight > side.clientHeight + 4) {
      var r = act.getBoundingClientRect(), n = side.getBoundingClientRect();
      if (r.top < n.top + 56 || r.bottom > n.bottom - 56) side.scrollTop += r.top - n.top - side.clientHeight / 3;
    }
  }
  function pick() {
    ticking = false;
    if (!targets.length) return;
    var line = Math.min(160, window.innerHeight * 0.3), best = targets[0], i;
    for (i = 0; i < targets.length; i++) if (targets[i].getBoundingClientRect().top <= line) best = targets[i];
    var se = document.scrollingElement || document.documentElement;
    if (window.innerHeight + window.scrollY >= se.scrollHeight - 2) {
      for (i = targets.length - 1; i >= 0; i--) if (targets[i].getBoundingClientRect().top < window.innerHeight * 0.85) { best = targets[i]; break; }
    }
    setActive(best.id);
  }
  function soon() { if (!ticking) { ticking = true; requestAnimationFrame(pick); } }
  window.addEventListener("scroll", soon, { passive: true });
  window.addEventListener("resize", soon);
  window.addEventListener("hashchange", soon);
  pick();
  [].forEach.call(document.querySelectorAll(".topnav a"), function (a) {
    a.addEventListener("click", function () { var d = a.closest("details"); if (d) d.open = false; });
  });
  [].forEach.call(document.querySelectorAll("figure.diagram .zoom"), function (b) {
    b.addEventListener("click", function () {
      var fig = b.closest("figure"), on = !fig.classList.contains("zoomed");
      fig.classList.toggle("zoomed", on);
      b.setAttribute("aria-pressed", on ? "true" : "false");
      b.textContent = on ? "Fit" : "Zoom";
    });
  });
  var dlg = document.getElementById("pr-dive");
  if (dlg && typeof dlg.showModal === "function") {
    var body = dlg.querySelector(".pr-dive-body"), title = dlg.querySelector("#pr-dive-title"), opener = null;
    var openPr = function (n, from) {
      var t = document.getElementById("pr-tpl-" + n);
      if (!t) return;
      body.innerHTML = t.innerHTML; title.textContent = t.getAttribute("data-title");
      opener = from || null; dlg.showModal(); body.scrollTop = 0;
    };
    [].forEach.call(document.querySelectorAll(".pr-card"), function (c) {
      c.addEventListener("click", function () { openPr(c.getAttribute("data-pr"), c); });
    });
    dlg.querySelector(".pr-dive-close").addEventListener("click", function () { dlg.close(); });
    dlg.addEventListener("click", function (e) { if (e.target === dlg) dlg.close(); });
    dlg.addEventListener("close", function () { if (opener) opener.focus(); });
    var m = location.hash.match(/^#pr-(\d+)$/); if (m) openPr(m[1]);
  }
})();
"""

# ----------------------------------------------------------------------------- CLI

class Args:
    pass

def parse_args(argv):
    a = Args()
    flags = {"--diagrams": "diagrams", "--diagram-map": "diagram_map", "--video-map": "video_map", "--videos": "videos", "--media": "media",
             "--modules": "modules", "--prs": "prs", "--css": "css", "--project-tokens": "project_tokens", "--meta": "meta"}
    bools = {"--inline": "inline", "--no-copy": "no_copy", "--strict": "strict"}
    for v in list(flags.values()) + list(bools.values()):
        setattr(a, v, None if v in flags.values() else False)
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
            sys.exit(f"unknown option {x}\n\n{__doc__}")
        else:
            pos.append(x)
            i += 1
    if len(pos) != 2:
        sys.exit(__doc__)
    a.src, a.out = pos
    for name in ("src", "css", "diagram_map", "video_map", "media", "modules", "project_tokens", "meta"):
        p = getattr(a, name)
        if p and not os.path.isfile(p):
            sys.exit(f"{name.replace('_', '-')}: no such file {p}")
    for name in ("diagrams", "prs", "videos"):
        p = getattr(a, name)
        if p and not os.path.isdir(p):
            sys.exit(f"--{name}: no such directory {p}")
    return a

if __name__ == "__main__":
    argv = sys.argv[1:]
    if argv and argv[0] in ("--sync-css", "--check-css"):
        rest = argv[1:]
        sync_css(rest[0] if rest else DEFAULT_TOKENS, rest[1] if len(rest) > 1 else DEFAULT_CSS, check=argv[0] == "--check-css")
        sys.exit(0)
    Builder(parse_args(argv)).build()
