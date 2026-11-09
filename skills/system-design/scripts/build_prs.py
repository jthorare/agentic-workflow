"""Turn prs/pr-NN.md into milestone-grouped PR cards plus one shared deep-dive <dialog>. Re-runnable.
Usage: build_prs.py <page.html> <prs-dir> [--shots shots.json]
The page marks the card spot with <!-- PR-CARDS --> (or the PR-CARDS:START/END pair an earlier run left). The dialog, the
per-PR deep dives (markdown rendered here, so the page needs no script from a CDN) and the styles are written between
PR-DETAIL:START/END before </body>. Front matter and file shape: templates/pr-cards.md.
Exit 0 on success; 1 with a message on bad input (no pr-NN.md, no placeholder, unmatched markers, bad arguments)."""
import html, json, os, re, sys

CSS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "templates", "pr-cards.css")
C0, C1 = "<!-- PR-CARDS:START -->", "<!-- PR-CARDS:END -->"
D0, D1 = "<!-- PR-DETAIL:START -->", "<!-- PR-DETAIL:END -->"
BLOCK = r"(#{1,6} |```|\||\s*([-*]|\d+\.) )"
esc = html.escape

def front(md):
    m = re.match(r"\A---\n(.*?)\n---\n", md, re.S)
    meta = {}
    if m:
        for line in m.group(1).splitlines():
            k, _, v = line.partition(":")
            if v.strip():
                meta[k.strip()] = re.sub(r"\s+#.*$", "", v.strip()).strip("\"'")
        md = md[m.end():]
    return meta, md

def inline(s):
    s = esc(s, quote=False)
    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    s = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", s)
    s = re.sub(r"(?<![\w*])\*([^*\n]+)\*(?![\w*])", r"<i>\1</i>", s)
    return re.sub(r"\[([^\]]+)\]\((https?://[^)\s]+)\)", r'<a href="\2">\1</a>', s)

def split_row(r):
    return [c.strip() for c in r.strip().strip("|").split("|")]

def markdown(md):
    """Headings, tables, lists, code fences, paragraphs, inline code/bold/links: the subset a PR deep dive uses."""
    out, lines, i = [], md.splitlines(), 0
    while i < len(lines):
        ln = lines[i]
        if ln.startswith("```"):
            j = i + 1
            while j < len(lines) and not lines[j].startswith("```"):
                j += 1
            out.append("<pre><code>" + esc("\n".join(lines[i + 1:j])) + "</code></pre>")
            i = j + 1
        elif re.match(r"#{1,6} ", ln):
            n = len(ln) - len(ln.lstrip("#"))
            out.append(f"<h{n}>{inline(ln[n:].strip())}</h{n}>")
            i += 1
        elif ln.startswith("|") and i + 1 < len(lines) and re.match(r"\|[\s:|-]+\|?\s*$", lines[i + 1]):
            head, j, rows = split_row(ln), i + 2, []
            while j < len(lines) and lines[j].startswith("|"):
                rows.append(split_row(lines[j]))
                j += 1
            out.append("<table><thead><tr>" + "".join(f"<th>{inline(c)}</th>" for c in head) + "</tr></thead><tbody>"
                       + "".join("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>" for r in rows) + "</tbody></table>")
            i = j
        elif re.match(r"\s*([-*]|\d+\.) ", ln):
            tag = "ol" if re.match(r"\s*\d+\.", ln) else "ul"
            items = []
            while i < len(lines) and re.match(r"\s*([-*]|\d+\.) ", lines[i]):
                items.append(re.sub(r"^\s*([-*]|\d+\.) ", "", lines[i]))
                i += 1
            out.append(f"<{tag}>" + "".join(f"<li>{inline(x)}</li>" for x in items) + f"</{tag}>")
        elif ln.strip():
            para = [ln]
            i += 1
            while i < len(lines) and lines[i].strip() and not re.match(BLOCK, lines[i]):
                para.append(lines[i])
                i += 1
            out.append("<p>" + inline(" ".join(para)) + "</p>")
        else:
            i += 1
    return "\n".join(out)

def first_sentence(s):
    m = re.match(r".*?[.!?](?=\s|$)", s)
    return (m.group(0) if m else s)[:240]

def load(prs_dir):
    prs = []
    if not os.path.isdir(prs_dir):
        sys.exit(f"{prs_dir}: not a directory")
    for f in sorted(os.listdir(prs_dir)):
        m = re.fullmatch(r"pr-(\d+)\.md", f)
        if not m:
            continue
        raw = open(os.path.join(prs_dir, f), encoding="utf-8").read()
        meta, body = front(raw)
        n = int(m.group(1))
        h1 = re.search(r"^# (.+)$", body, re.M)
        title = meta.get("title") or (re.sub(r"^PR\s*\d+\s*[:.\-·]?\s*", "", h1.group(1)) if h1 else f"PR {n}")
        sm = re.search(r"^## Summary\s*\n(.*?)(?=^## |\Z)", body, re.S | re.M)
        summ = " ".join(sm.group(1).split()) if sm else ""
        rb = re.search(r"Rollback:\s*(.+?)(?:\.\s|\.?$)", summ)
        ms = meta.get("milestone")
        if not ms:
            t = re.search(r"milestone-(\w+)", raw)
            ms = f"Milestone {t.group(1)}" if t else "PRs"
        prs.append(dict(n=n, title=title, ms=ms, ms_note=meta.get("milestone_note", ""), body=re.sub(r"\A# .*\n", "", body.lstrip()),
                        change=meta.get("change") or first_sentence(summ), rollback=meta.get("rollback") or (rb.group(1) if rb else "not stated")))
    return sorted(prs, key=lambda p: p["n"])  # numeric: pr-2 before pr-10

