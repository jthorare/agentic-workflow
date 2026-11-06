"""Write packet.json, the manifest /system-design-video reads. Re-runnable; reads what is on disk, invents nothing. Exit 0 on success; 1 with a message on bad arguments or a packet without tdd.html.
Usage: write_packet.py <packet-dir> --slug <slug> --title "<title>" [--repo owner/name]
Layout (see SKILL.md): tdd.html, product-design.html (or <persona>.html), tdd-share.html + deck.html + share/story.json (build_share.py),
diagrams/<key>.mmd + <key>.svg, status.md, prs/pr-NN.md.
Also writes doc.md next to packet.json: the design-doc convention (docs/conventions/design-docs.md), generated from packet.json (front matter generated + source-sha256),
with sections, a Ph<n> per PR card, a REQ-<n> per EARS criterion in tdd.html (an li/p/td/th that starts with When/While/Where/If/The and holds "shall") and a TP-<n> per list item or table row under a card's ## Tests / ## Test plan.
Sections come from the TDD's <section id="..."> blocks, in document order, each with its heading and the diagram keys it embeds."""
import hashlib, json, os, re, sys

def arg(name, default=None):
    a = sys.argv
    return a[a.index(name) + 1] if name in a else default

pos = [x for i, x in enumerate(sys.argv[1:], 1) if not x.startswith("--") and sys.argv[i - 1] not in ("--slug", "--title", "--repo")]
if len(pos) != 1:
    sys.exit(__doc__)
root = os.path.abspath(pos[0])
if not os.path.isdir(root):
    sys.exit(f"{root}: not a directory")
rel = lambda *p: os.path.join(*p)
exists = lambda p: os.path.exists(os.path.join(root, p))

PERSONA = {"tdd.html": "engineering", "product-design.html": "product-design"}
SHARE = {"tdd-share.html": "share", "deck.html": "deck"}  # build_share.py output: top-level keys, not personas (no film of a deck)
personas = {}
for f in sorted(os.listdir(root)):
    if f.endswith(".html") and not f.endswith(".src.html") and f not in SHARE:  # *.src.html is build_page.py input, not a page
        personas[PERSONA.get(f, f[:-5])] = {"page": f}
if "tdd.html" not in {v["page"] for v in personas.values()}:
    sys.exit("packet has no tdd.html")

diagrams = {}
dd = os.path.join(root, "diagrams")
for f in sorted(os.listdir(dd)) if os.path.isdir(dd) else []:
    if f.endswith(".mmd"):
        key = f[:-4]
        diagrams[key] = {"mmd": rel("diagrams", f), "svg": rel("diagrams", key + ".svg") if exists(rel("diagrams", key + ".svg")) else None}

tdd = open(os.path.join(root, "tdd.html"), encoding="utf-8").read()
sections = []
for m in re.finditer(r'<section\b[^>]*\bid="([^"]+)"[^>]*>(.*?)</section>', tdd, re.S):
    h = re.search(r"<h2[^>]*>(.*?)</h2>", m.group(2), re.S)
    title = re.sub(r"<[^>]+>", "", h.group(1)).strip() if h else m.group(1)
    keys = [k for k in re.findall(r'diagrams/([\w.-]+?)\.svg', m.group(2)) if k in diagrams]
    sections.append({"id": m.group(1), "title": title, "anchor": "#" + m.group(1), "diagrams": list(dict.fromkeys(keys))})

