"""Write packet.json, the manifest /system-design-video reads. Re-runnable; reads what is on disk, invents nothing. Exit 0 on success; 1 with a message on bad arguments or a packet without tdd.html.
Usage: write_packet.py <packet-dir> --slug <slug> --title "<title>"
Layout (see SKILL.md): tdd.html, product-design.html (or <persona>.html), diagrams/<key>.mmd + <key>.svg, status.md, prs/pr-NN.md.
Sections come from the TDD's <section id="..."> blocks, in document order, each with its heading and the diagram keys it embeds."""
import json, os, re, sys

def arg(name, default=None):
    a = sys.argv
    return a[a.index(name) + 1] if name in a else default

pos = [x for i, x in enumerate(sys.argv[1:], 1) if not x.startswith("--") and sys.argv[i - 1] not in ("--slug", "--title")]
if len(pos) != 1:
    sys.exit(__doc__)
root = os.path.abspath(pos[0])
if not os.path.isdir(root):
    sys.exit(f"{root}: not a directory")
rel = lambda *p: os.path.join(*p)
exists = lambda p: os.path.exists(os.path.join(root, p))

PERSONA = {"tdd.html": "engineering", "product-design.html": "product-design"}
personas = {}
for f in sorted(os.listdir(root)):
    if f.endswith(".html") and not f.endswith(".src.html"):  # *.src.html is build_page.py input, not a page
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
    "diagrams": diagrams,
    "sections": sections,
    "prs": [rel("prs", f) for f in prs if re.fullmatch(r"pr-\d+\.md", f)],
}
with open(os.path.join(root, "packet.json"), "w", encoding="utf-8") as fh:
    json.dump(manifest, fh, indent=2)
print(f"packet.json: {len(personas)} pages, {len(diagrams)} diagrams, {len(sections)} sections, {len(manifest['prs'])} PRs")