def build(page, prs_dir, shots_path):
    prs = load(prs_dir)
    if not prs:
        sys.exit(f"no pr-NN.md files in {prs_dir}")
    shots = []
    if shots_path:
        base = os.path.dirname(os.path.abspath(shots_path))
        shots = json.load(open(shots_path, encoding="utf-8"))
        for s in shots:
            s["_src"] = os.path.relpath(os.path.join(base, s["file"]), os.path.dirname(os.path.abspath(page)))
    groups = {}
    for p in prs:
        groups.setdefault(p["ms"], []).append(p)
    cards = [C0, '<div class="milestones">']
    for name, items in groups.items():
        note = next((p["ms_note"] for p in items if p["ms_note"]), "")
        tag = f'<span class="tag">{esc(note)}</span>' if note else ""
        cards.append(f'<div class="ms"><div class="ms-head"><h3>{esc(name)}</h3>{tag}</div><div class="prs">')
        for p in items:
            cards.append(f'<div class="pr" data-pr="{p["n"]}" tabindex="0" role="button" aria-label="Open PR {p["n"]} deep dive"><span class="k">{p["n"]}</span>'
                         f'<div class="t"><b>{esc(p["title"])}</b><span>{inline(p["change"])}</span></div><div class="rb"><b>Rollback</b>{inline(p["rollback"])}</div></div>')
        cards.append("</div></div>")
    cards += ["</div>", C1]
    detail = [D0, "<style>", open(CSS, encoding="utf-8").read(), "</style>"]
    for p in prs:
        figs = []
        for s in shots:
            if p["n"] in (s.get("prs") or []):
                cap = esc(" · ".join(x for x in (s.get("page"), s.get("route"), s.get("caption")) if x))
                figs.append(f'<figure class="shot"><img loading="lazy" src="{esc(s["_src"])}" alt="{cap}"><figcaption>{cap}</figcaption></figure>')
        shot_html = ('<h3>Pages this touches, as they look today</h3><div class="shots">' + "".join(figs) + "</div>") if figs else '<p class="muted">Screenshots: none for this PR.</p>'
        detail.append(f'<template id="pr-{p["n"]}" data-title="PR {p["n"]} · {esc(p["title"])}">{shot_html}{markdown(p["body"])}</template>')
    detail.append("""<dialog class="prd" id="prd"><div class="prd-head"><b id="prd-title">PR</b><button type="button" id="prd-close">Close</button></div><div class="prd-body" id="prd-body"></div></dialog>
<script>
(function () {
  const dlg = document.getElementById("prd"), body = document.getElementById("prd-body"), title = document.getElementById("prd-title");
  function open(n) {
    const t = document.getElementById("pr-" + n);
    if (!t) return;
    body.innerHTML = t.innerHTML; title.textContent = t.dataset.title;
    dlg.showModal(); body.scrollTop = 0; history.replaceState(null, "", "#pr-" + n);
  }
  document.querySelectorAll(".pr").forEach(card => {
    const n = card.dataset.pr;
    card.addEventListener("click", () => open(n));
    card.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); open(n); } });
  });
  document.getElementById("prd-close").addEventListener("click", () => dlg.close());
  dlg.addEventListener("click", e => { if (e.target === dlg) dlg.close(); });
  dlg.addEventListener("close", () => history.replaceState(null, "", location.pathname));
  const m = location.hash.match(/^#pr-(\\d+)$/); if (m) open(m[1]);
})();
</script>""")
    detail.append(D1)
    s = open(page, encoding="utf-8").read()
    for a, b in ((C0, C1), (D0, D1)):
        if (a in s) != (b in s):
            sys.exit(f"{page}: unmatched {a} / {b} markers; fix or remove them by hand")
    block = "\n".join(cards)
    if C0 in s:
        s = s[:s.index(C0)] + block + s[s.index(C1) + len(C1):]
    elif "<!-- PR-CARDS -->" in s:
        s = s.replace("<!-- PR-CARDS -->", block, 1)
    else:
        sys.exit("page has no <!-- PR-CARDS --> placeholder")
    d = "\n".join(detail)
    s = s[:s.index(D0)] + d + s[s.index(D1) + len(D1):] if D0 in s else s.replace("</body>", d + "\n</body>", 1)
    open(page, "w", encoding="utf-8").write(s)
    print(f"wrote {len(prs)} cards in {len(groups)} milestones, {len(shots)} screenshots ->", page)

if __name__ == "__main__":
    a = sys.argv[1:]
    if "--shots" in a and a.index("--shots") + 1 >= len(a):
        sys.exit("--shots needs a file")
    shots = a[a.index("--shots") + 1] if "--shots" in a else None
    pos = [x for i, x in enumerate(a) if not x.startswith("--") and (i == 0 or a[i - 1] != "--shots")]
    if len(pos) != 2:
        sys.exit(__doc__)
    build(pos[0], pos[1], shots)
