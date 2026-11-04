"""Build the shareable TDD (tdd-share.html) and the eng-review deck (deck.html) from one story file. Re-runnable.
Usage: build_share.py <packet-dir> [--story share/story.json] [--style doc-style.css] [--check]
The story (shape: templates/share-story.md) is the one source for both pages, so they cannot drift. Text is escaped
and supports `code`, **bold**, ==highlight== and [link](https://...); only walkthrough and problem `html` snippets are
raw. Every local <img> is inlined, so each page is one file. --style is the doc-style.css tokens_to_css.py writes
(default: <packet>/doc-style.css when it exists); its project token block is layered on templates/share-base.css.
--check validates the story and writes nothing.
The story carries only the current design: history markers (superseded, addendum, strike-through, "updated on")
are refused; the history lives in status.md.
Exit 0 on success; 1 with every problem listed on a bad story, a missing image or bad arguments."""
import base64, html, json, mimetypes, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
TPL = os.path.join(HERE, "..", "templates")
esc = html.escape

# ---------------------------------------------------------------- arguments
args = sys.argv[1:]
def opt(name, default=None):
    if name in args:
        i = args.index(name)
        if i + 1 >= len(args) or args[i + 1].startswith("--"):
            sys.exit(f"{name} needs a value")
        v = args[i + 1]
        del args[i:i + 2]
        return v
    return default
story_arg, style_arg = opt("--story"), opt("--style")
check_only = "--check" in args
args = [a for a in args if a != "--check"]
if len(args) != 1 or args[0].startswith("--"):
    sys.exit(__doc__)
root = os.path.abspath(args[0])
if not os.path.isdir(root):
    sys.exit(f"{root}: not a directory")
story_path = os.path.join(root, story_arg or os.path.join("share", "story.json"))
try:
    story = json.load(open(story_path, encoding="utf-8"))
except (OSError, ValueError) as e:
    sys.exit(f"{story_path}: {e}")

# ---------------------------------------------------------------- validation
TYPES = {
    "problem": ["pains"], "whys": ["rows"], "fix": ["thesis", "pieces"], "walkthrough": ["steps"],
    "lifecycle": ["states", "start", "actions"], "compare": ["before", "after"], "model": ["tables"],
    "diagram": ["src", "alt"], "decisions": ["items"], "slices": ["items"], "text": [],
}
LIST_CAP = {"steps": 10, "items": 12, "cols": 14, "rows": 12, "chapters": 6, "sections": 10, "actions": 10, "states": 8}
TEXT_CAP, HEADLINE_CAP, NOTES_CAP = 420, 110, 900
RAW = {"html", "derive", "notes", "href", "src", "fonts_href"}
HISTORY = re.compile(r"supersed|\baddend(um|a)\b|~~|<\s*(s|del|strike)\b|\brevised on\b|\bupdated (on|in) \d|\(was\b", re.I)
errors = []

def walk(node, path):
    if isinstance(node, dict):
        for k, v in node.items():
            walk(v, f"{path}.{k}")
    elif isinstance(node, list):
        key = path.rsplit(".", 1)[-1]
        cap = LIST_CAP.get(key, 8)
        if len(node) > cap:
            errors.append(f"{path}: {len(node)} items, the cap is {cap}; split it or cut it")
        for i, v in enumerate(node):
            walk(v, f"{path}[{i}]")
    elif isinstance(node, str):
        key = re.sub(r"\[\d+\]$", "", path.rsplit(".", 1)[-1])
        if HISTORY.search(node):
            errors.append(f"{path}: carries history ({HISTORY.search(node).group(0)!r}); show only the current answer, keep history in status.md")
        cap = NOTES_CAP if key == "notes" else HEADLINE_CAP if key == "headline" else None if key in RAW else TEXT_CAP
        if cap and len(node) > cap:
            errors.append(f"{path}: {len(node)} characters, the cap is {cap}")

def need(obj, keys, path):
    if not isinstance(obj, dict):
        errors.append(f"{path}: expected an object")
        return False
    for k in keys:
        if obj.get(k) in (None, "", []):
            errors.append(f"{path}: missing {k}")
    return True

need(story, ["title", "headline", "lede", "chapters"], "story")
walk(story, "story")
chapters = story.get("chapters") or []
if not isinstance(chapters, list):
    errors.append("story.chapters: expected a list"); chapters = []
if len(story.get("stats") or []) > 4:
    errors.append("story.stats: at most 4 stat tiles")