prs = sorted((f for f in os.listdir(os.path.join(root, "prs"))), key=lambda f: [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", f)]) if os.path.isdir(os.path.join(root, "prs")) else []
manifest = {
    "slug": arg("--slug", os.path.basename(root)),
    "title": arg("--title", os.path.basename(root)),
    "personas": personas,
    "status": "status.md" if exists("status.md") else None,
    "share": "tdd-share.html" if exists("tdd-share.html") else None,
    "deck": "deck.html" if exists("deck.html") else None,
    "story": rel("share", "story.json") if exists(rel("share", "story.json")) else None,
    "diagrams": diagrams,
    "sections": sections,
    "prs": [rel("prs", f) for f in prs if re.fullmatch(r"pr-\d+\.md", f)],
}
with open(os.path.join(root, "packet.json"), "w", encoding="utf-8") as fh:
    json.dump(manifest, fh, indent=2)
print(f"packet.json: {len(personas)} pages, {len(diagrams)} diagrams, {len(sections)} sections, {len(manifest['prs'])} PRs")


# ---- doc.md: the design-doc convention, generated from packet.json (docs/conventions/design-docs.md) ----
def doc_id(slug):
    s = re.sub(r"-+", "-", re.sub(r"[^a-z0-9-]", "-", slug.lower())).strip("-")
    if len(s) > 64:
        cut = s[:65].rfind("-")
        s = (s[:cut] if cut >= 3 else s[:64]).strip("-")
    return s

def one_line(t):
    return re.sub(r"\s+", " ", t).strip()

def cell(t):
    return one_line(t).replace("|", "\\|")

def card(path):
    md = open(os.path.join(root, path), encoding="utf-8").read()
    title = None
    m = re.match(r"\A---\n(.*?)\n---\n", md, re.S)
    if m:
        t = re.search(r"^title:\s*(.+?)\s*$", m.group(1), re.M)
        if t:
            title = re.sub(r"\s+#.*$", "", t.group(1)).strip("\"'")
        md = md[m.end():]
    if not title:
        h = re.search(r"^#\s+(?:PR\s*\d+\s*:\s*)?(.+?)\s*$", md, re.M)
        title = h.group(1) if h else os.path.basename(path)[:-3]
    rows, intests, infence, intable = [], False, False, False
    for ln in md.splitlines():
        if ln.startswith("```"):
            infence = not infence
            continue
        if infence:
            continue
        h = re.match(r"#{2,6}\s+(.*)", ln)
        if h:
            intests = bool(re.fullmatch(r"(tests?|test plan)", h.group(1).strip(), re.I))
            continue
        if not intests:
            continue
        li = re.match(r"\s*(?:[-*]|\d+\.)\s+(.*)", ln)
        if li:
            rows.append(li.group(1))
        elif ln.startswith("|"):
            if not intable:  # the header row of a pipe table is not a test row
                intable = True
            elif not re.match(r"\|[\s:|-]+\|?\s*$", ln):
                rows.append(" - ".join(c.strip() for c in ln.strip().strip("|").split("|") if c.strip()))
            continue
        intable = False
    return one_line(title), [one_line(r) for r in rows if one_line(r)]

ears = []
for m in re.finditer(r"<(li|p|td|th)\b[^>]*>(.*?)</\1>", tdd, re.S):
    t = one_line(re.sub(r"<[^>]+>", "", m.group(2)))
    t = t.replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">").replace("&quot;", '"').replace("&#39;", "'")
    if re.match(r"(When|While|Where|If|The)\b", t) and re.search(r"\bshall\b", t) and t not in ears:
        ears.append(t)

slug_id = doc_id(manifest["slug"])
if len(slug_id) < 3:
    sys.exit(f"slug {manifest['slug']!r} gives the doc id {slug_id!r}, under 3 characters; pass a longer --slug")
repo = arg("--repo")
if repo is not None and not re.fullmatch(r"[\w.-]+/[\w.-]+", repo):
    sys.exit(f"--repo {repo!r}: expected owner/name")
sha = hashlib.sha256(open(os.path.join(root, "packet.json"), "rb").read()).hexdigest()
out = ["---", "aw-doc: 1", f"id: {slug_id}", "kind: tdd", f"title: {json.dumps(manifest['title'])}", "status: draft",
       "generated: write_packet.py", f"source-sha256: {sha}"]
if repo:
    out.append(f"repos: [{repo}]")
out += ["---", "", f"# {one_line(manifest['title'])}", "",
        "Generated from packet.json by write_packet.py; edit the packet, not this file.", ""]
for sec in sections:
    out += [f"## {one_line(sec['title'])}", "", f"See [tdd.html#{sec['id']}](tdd.html#{sec['id']}).", ""]
cards = []
for f in manifest["prs"]:
    n = int(re.search(r"pr-(\d+)\.md", f).group(1))
    cards.append((n,) + card(f))
if cards:
    out += ["## Phases", ""]
    for n, title, _ in cards:
        out += [f"### Ph{n} {title}", ""]
if ears:
    out += ["## Requirements", ""] + [f"- **REQ-{i}** {t}" for i, t in enumerate(ears, 1)] + [""]
tp = [(n, r) for n, _, rows in cards for r in rows]
if tp:
    out += ["## Test plan", "", "| Row | Check | Verifies |", "|---|---|---|"]
    out += [f"| TP-{i} | {cell(r)} | Ph{n} |" for i, (n, r) in enumerate(tp, 1)] + [""]
with open(os.path.join(root, "doc.md"), "w", encoding="utf-8") as fh:
    fh.write("\n".join(out).rstrip("\n") + "\n")
print(f"doc.md: id {slug_id}, {len(cards)} phases, {len(ears)} requirements, {len(tp)} test rows")