multi = len(chapters) > 1
for ci, ch in enumerate(chapters):
    cp = f"story.chapters[{ci}]"
    if not need(ch, ["sections"], cp):
        continue
    if multi and not ch.get("title"):
        errors.append(f"{cp}: a design with several chapters names each one (title)")
    for si, s in enumerate(ch.get("sections") or []):
        sp = f"{cp}.sections[{si}]"
        if not need(s, ["type", "title"], sp):
            continue
        t = s.get("type")
        if t not in TYPES:
            errors.append(f"{sp}: unknown type {t!r} (one of {', '.join(TYPES)})"); continue
        need(s, TYPES[t], sp)
        if t == "walkthrough":
            for i, st in enumerate(s.get("steps") or []):
                if need(st, ["title", "note"], f"{sp}.steps[{i}]") and not (st.get("image") or st.get("html")):
                    errors.append(f"{sp}.steps[{i}]: needs an image or an html mock")
        if t == "lifecycle":
            names = [x.get("name") for x in s.get("states") or [] if isinstance(x, dict)]
            if s.get("start") not in names:
                errors.append(f"{sp}.start: {s.get('start')!r} is not one of the states")
            for i, a in enumerate(s.get("actions") or []):
                if not need(a, ["label", "to"], f"{sp}.actions[{i}]"):
                    continue
                for n in [a.get("to")] + [f for f in (a.get("from") or []) if f != "*"]:
                    if n not in names:
                        errors.append(f"{sp}.actions[{i}]: {n!r} is not one of the states")
        if t == "fix" and not 2 <= len(s.get("pieces") or []) <= 4:
            errors.append(f"{sp}.pieces: 2 to 4 pieces")
        if t == "whys" and len(s.get("rows") or []) < 2:
            errors.append(f"{sp}.rows: at least a symptom and a root")
        if t == "text" and not (s.get("paragraphs") or s.get("table")):
            errors.append(f"{sp}: a text section needs paragraphs or a table")
for i, a in enumerate(story.get("asks") or []):
    need(a, ["q"], f"story.asks[{i}]")
if story.get("fonts_href") and not str(story["fonts_href"]).startswith("https://"):
    errors.append("story.fonts_href: must be an https:// stylesheet URL")
if errors:
    sys.exit(f"{story_path}: {len(errors)} problem(s)\n  " + "\n  ".join(errors))
if check_only:
    print(f"ok: {story_path}")
    sys.exit(0)

# ---------------------------------------------------------------- text helpers
def fmt(s):
    s = esc(str(s), quote=False)
    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    s = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", s)
    s = re.sub(r"==([^=]+)==", r"<mark>\1</mark>", s)
    return re.sub(r"\[([^\]]+)\]\((https?://[^)\s]+)\)", r'<a href="\2" target="_blank" rel="noopener">\1</a>', s)

def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", str(s).lower()).strip("-")[:40] or "s"

TONE = {"good", "warn", "bad", "mute", "accent"}
def tone(t):
    return t if t in TONE else ""

DEFAULT = {  # type -> (eyebrow, nav label)
    "problem": ("The problem today", "Today"), "whys": ("Why it keeps happening", "Why"), "fix": ("How we solve it", "The fix"),
    "walkthrough": ("Walkthrough", "Walkthrough"), "lifecycle": ("Lifecycle", "Lifecycle"), "compare": ("Before and after", "Before/after"),
    "model": ("Data", "Data"), "diagram": ("Diagram", "Diagram"), "decisions": ("Locked decisions", "Decisions"),
    "slices": ("Build plan", "Build"), "text": ("Notes", "Notes"),
}

def img(src, alt):
    return f'<img src="{esc(src)}" alt="{esc(alt or "")}">'

def opt_p(obj, key, tag="p"):
    """<tag>text</tag> for an optional field, or nothing."""
    return f"<{tag}>{fmt(obj[key])}</{tag}>" if obj.get(key) else ""

def ul(items, cls="list"):
    return f'<ul class="{cls}">' + "".join(f"<li><span>{fmt(x)}</span></li>" for x in items) + "</ul>"

def chunks(xs, n):
    return [xs[i:i + n] for i in range(0, len(xs), n)] or [[]]

# ---------------------------------------------------------------- blocks (shared by page and deck)
def whys_html(s):
    rows = s["rows"]
    out = []
    for i, r in enumerate(rows):
        root_cls = " root" if i == len(rows) - 1 else ""
        ev = f'<span class="ev">{fmt(r["evidence"])}</span>' if r.get("evidence") else ""
        label = r.get("label") or ("Symptom" if i == 0 else "Root" if root_cls else f"Why {i}")
        out.append(f'<div class="why{root_cls}" style="--i:{i}"><span class="k">{fmt(label)}</span><p>{fmt(r["text"])}{ev}</p></div>')
    return '<div class="whys">' + "".join(out) + "</div>"

def pains_html(s):
    return '<div class="pains">' + "".join(
        f'<div class="pain"><span class="ic">{i + 1}</span><div><h3>{fmt(p["title"])}</h3><p>{fmt(p.get("text", ""))}</p></div></div>'
        for i, p in enumerate(s["pains"])) + "</div>"

def problem_visual(s):
    if s.get("html"):
        return f'<div class="visual">{s["html"]}</div>'
    if s.get("image"):
        im = s["image"]
        cap = f"<figcaption>{fmt(im['caption'])}</figcaption>" if im.get("caption") else ""
        return f'<figure class="figure">{img(im["src"], im.get("alt"))}{cap}</figure>'
    return ""

def pieces_html(s):
    out = []
    for i, p in enumerate(s["pieces"]):
        where = f'<span class="where">{fmt(p["where"])}</span>' if p.get("where") else ""
        out.append(f'<div class="piece"><span class="n">{fmt(p.get("label") or i + 1)}</span><h3>{fmt(p["title"])}</h3><p>{fmt(p.get("text", ""))}</p>{where}</div>')
    return '<div class="pieces">' + "".join(out) + "</div>"

def versus_html(s):
    if not (s.get("not_building") or s.get("building")):
        return ""
    no = s.get("not_building") or []
    yes = s.get("building") or []
    nf = f'<p class="foot">{fmt(s["not_note"])}</p>' if s.get("not_note") else ""
    yf = f'<p class="foot">{fmt(s["building_note"])}</p>' if s.get("building_note") else ""
    return (f'<div class="versus"><div class="vs no"><h3>What we are not building</h3>{ul(no, "")}{nf}</div>'
            f'<div class="vs yes"><h3>What we build instead</h3>{ul(yes, "")}{yf}</div></div>')

def step_visual(st):
    if st.get("html"):
        return st["html"]
    return img(st["image"], st.get("alt") or st["title"])

def states_html(s, on=None):
    return '<div class="states">' + "".join(
        f'<div class="st {tone(x.get("tone"))}{" on" if x["name"] == on else ""}" data-s="{esc(x["name"])}"><b>{fmt(x["name"])}</b><small>{fmt(x.get("desc", ""))}</small></div>'
        for x in s["states"]) + "</div>"

def rlist_html(items):
    out = []
    for i, r in enumerate(items):
        sub = f'<span class="sub">{fmt(r["sub"])}</span>' if r.get("sub") else ""
        out.append(f'<div class="ritem{" hi" if r.get("hi") else ""}"><span class="pos">{i + 1}</span><span><span class="nm">{fmt(r["name"])}</span>{sub}</span></div>')
    return '<div class="rlist">' + "".join(out) + "</div>"

def side_html(s):
    if not s.get("side"):
        return ""
    return '<div class="side">' + "".join(
        f'<div class="stg"><span class="d">{fmt(x.get("label") or i + 1)}</span><span><b>{fmt(x["title"])}</b><small style="display:block;color:var(--muted)">{fmt(x.get("sub", ""))}</small></span></div>'
        for i, x in enumerate(s["side"])) + "</div>"

def table_html(t):
    st = t.get("status", "exists")
    cols = "".join(
        f'<div class="col{" key" if c.get("key") else ""}{" added" if c.get("added") else ""}"><span>{fmt(c["name"])}</span><span>{fmt(c.get("type", ""))}</span></div>'
        for c in t.get("cols") or [])
    badge = {"new": "new", "changed": "changed"}.get(st, "")
    note = f'<div class="col"><span style="font-family:var(--f-body);color:var(--muted)">{fmt(t["note"])}</span><span></span></div>' if t.get("note") else ""
    return f'<div class="tbl {esc(st)}"><div class="tbl-h"><span>{fmt(t["name"])}</span><span class="badge {badge}">{esc(st)}</span></div>{cols}{note}</div>'

ARROW = '<div class="arrow" aria-hidden="true"><svg width="40" height="24" viewBox="0 0 40 24" fill="none"><path d="M2 12h34M28 4l8 8-8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></div>'

def model_html(s):
    new = [t for t in s["tables"] if t.get("status") in ("new", "changed")]
    old = [t for t in s["tables"] if t.get("status") not in ("new", "changed")]
    if new and old:
        body = f'<div class="model"><div class="tcol">{"".join(map(table_html, new))}</div>{ARROW}<div class="tcol">{"".join(map(table_html, old))}</div></div>'
    else:
        body = f'<div class="model solo">{"".join(map(table_html, new or old))}</div>'
    rules = ""
    if s.get("rules"):
        rules = '<div class="rules">' + "".join(f'<div class="rule"><b>{fmt(r["title"])}</b><span>{fmt(r.get("text", ""))}</span></div>' for r in s["rules"]) + "</div>"
    return body, rules

def decisions_html(items):
    return '<div class="dgrid">' + "".join(
        f'<div class="dec"><span class="q">{fmt(d["q"])}</span><span class="a">{fmt(d["a"])}</span>{opt_p(d, "text")}</div>'
        for d in items) + "</div>"

def lanes_html(items, start=0):
    out = []
    for i, x in enumerate(items, start + 1):
        tags = "".join(f'<span class="badge">{fmt(t)}</span>' for t in x.get("tags") or [])
        out.append(f'<div class="lane"><span class="n">{i}</span><div><h3>{fmt(x["title"])}</h3><p>{fmt(x.get("text", ""))}</p></div><div class="tags">{tags}</div></div>')
    return '<div class="lanes">' + "".join(out) + "</div>"

def text_html(s):
    out = "".join(f"<p>{fmt(p)}</p>" for p in s.get("paragraphs") or [])
    t = s.get("table")
    if t:
        head = "".join(f"<th>{fmt(h)}</th>" for h in t.get("head") or [])
        rows = "".join("<tr>" + "".join(f"<td>{fmt(c)}</td>" for c in r) + "</tr>" for r in t.get("rows") or [])
        out += f'<div class="table-wrap"><table>{f"<thead><tr>{head}</tr></thead>" if head else ""}<tbody>{rows}</tbody></table></div>'
    return f'<div class="prose">{out}</div>'

def asks_html(asks, cls="ask"):
    out = []
    for a in asks:
        opts = "".join(f"<li>{fmt(o)}</li>" for o in a.get("options") or [])
        ol = f"<ol>{opts}</ol>" if opts else ""
        out.append(f'<div class="{cls}"><b>{fmt(a["q"])}</b>{opt_p(a, "text")}{ol}</div>')
    return "".join(out)

def chip(c):
    inner = f'<a href="{esc(c["href"])}" target="_blank" rel="noopener">{fmt(c["text"])}</a>' if c.get("href") else fmt(c["text"])
    return f'<span class="chip">{inner}</span>'

# ---------------------------------------------------------------- the page
def head_html(s, eyebrow):
    lede = f'<p class="lede">{fmt(s["lede"])}</p>' if s.get("lede") else ""
    return f'<div class="sec-head"><span class="eyebrow">{eyebrow}</span><h2>{fmt(s["title"])}</h2>{lede}</div>'

def page_section(s, sid, eyebrow):
    t = s["type"]
    body = ""
    if t == "problem":
        vis = problem_visual(s)
        body = f'<div class="today{"" if vis else " solo"}">{vis}{pains_html(s)}</div>'
    elif t == "whys":
        body = whys_html(s)
    elif t == "fix":
        lede = f'<p class="lede">{fmt(s["lede"])}</p>' if s.get("lede") else ""
        return (f'<section id="{sid}"><div class="sec-head"><span class="eyebrow">{eyebrow}</span><h2 class="sr">{fmt(s["title"])}</h2>'
                f'<p class="thesis">{fmt(s["thesis"])}</p>{lede}</div>{pieces_html(s)}{versus_html(s)}</section>')
    elif t == "walkthrough":
        btns, panes = [], []
        for i, st in enumerate(s["steps"]):
            who = f'<span class="who">{fmt(st["who"])}</span>' if st.get("who") else ""
            sub = f'<small>{fmt(st["sub"])}</small>' if st.get("sub") else ""
            btns.append(f'<button type="button" class="step-btn" role="tab" aria-selected="{"true" if i == 0 else "false"}"><span class="d">{i + 1}</span><span>{who}<b>{fmt(st["title"])}</b>{sub}</span></button>')
            panes.append(f'<div class="pane" data-note="{esc(fmt(st["note"]))}"{"" if i == 0 else " hidden"}>{step_visual(st)}</div>')
        body = (f'<div class="walk js-walk"><div class="steps" role="tablist">{"".join(btns)}</div><div class="stage">'
                f'<div class="stage-frame" aria-live="polite">{"".join(panes)}</div><div class="stage-cap"><p class="note">{fmt(s["steps"][0]["note"])}</p>'
                f'<div class="stage-nav"><button class="btn prev" type="button">Back</button><button class="btn primary next" type="button">Next</button></div></div></div></div>')
    elif t == "lifecycle":
        actors = []
        for a in s["actions"]:
            if a.get("actor", "") not in actors:
                actors.append(a.get("actor", ""))
        ctrl = ""
        for who in actors:
            btns = "".join(
                f'<button class="btn" type="button" data-to="{esc(a["to"])}" data-from="{esc(json.dumps(a.get("from") or ["*"]))}" data-who="{esc(who)}">{fmt(a["label"])}</button>'
                for a in s["actions"] if a.get("actor", "") == who)
            ctrl += (f'<span class="who">{fmt(who)}</span>' if who else "") + f'<div class="grid">{btns}</div>'
        rule = f'<div class="derive">{esc(s["rule"])}</div>' if s.get("rule") else ""
        body = (f'<div class="sim js-sim" data-start="{esc(s["start"])}"><div class="sim-board">{states_html(s, s["start"])}{rule}</div>'
                f'<div class="sim-ctrl">{ctrl}<div class="grid"><button class="btn reset" type="button">Reset</button></div><div class="log" aria-live="polite"></div></div></div>')
    elif t == "compare":
        bl, al = s.get("before_label") or "Today", s.get("after_label") or "After"
        side = side_html(s)
        body = (f'<div class="js-compare"><div class="tog" role="group"><button type="button" data-v="before" aria-pressed="false">{fmt(bl)}</button>'
                f'<button type="button" data-v="after" aria-pressed="true">{fmt(al)}</button></div>'
                f'<div class="compare{"" if side else " solo"}" style="margin-top:20px"><div><div data-list="before" hidden>{rlist_html(s["before"])}</div>'
                f'<div data-list="after">{rlist_html(s["after"])}</div></div>{side}</div></div>')
    elif t == "model":
        m, rules = model_html(s)
        body = m + rules
    elif t == "diagram":
        cap = f"<figcaption>{fmt(s['caption'])}</figcaption>" if s.get("caption") else ""
        body = f'<figure class="figure">{img(s["src"], s["alt"])}{cap}</figure>'
    elif t == "decisions":
        body = decisions_html(s["items"])
    elif t == "slices":
        body = lanes_html(s["items"])
        if s.get("done") or s.get("questions"):
            done = f'<div class="panel"><h3>Done means</h3>{ul(s["done"], "list ok")}</div>' if s.get("done") else ""
            qs = f'<div class="panel"><h3>Open questions</h3>{ul(s["questions"], "list q")}</div>' if s.get("questions") else ""
            body += f'<div class="two">{done}{qs}</div>'
    elif t == "text":
        body = text_html(s)
    return f'<section id="{sid}">{head_html(s, eyebrow)}{body}</section>'

# ids, numbering and nav, shared by page and deck
plan = []  # (chapter index, chapter, [(section, id, eyebrow)])
used = set()
for ci, ch in enumerate(chapters):
    cid = ch.get("id") or slug(ch.get("title") or f"part-{ci + 1}")
    rows = []
    for si, s in enumerate(ch["sections"]):
        sid = s.get("id") or slug(DEFAULT[s["type"]][1] if not multi else f"{cid}-{s['title']}")
        base, n = sid, 2
        while sid in used:
            sid, n = f"{base}-{n}", n + 1
        used.add(sid)
        num = f"{ci + 1}.{si + 1}" if multi else f"{si + 1:02d}"
        rows.append((s, sid, f"{num} · {fmt(s.get('eyebrow') or DEFAULT[s['type']][0])}"))
    plan.append((ci, ch, cid, rows))

title = story["title"]
hero_em = f' <em>{fmt(story["headline_em"])}</em>' if story.get("headline_em") else ""
chips = "".join(chip(c) for c in story.get("chips") or [])
stats = story.get("stats") or []
stats_html = (f'<div class="stats" style="--n:{len(stats)}" role="list">' + "".join(
    f'<div class="stat{" goal" if x.get("goal") else ""}" role="listitem"><b>{fmt(x["value"])}</b><span>{fmt(x["label"])}</span></div>' for x in stats) + "</div>") if stats else ""
eyebrow_top = f'<span class="eyebrow">{fmt(story.get("eyebrow") or "Technical design")}</span>'

nav = [f'<span class="brand">{fmt(title)}</span>']
body = [f'<header class="hero">{eyebrow_top}<h1>{fmt(story["headline"])}{hero_em}</h1><p class="lede">{fmt(story["lede"])}</p>'
        + (f'<div class="meta">{chips}</div>' if chips else "") + f'{stats_html}</header>']
if multi:
    parts = "".join(f'<a class="part" href="#{cid}"><span class="n">Part {ci + 1}</span><b>{fmt(ch["title"])}</b>{opt_p(ch, "summary")}</a>'
                    for ci, ch, cid, _ in plan)
    body.append(f'<section id="parts"><div class="sec-head"><span class="eyebrow">The parts</span><h2>{len(plan)} parts, one design</h2></div><div class="parts">{parts}</div></section>')
for ci, ch, cid, rows in plan:
    secs = "".join(page_section(s, sid, eb) for s, sid, eb in rows)
    if multi:
        nav.append(f'<a class="ch" href="#{cid}">{fmt(ch.get("nav") or ch["title"])}</a>')
        summ = f'<p class="lede">{fmt(ch["summary"])}</p>' if ch.get("summary") else ""
        body.append(f'<div class="chapter" id="{cid}"><div class="chapter-head"><span class="n">Part {ci + 1}</span><h2>{fmt(ch["title"])}</h2>{summ}</div>{secs}</div>')
    else:
        nav += [f'<a href="#{sid}">{fmt(s.get("nav") or DEFAULT[s["type"]][1])}</a>' for s, sid, _ in rows]
        body.append(secs)
asks = story.get("asks") or []
if asks:
    nav.append('<a href="#asks">For the review</a>')
    body.append(f'<section id="asks"><div class="sec-head"><span class="eyebrow">For the review</span><h2>What we need decided</h2></div>'
                f'<div class="lanes">{asks_html(asks, "panel")}</div></section>')
links = "".join(f'<a href="{esc(l["href"])}" target="_blank" rel="noopener">{fmt(l["text"])}</a>' for l in story.get("links") or [])
body.append(f'<footer><span>{fmt(title)}{" · " + fmt(story["eyebrow"]) if story.get("eyebrow") else ""}</span>{links}</footer>')

PAGE_JS = r"""
(() => {
  document.querySelectorAll('.js-walk').forEach((w) => {
    const btns = [...w.querySelectorAll('.step-btn')], panes = [...w.querySelectorAll('.pane')];
    const note = w.querySelector('.note'), prev = w.querySelector('.prev'), next = w.querySelector('.next');
    let cur = 0;
    const show = (i) => {
      cur = Math.max(0, Math.min(panes.length - 1, i));
      panes.forEach((p, j) => { p.hidden = j !== cur; });
      btns.forEach((b, j) => b.setAttribute('aria-selected', String(j === cur)));
      note.innerHTML = panes[cur].dataset.note;
      prev.disabled = cur === 0; next.disabled = cur === panes.length - 1;
    };
    btns.forEach((b, i) => b.addEventListener('click', () => show(i)));
    prev.addEventListener('click', () => show(cur - 1));
    next.addEventListener('click', () => show(cur + 1));
    show(0);
  });
  document.querySelectorAll('.js-sim').forEach((s) => {
    const start = s.dataset.start, sts = [...s.querySelectorAll('.st')], log = s.querySelector('.log');
    const acts = [...s.querySelectorAll('[data-to]')];
    let cur = start, n = 0;
    const paint = () => {
      sts.forEach((x) => x.classList.toggle('on', x.dataset.s === cur));
      acts.forEach((a) => { const f = JSON.parse(a.dataset.from); a.disabled = !(f.includes('*') || f.includes(cur)); });
    };
    const line = (t) => { const d = document.createElement('div'); d.innerHTML = `<span class="tag">${++n}</span><span></span>`; d.lastChild.textContent = t; log.prepend(d); };
    acts.forEach((a) => a.addEventListener('click', () => { cur = a.dataset.to; line(`${a.dataset.who ? a.dataset.who + ': ' : ''}${a.textContent} → ${cur}`); paint(); }));
    s.querySelector('.reset').addEventListener('click', () => { cur = start; log.innerHTML = ''; n = 0; paint(); });
    paint();
  });
  document.querySelectorAll('.js-compare').forEach((c) => {
    const btns = [...c.querySelectorAll('.tog button')];
    btns.forEach((b) => b.addEventListener('click', () => {
      btns.forEach((x) => x.setAttribute('aria-pressed', String(x === b)));
      c.querySelectorAll('[data-list]').forEach((l) => { l.hidden = l.dataset.list !== b.dataset.v; });
    }));
  });
})();
"""

# ---------------------------------------------------------------- the deck
slides = []  # (cls, inner html, notes, label)
def slide(inner, notes="", cls="", label=""):
    slides.append((cls, inner, notes or "", label))

def sh(eyebrow, ttl, lede=""):
    return f'<span class="eyebrow">{eyebrow}</span><h2>{fmt(ttl)}</h2>' + (f'<p class="lede">{fmt(lede)}</p>' if lede else "")

slide(f'{eyebrow_top}<h1>{fmt(story["headline"])}{hero_em}</h1><p class="lede">{fmt(story["lede"])}</p>{stats_html}',
      story.get("notes") or story["lede"], "title-slide", title)
if multi:
    ag = "".join(f'<div class="part"><span class="n">{ci + 1}</span><span><b>{fmt(ch["title"])}</b>{opt_p(ch, "summary")}</span></div>'
                 for ci, ch, _, _ in plan)
    slide(f'<span class="eyebrow">Agenda</span><h2>{len(plan)} parts, one design</h2><div class="body"><div class="agenda">{ag}</div></div>', "", "", "Agenda")
for ci, ch, cid, rows in plan:
    if multi:
        slide(f'<span class="n">Part {ci + 1}</span><h2>{fmt(ch["title"])}</h2>' + (f'<p class="lede">{fmt(ch["summary"])}</p>' if ch.get("summary") else ""),
              ch.get("notes") or ch.get("summary", ""), "divider", ch["title"])
    for s, sid, eb in rows:
        t, notes, lab = s["type"], s.get("notes") or s.get("lede", ""), s["title"]
        if t == "problem":
            vis = problem_visual(s)
            slide(sh(eb, s["title"], s.get("lede")) + f'<div class="body"><div class="today{"" if vis else " solo"}">{vis}{pains_html(s)}</div></div>', notes, "", lab)
        elif t == "whys":
            slide(sh(eb, s["title"]) + f'<div class="body">{whys_html(s)}</div>', notes, "", lab)
        elif t == "fix":
            slide(f'<span class="eyebrow">{eb}</span><p class="thesis">{fmt(s["thesis"])}</p><div class="body">{pieces_html(s)}</div>', notes, "", lab)
            if versus_html(s):
                slide(sh(eb, "What we are not building") + f'<div class="body">{versus_html(s)}</div>', notes, "", lab)
        elif t == "walkthrough":
            for i, st in enumerate(s["steps"]):
                rows_html = "".join(f'<div class="step-row{" on" if j == i else ""}"><span class="d">{j + 1}</span><span>{fmt(x["title"])}</span></div>' for j, x in enumerate(s["steps"]))
                slide(sh(eb, s["title"]) + f'<div class="body"><div class="walk"><div class="steps">{rows_html}</div><div class="stage"><div class="stage-frame">{step_visual(st)}</div>'
                      f'<div class="stage-cap"><p>{fmt(st["note"])}</p></div></div></div></div>', st.get("notes") or st["note"], "", f"{lab} {i + 1}/{len(s['steps'])}")
        elif t == "lifecycle":
            rule = f'<div class="derive">{esc(s["rule"])}</div>' if s.get("rule") else ""
            acts = "".join(f'<li><span><b>{fmt(a.get("actor") or "")}</b> {fmt(a["label"])} → {fmt(a["to"])}</span></li>' for a in s["actions"])
            slide(sh(eb, s["title"], s.get("lede")) + f'<div class="body"><div class="sim"><div class="sim-board">{states_html(s, s["start"])}{rule}</div><ul class="list">{acts}</ul></div></div>', notes, "", lab)
        elif t == "compare":
            bl, al = s.get("before_label") or "Today", s.get("after_label") or "After"
            slide(sh(eb, s["title"], s.get("lede")) + f'<div class="body"><div class="two"><div class="panel"><h3>{fmt(bl)}</h3>{rlist_html(s["before"])}</div>'
                  f'<div class="panel"><h3>{fmt(al)}</h3>{rlist_html(s["after"])}</div></div></div>', notes, "", lab)
        elif t == "model":
            m, rules = model_html(s)
            slide(sh(eb, s["title"]) + f'<div class="body">{m}</div>', notes, "", lab)
            if rules:
                slide(sh(eb, s["title"]) + f'<div class="body center">{rules}</div>', notes, "", lab)
        elif t == "diagram":
            slide(sh(eb, s["title"]) + f'<figure class="figure">{img(s["src"], s["alt"])}</figure>', notes or s.get("caption", ""), "", lab)
        elif t == "decisions":
            for part in chunks(s["items"], 6):
                slide(sh(eb, s["title"]) + f'<div class="body">{decisions_html(part)}</div>', notes, "", lab)
        elif t == "slices":
            for k, part in enumerate(chunks(s["items"], 5)):
                slide(sh(eb, s["title"]) + f'<div class="body">{lanes_html(part, k * 5)}</div>', notes, "", lab)
            if s.get("done") or s.get("questions"):
                done = f'<div class="panel"><h3>Done means</h3>{ul(s["done"], "list ok")}</div>' if s.get("done") else ""
                qs = f'<div class="panel"><h3>Open questions</h3>{ul(s["questions"], "list q")}</div>' if s.get("questions") else ""
                slide(sh(eb, s["title"]) + f'<div class="body"><div class="two">{done}{qs}</div></div>', notes, "", lab)
        elif t == "text":
            slide(sh(eb, s["title"], s.get("lede")) + f'<div class="body">{text_html(s)}</div>', notes, "", lab)
if asks:
    for part in chunks(asks, 3):
        slide(f'<span class="eyebrow">Decide in the room</span><h2>What we need from this review</h2><div class="body">{asks_html(part)}</div>',
              "\n".join(a.get("notes", "") for a in part), "", "Asks")

deck_slides = "".join(
    f'<section class="slide {cls}" data-label="{esc(label)}" aria-roledescription="slide" aria-label="{i + 1} of {len(slides)}">{inner}'
    f'<div class="foot"><span>{fmt(title)}</span><span>{i + 1} / {len(slides)}</span></div><aside class="notes">{esc(notes)}</aside></section>'
    for i, (cls, inner, notes, label) in enumerate(slides))

DECK_JS = r"""
(() => {
  const body = document.body, slides = [...document.querySelectorAll('.stagebox .slide')], N = slides.length;
  const pane = document.querySelector('.notes-pane'), hud = document.querySelector('.hud');
  const presenter = location.hash === '#presenter';
  let cur = 0, peer = presenter ? window.opener : null, t0 = Date.now();
  const fit = (el, box) => { const k = Math.min(box.clientWidth / 1280, box.clientHeight / 720); el.style.setProperty('--k', k); };
  const fromHash = () => { const m = /^#(\d+)$/.exec(location.hash); return m ? Math.min(N, Math.max(1, +m[1])) - 1 : 0; };
  const notesOf = (i) => slides[i] ? slides[i].querySelector('.notes').textContent : '';
  function paint() {
    slides.forEach((s, i) => s.classList.toggle('on', i === cur));
    pane.textContent = notesOf(cur) || '(no notes)';
    hud.textContent = `${cur + 1} / ${N} · N notes · P presenter · F full screen`;
    if (presenter) {
      const put = (sel, i, label) => {
        const box = document.querySelector(sel); box.innerHTML = `<span class="lbl">${label}</span>`;
        if (!slides[i]) return; const c = slides[i].cloneNode(true); c.classList.add('on'); box.appendChild(c); fit(c, box);
      };
      put('.pv .cur', cur, `Now ${cur + 1} / ${N}`); put('.pv .nxt', cur + 1, cur + 1 < N ? `Next ${cur + 2}` : 'End');
      document.querySelector('.pv .pnotes').textContent = notesOf(cur) || '(no notes)';
      document.querySelector('.pv .cnt').textContent = `${cur + 1} / ${N}`;
    } else if (location.hash !== `#${cur + 1}`) history.replaceState(null, '', `#${cur + 1}`);
  }
  function go(i, quiet) {
    cur = Math.max(0, Math.min(N - 1, i)); paint();
    if (!quiet && peer && !peer.closed) peer.postMessage({ deck: 'go', i: cur }, '*');
  }
  window.addEventListener('message', (e) => { if (e.data && e.data.deck === 'go') go(e.data.i, true); });
  window.addEventListener('keydown', (e) => {
    if (e.metaKey || e.ctrlKey || e.altKey) return;
    const k = e.key;
    if (['ArrowRight', 'PageDown', ' ', 'Enter'].includes(k)) { e.preventDefault(); go(cur + 1); }
    else if (['ArrowLeft', 'PageUp', 'Backspace'].includes(k)) { e.preventDefault(); go(cur - 1); }
    else if (k === 'Home') go(0);
    else if (k === 'End') go(N - 1);
    else if (k === 'n' || k === 'N') body.classList.toggle('show-notes');
    else if ((k === 'p' || k === 'P') && !presenter) { peer = window.open(location.pathname + '#presenter', 'deck-presenter', 'width=1200,height=760'); setTimeout(() => go(cur), 400); }
    else if (k === 'f' || k === 'F') { document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen().catch(() => {}); }
  });
  document.querySelector('.stagebox').addEventListener('click', (e) => { if (!e.target.closest('a,button')) go(cur + (e.clientX > innerWidth / 3 ? 1 : -1)); });
  let idle; const wake = () => { hud.classList.remove('idle'); clearTimeout(idle); idle = setTimeout(() => hud.classList.add('idle'), 3000); };
  window.addEventListener('mousemove', wake); window.addEventListener('keydown', wake); wake();
  const refit = () => { slides.forEach((s) => fit(s, document.querySelector('.stagebox'))); if (presenter) paint(); };
  window.addEventListener('resize', refit);
  window.addEventListener('hashchange', () => { if (!presenter) go(fromHash(), true); });
  if (presenter) {
    body.classList.add('presenter');
    setInterval(() => { const s = Math.floor((Date.now() - t0) / 1000); document.querySelector('.pv .t').textContent = `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; }, 1000);
  } else cur = fromHash();
  refit(); paint();
})();
"""

# ---------------------------------------------------------------- style, images, write
def token_block():
    path = style_arg and os.path.join(root, style_arg) if style_arg and not os.path.isabs(style_arg) else style_arg
    if not path:
        cand = os.path.join(root, "doc-style.css")
        path = cand if os.path.isfile(cand) else None
    if not path:
        return ""
    if not os.path.isfile(path):
        sys.exit(f"{path}: no such style file (tokens_to_css.py writes it)")
    css = open(path, encoding="utf-8").read()
    if "/* tokens */" in css:
        return css.split("/* tokens */", 1)[1]
    blocks = re.findall(r":root\s*\{[^}]*\}", css)
    return blocks[-1] if blocks else ""

share_css = open(os.path.join(TPL, "share-base.css"), encoding="utf-8").read()
deck_css = open(os.path.join(TPL, "deck-base.css"), encoding="utf-8").read()
tokens_css = token_block()
missing = []

def inline_images(doc):
    def one(m):
        ref = m.group(2)
        if ref.startswith(("data:", "http:", "https:")):
            return m.group(0)
        path = os.path.normpath(os.path.join(root, ref))
        if not os.path.isfile(path):
            missing.append(ref)
            return m.group(0)
        mime = mimetypes.guess_type(path)[0] or "application/octet-stream"
        return f'{m.group(1)}"data:{mime};base64,{base64.b64encode(open(path, "rb").read()).decode()}"'
    return re.sub(r'(<img\b[^>]*?\ssrc=)"([^"]+)"', one, doc)

fonts = f'<link rel="stylesheet" href="{esc(story["fonts_href"])}">' if story.get("fonts_href") else ""
def doc(page_title, styles, body_cls, inner, js):
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            f'<title>{esc(page_title)}</title>\n{fonts}<style>\n{styles}</style>\n</head>\n<body class="{body_cls}">\n{inner}\n<script>{js}</script>\n</body>\n</html>\n')

page = doc(f"{title} TDD", share_css + "\n/* tokens */\n" + tokens_css + "\n.sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }\n", "",
           f'<nav class="nav" aria-label="Sections"><div class="nav-in">{"".join(nav)}</div></nav>\n<main class="wrap">\n' + "\n".join(body) + "\n</main>", PAGE_JS)
deck = doc(f"{title} deck", share_css + "\n/* tokens */\n" + tokens_css + "\n" + deck_css, "deck",
           f'<div class="stagebox">{deck_slides}</div><div class="notes-pane" aria-live="polite"></div><div class="hud"></div>'
           f'<div class="pv"><div class="bar"><span class="t">00:00</span><span class="cnt"></span><span>{fmt(title)}</span></div><div class="cur"></div>'
           f'<div class="side"><div class="nxt"></div><div class="pnotes"></div></div></div>', DECK_JS)
page, deck = inline_images(page), inline_images(deck)
if missing:
    sys.exit(f"{story_path}: missing image(s): {', '.join(sorted(set(missing)))} (paths are relative to the packet)")
open(os.path.join(root, "tdd-share.html"), "w", encoding="utf-8").write(page)
open(os.path.join(root, "deck.html"), "w", encoding="utf-8").write(deck)
print(f"wrote tdd-share.html ({len(page) / 1e6:.2f} MB, {sum(len(r) for *_, r in plan)} sections, {len(plan)} chapter(s)) "
      f"and deck.html ({len(deck) / 1e6:.2f} MB, {len(slides)} slides)")
