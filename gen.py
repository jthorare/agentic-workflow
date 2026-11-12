import pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'
FRAMES = []
def write(name, html):
    if 'serve-down' in name: html = html.replace('class="status-pill">Connected', 'class="status-pill off">Not running')
    (D / f'reviews-{name}.html').write_text(html); FRAMES.append(name)

def nav(n=4):
    m = f'<span class="n">{n}</span>' if n is not None else ''
    return ('<a>Home<span class="n">9</span></a><a class="on">Reviews' + m + '</a><a>Items<span class="n">31</span></a><a>Workflows</a>'
            '<a>Agents<span class="n">3</span></a><a>Projects</a><a>Notes</a><a>Context</a>')
KP = 'style="background:transparent;border-color:rgba(255,255,255,.5);color:#fff"'
def kb(k, p=False): return f'<kbd {KP if p else ""}>{k}</kbd>'
def btn(t, k='', cls='', p=False): return f'<span class="btn {cls}">{t}{(" " + kb(k, "primary" in cls)) if k else ""}</span>'
def keys(base, extra):
    s = ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in base)
    if extra: s += '<span class="keys-div"></span>' + ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in extra)
    return s
def det(code=''): return '<div class="dt">Details ▸' + (f' <span class="mono" style="display:none">{code}</span>' if code else '') + '</div>'
QKEYS = [('j/k', 'move'), ('Space', 'peek'), ('Enter', 'review'), ('Ctrl+Enter', 'PR menu'), ('.', 'actions')]
QPAGE = [('h', 'snooze'), ('e', 'done'), ('a', 'reviewer'), ('R', 're-rank'), ('F5', 'refresh')]
def shell(title, w, cls, header, body, kbar, sr, extra='', pre='', bodyh=None, ncount=4):
    if w < 1100: kbar = kbar
    return f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>{title}</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="reviews.css"><style>body{{width:{w}px}}{'.cmdk{width:150px;white-space:nowrap;overflow:hidden}.cmdk kbd{display:none}' if w < 1200 else ''}</style></head>
<body><div class="app {cls}">
<div class="topbar"><span class="wordmark">Sindri</span><nav class="nav">{nav(ncount)}</nav><span class="spacer"></span><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><span class="status-pill">Connected</span></div>
{header}
{body}
<div class="foot"><div class="keys">{kbar}</div><div class="sr"><b>Screen reader</b>{sr}</div></div>
{extra}</div></body></html>
'''.replace('Search or run a command', 'Search' if w < 1100 else 'Search or run a command')
# ---------------- queue ----------------
def qhead(count='4 need you · 2 ready to merge', asof='as of 09:41 ⟳ · joi', filt='Filter'):
    return f'<div class="header"><h1>Reviews</h1><span class="count">{count}</span><span class="asof">{asof}</span><div class="filter">{filt}<kbd style="margin-left:auto">/</kbd></div></div>'
def grp(name, n, chev='▾'): return f'<div class="group"><span class="chev">{chev}</span>{name}<span class="n">{n}</span></div>'
GLY = {'need': 'need', 'new': 'newc', 'ret': 'ret', 'wait': 'wait', 'ready': 'ready', 'dft': 'dft'}
def qrow(kind, num, title, reason, rcls, add, rem, ci, cicls, who, age, cls='read', extra=''):
    return (f'<div class="row rv {cls}"><span class="g {GLY[kind]}"></span><span class="title"><span class="id">#{num}</span><span class="t">{title}</span></span>'
            f'<span class="fact rs {rcls}">{reason}</span><span class="fact"><span class="diffstat"><span class="a">+{add}</span> <span class="d">−{rem}</span></span></span>'
            f'<span class="fact {cicls}">{ci}</span><span class="fact drop-peek">{who}</span><span class="age">{age}</span></div>\n')
def qrows(sel=None, ready_two=True, dim=False, merged=False, posted=False, done=None, cur=None):
    c = lambda n: 'sel' if n == sel else ''
    o = grp('Needs your review', 1 if (posted or done) else 2)
    if not posted: o += qrow('need', 3120, 'Harbor ferry: seat holds', 'asked of you', '', 310, 42, 'CI green', 'ok', 'ada', '1h40', 'unread ' + c(3120))
    if done != 3127: o += qrow('need', 3127, 'Harbor ferry: fare table', 'asked of @harbor-api', '', 88, 9, 'CI red', 'err', 'lee', '3h', 'unread ' + c(3127))
    o += grp('New commits since you reviewed', 1)
    o += qrow('new', 3131, 'Quay lights: dim at night', '3 new commits', '', 20, 4, 'CI green', 'ok', 'sam', '5h', c(3131) + (' cursor' if cur == 3131 else ''))
    o += grp('Returned to you', 1)
    o += qrow('ret', 3118, 'Booking export', '2 unreplied threads', 'att', 120, 30, 'CI green', 'ok', 'you', '1d', c(3118))
    o += grp('Waiting on others', 3 if posted else 2, '▸')
    o += grp('Ready to merge', 1 if merged else 2)
    if not merged: o += qrow('ready', 3109, 'Pier schedule: holiday rows', 'Can merge', 'ok', 64, 2, 'CI green', 'ok', 'you', '2d', c(3109))
    o += qrow('ready', 3112, 'Harbor ferry: seat map', 'Can merge', 'ok', 40, 11, 'CI green', 'ok', 'you', '1d', c(3112))
    o += grp('Drafts', 1, '▸')
    return o
def qpage(title, rows, peek='', head=None, kb=None, sr='', extra='', top='', w=1440, base=None, ncount=4, donelist=True):
    body = f'<div class="body {"peeking" if peek else ""}"><div class="list">{top}{rows}</div>{peek}</div>'
    return shell(title, w, 'q', head or qhead(), body, keys(base if base is not None else QKEYS, kb if kb is not None else QPAGE) + ('<span style="margin-left:auto;color:var(--ink-subtle)">Done list</span>' if donelist and base is None else ''), sr, extra, ncount=ncount)
def peek(state='absent', pr=3120, ready=None):
    if ready:
        title, fields = 'Pier schedule: holiday rows', [('Author', 'you → main'), ('Head', '<span class="mono">b7e2d41</span>'), ('Size', '2 files +64 −2'), ('Review', 'approved by ada')]
        num = 3109
    else:
        title, fields = 'Harbor ferry: seat holds', [('Author', 'ada → main'), ('Head', '<span class="mono">a1b2c3d</span>'), ('Size', '8 files +310 −42'), ('Asked of', 'you, 1h40 ago')]
        num = 3120
    dl = ''.join(f'<dt>{a}</dt><dd>{b}</dd>' for a, b in fields)
    if ready:
        pre = ''
        rl = '<div class="rl"><span class="g ready"></span><span class="ok">Can merge</span><span style="color:var(--ink-subtle)">required checks 2 of 2, no open thread, approved</span></div>' if ready == 'ok' else '<div class="rl"><span class="g fail"></span><span class="wt" style="color:var(--att)">Waiting on: required check <span class="mono">build</span></span></div>'
        pre = '<div class="sec">Pre-review</div><div class="pre"><span class="g linked"></span><span><b>No pre-review for this head.</b> Run one.</span></div>'
        desc = 'Adds the holiday rows to the pier timetable. No schema change.'
        act = btn('Merge ▾', '', 'primary') + btn('Review', 'Enter') + btn('Open on GitHub')
    else:
        rl = '<div class="rl"><span class="g wait"></span><span style="color:var(--ink-muted)">Needs your review. Required checks 2 of 2 pass.</span></div>'
        desc = 'Adds a 10-minute hold on a seat before payment. Touches 3 layers: endpoint, service, one migration.'
        pres = {
          'running': '<div class="pre att"><span class="g ci-run"></span><span><b>Pre-review running</b>, started 2m ago. 3 findings written so far. Cost $0.12 of the $5 day cap.</span></div>',
          'failed': '<div class="pre err"><span class="g fail"></span><span><b>Pre-review failed</b> after 4m: the run hit the model limit. Cost $0.31.' + det('SND-REVIEW-017') + '</span></div>',
          'absent': '<div class="pre"><span class="g draft"></span><span><b>No pre-review for this head.</b> Run one.</span></div>',
          'stale': '<div class="pre att"><span class="g parked"></span><span><b>Pre-review is stale.</b> It read <span class="mono">9f8e7d6</span>; the PR is now at <span class="mono">a1b2c3d</span>. 6 findings, 2 may point at moved lines. Cost $0.27.</span></div>'}
        pre = '<div class="sec">Pre-review</div>' + pres[state]
        if state == 'stale': pre += '<p style="margin-top:8px;color:var(--ink-muted)">Risk areas: static clock in <span class="lk">HoldService.cs</span>, no index on <span class="mono">seat_id</span>.</p>'
        a2 = {'running': btn('Cancel run'), 'failed': btn('Run again'), 'absent': btn('Run pre-review'), 'stale': btn('Run again')}[state]
        act = btn('Review', 'Enter', 'primary') + a2 + btn('Request reviewer', 'a') + btn('Open on GitHub')
    return f'''<aside class="peek"><div class="peek-head"><h2>{title}</h2><span class="id">#{num}</span><span class="icon">⤢</span><span class="icon">✕</span></div>
<dl class="peek-fields">{dl}</dl><div class="peek-body"><div class="sec">Description</div><p>{desc}</p>
{pre}<div class="sec">Checks</div><div class="chk"><span class="chip ok">build</span><span class="chip ok">test</span><span class="chip ok">format</span></div>{rl}</div>
<div class="peek-actions" style="flex-wrap:wrap">{act}</div></aside>'''
QSR = 'Row reads: "Needs you, number 3120, Harbor ferry: seat holds, asked of you, 310 added 42 removed, CI green, by ada, 1 hour 40 minutes ago."'
# queue frames
write('queue', qpage('Reviews - queue', qrows(sel=None).replace('row rv unread ', 'row rv unread cursor ', 1), sr='Cursor starts on row 1 (spec 7.4: the cursor starts on the first row). ' + QSR))
for st in ('running', 'failed', 'absent', 'stale'):
    write(f'queue-peek-{st}', qpage(f'Reviews - peek, pre-review {st}', qrows(sel=3120), peek(st), sr=f'Peek: pre-review {st}. ' + QSR))
write('queue-peek-ready', qpage('Reviews - peek, ready line', qrows(sel=3109), peek(ready='ok'), sr='Peek ready line reads: "Can merge. Required checks 2 of 2, no open thread, approved."'))
# partial
sl = '<div class="state-line att"><span class="g parked"></span><span class="what">List is from 09:12: GitHub search failed: rate limit reached, resets 09:58.</span><span class="btns"><span class="det">Details ▸</span><span class="btn">Retry <kbd>F5</kbd></span></span></div>'
write('queue-partial', qpage('Reviews - partial list', qrows(), top=sl, head=qhead(asof='as of 09:12 · joi'), sr='Banner reads: "List is from 09:12. GitHub search failed: rate limit reached. Button: Retry." Rows below are the last read.'))
sl = '<div class="state-line err"><span class="g dn"></span><span class="what">Sindri is not running. Showing the last read, as of 09:41.</span><span class="btns"><span class="det">Details ▸</span><span class="btn">Start Sindri</span></span></div>'
write('queue-serve-down-cache', qpage('Reviews - serve down with cache', qrows(), top=sl, head=qhead(asof='as of 09:41 · not live'), sr='Banner reads: "Sindri is not running. Showing the last read, as of 09:41. Button: Start Sindri."'))
def empty(glyph, h, p, acts, extra=''):
    return f'<div class="empty"><div class="eg"><span class="g {glyph}"></span></div><h2>{h}</h2><p>{p}</p><div class="acts">{acts}</div>{extra}</div>'
write('queue-filtered-nothing', qpage('Reviews - filter matches nothing', empty('todo', 'No match for “ferry zzz”', 'Nothing in Reviews has those words in its title, number, author or reason.', btn('Clear filter', 'Esc', 'primary')), head=qhead(filt='<span style="color:var(--ink)">ferry zzz</span><span class="caret"></span>'), base=[('Esc', 'clear filter'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], sr='Page reads: "No match for ferry zzz. Button: Clear filter."'))
guide = ('<div class="guide"><div class="step now"><span class="no">1</span><span class="say">Make a fine-grained token on github.com with Pull requests, Contents and Actions, read and write.</span><span class="btn">Open github.com</span></div>'
         '<div class="step"><span class="no">2</span><span class="say">Store it once. It never goes in an environment variable.</span><span class="seg"><span>macOS</span><span class="on">Linux</span></span></div>'
         '<div class="step" style="min-height:0"><span></span><span class="field" style="width:100%">secret-tool store --label=sindri-review service sindri-review</span><span></span></div>'
         '<div class="step"><span class="no">3</span><span class="say">Run <span class="mono">sindri review connect</span>. It reads your login and checks the token can read.</span><span></span></div></div>')
posted = '<div class="toast"><span class="g done"></span><span>Posted. Next: #3127</span><span class="bt"><span class="btn">Open <kbd>Enter</kbd></span></span><span class="life"></span></div>'
write('toast-posted', qpage('Reviews - posted toast', qrows(sel=3127, posted=True).replace('unread  sel', 'unread cursor sel').replace('unread sel', 'unread cursor sel'), head=qhead(count='3 need you · 2 ready to merge', asof='as of 09:43 ⟳ · joi'), ncount=3, extra=posted, sr='Toast reads: "Posted. Next: number 3127. Button: Open." Focus is on the row for 3127. Enter opens it.'))
write('toast-merged', qpage('Reviews - merged toast', qrows(merged=True, sel=3112).replace('sel', 'cursor sel', 1), head=qhead(count='4 need you · 1 ready to merge', asof='as of 09:44 ⟳ · joi'),
      extra='<div class="toast"><span class="g done"></span><span>Merged #3109 into main</span><span class="life"></span></div>', sr='Toast reads: "Merged number 3109 into main." Focus is on the queue cursor row. The row left Ready to merge.'))

# ---------------- review surface ----------------
RKEYS = [('j/k', 'file'), ('n/p', 'hunk'), ('↑↓', 'line'), ('c', 'comment'), ('v', 'viewed'), ('Ctrl+Enter', 'submit review'), ('?', 'all keys')]
def rhead(title='Harbor ferry: seat holds', num=3120, who='ada → main', sha='a1b2c3d', files='8 files +310 −42', viewed='3/8 viewed', primary='Submit review (2)', pk='Ctrl+Enter', pcls='primary',
          findings='Pre-review: 6 findings, 4 left', ci='CI green', req='Required 2 of 2', conf='0 conflicts', order=True, scope=False, ready=None, w=1440, since=None, chips=False):
    if primary: pb = f'<span class="btn {pcls}">{primary} {kb(pk, pcls == "primary")}</span>' if pk else f'<span class="btn {pcls}">{primary}</span>'
    else: pb = ''
    ro = '' if not ready else ready
    toggles = ''
    if order is not None: toggles += f'<span class="seg"><span class="{"on" if order else ""}">Order</span><span class="{"" if order else "on"}">Files</span></span>'
    toggles += f'<span class="seg"><span class="{"" if scope else "on"}">All</span><span class="{"on" if scope else ""}">Since your review</span></span>'
    ot = '<span class="tag">order from the code index at a1b2c3d</span>' if order and not chips else ''
    if chips:
        l2 = f'<span class="tag pri">Findings 4</span><span class="tag">Draft 2</span><span class="dot">·</span><span>{ci}</span><span class="dot">·</span><span>{req}</span><span class="dot">·</span><span>{conf}</span><span class="sp"></span>{toggles}'
    else:
        l2 = f'<span>{findings}</span><span class="dot">·</span><span>{ci}</span><span class="dot">·</span><span>{req}</span><span class="dot">·</span><span>{conf}</span>{ro}<span class="sp"></span>{ot}{toggles}'
    sin = f'<span class="tag att">{since}</span>' if since else ''
    return f'''<div class="rhead"><div class="l1"><span class="ttl">{title}</span><span class="meta"><span class="mono">#{num}</span> · {who} · head <span class="mono">{sha}</span> · {files} · <b>{viewed}</b></span>{sin}<span class="sp"></span><div class="btns"><span class="btn ghost">Actions <kbd>.</kbd></span>{pb}</div></div>
<div class="l2">{l2}</div></div>'''
def tf(mark, name, band, add, rem, cls='', mcls='', top=False):
    pm = f'<span class="pm"><span class="a">+{add}</span> <span class="d">−{rem}</span></span>' if add is not None else '<span class="pm"></span>'
    return f'<div class="tf {cls} {"vw" if mark=="✓" else ""} {"top" if top else ""}"><span class="mk {mcls}">{mark}</span><span class="nm">{name}</span><span class="bd">{band}</span>{pm}</div>'
# spec 5.0: 8 files = 7 rows plus one folded lockfile; +310 -42 in all (251/21 here plus the lockfile's 59/21); 3 ticks: HoldEndpoint, HoldRequest, docs/holds.md
FILES = [('✓', 'HoldEndpoint.cs', 'entry', 18, 2, 'v'), ('✓', 'HoldRequest.cs', 'core', 12, 0, 'v'), ('▸', 'HoldService.cs', 'core', 80, 12, ''), ('▸', 'HoldPolicy.cs', 'core', 22, 3, ''),
         ('▸', '0042_holds.sql', 'data', 14, 0, ''), ('▸', 'HoldServiceTests.cs', 'tests', 96, 4, ''), ('✓', 'docs/holds.md', 'other', 9, 0, 'v')]
def filelist_A(sel='HoldService.cs', fx=None, one=False, extra_note=''):
    def f(i):
        m, n, b, a, r, mc = FILES[i]
        c = ('sel ' if n == sel else '') + ('fx ' if n == fx else '')
        return tf(m, n.split('/')[-1], '', a, r, c, mc)
    left = '<div class="fg">▾ db/Migrations <span style="font-weight:400">1</span></div>' + f(4) + '<div class="fg">▾ docs <span style="font-weight:400">1</span></div>' + f(6) + '<div class="fg">▾ src/Booking/Holds <span style="font-weight:400">4</span></div>' + f(0) + f(3) + f(1) + f(2)
    right = '<div class="fg">▾ tests/Booking <span style="font-weight:400">1</span></div>' + f(5) + '<div class="fg">▸ Folded <span style="font-weight:400">1 lockfile</span></div>'
    if one: return f'<div class="fl one">{left}{right}</div>'
    return f'<div class="fl"><div>{left}</div><div>{right}</div></div>'
def tree(sel='HoldService.cs', fx=None, fail=None, more=False, unavailable=None, ordering=False, mode_order=True, only=None):
    def f(i, indent=True):
        m, n, b, a, r, mc = FILES[i]
        mm, cc = m, mc
        if fail == n: mm, cc = '!', 'x'
        c = ('sel ' if n == sel else '') + ('fx ' if n == fx else '')
        return tf(mm, n.split('/')[-1] + (' · couldn’t read' if fail == n else ''), '' if (fail == n or unavailable) else b, None if fail == n else a, r, c, cc)
    if ordering: return '<aside class="tree"><div class="tb" style="padding-left:6px">Ordering 8 files…</div>' + ''.join(f'<div class="tf"><span class="mk"></span><span class="nm" style="background:var(--s2);height:8px;border-radius:2px;width:{w}px;display:block"></span><span></span><span></span></div>' for w in (130, 100, 150, 110, 120, 90)) + '</aside>'
    t = '<aside class="tree">'
    if unavailable: t += f'<div class="tb" style="padding-left:6px;color:var(--att);height:auto;line-height:16px;margin:2px 0 6px">Order unavailable: {unavailable}. Files mode shown.</div>'
    t += '<div class="tf top sel"><span class="mk"></span><span class="nm" style="color:var(--ink)">Overview</span><span></span><span></span></div>' if sel == 'Overview' else '<div class="tf top"><span class="mk"></span><span class="nm">Overview</span><span></span><span></span></div>'
    if unavailable:
        t += '<div class="tg">▾ db/Migrations</div>' + f(4) + '<div class="tg">▾ docs</div>' + f(6) + '<div class="tg">▾ src/Booking/Holds</div>' + f(0) + f(3) + f(1) + f(2) + '<div class="tg">▾ tests/Booking</div>' + f(5)
    else:
        if only:
            t += '<div class="tg">▾ Booking: seat hold</div><div class="tb">core</div>' + f(2) + f(3) + '<div class="tb">data</div>' + f(4) + '<div class="tg" style="color:var(--ink-subtle);font-weight:400">▸ 11 files unchanged since your review</div>'
        else:
            t += '<div class="tg">▾ Booking: seat hold</div><div class="tb">entry</div>' + f(0) + '<div class="tb">core</div>' + f(1) + f(2) + f(3) + '<div class="tb">data</div>' + f(4) + '<div class="tb">tests</div>' + f(5)
            t += '<div class="tg">▾ Other files</div>' + f(6) + '<div class="tg" style="color:var(--ink-subtle);font-weight:400">▸ Folded: 1 lockfile</div>'
    if more: t += '<div class="more">showing 50 of 300 · <span class="lk">Load 50 more</span></div>'
    return t + '</aside>'
def dl(kind, o, n, code, cur=False, plus=False):
    m = {'a': '+', 'r': '−', 'c': ' '}[kind]
    return f'<div class="dl2 {kind if kind != "c" else ""} {"cur" if cur else ""}"><span class="o">{o}</span><span class="n">{n}</span><span class="m">{m}</span><span class="c">{code}</span>{"<span class=plus>+</span>" if plus else ""}</div>'
def diffA(cur=41, inline=None, composer=False, fnd=True, name='HoldService.cs', band='core', viewed=False, hunks=True, extra_top='', band_tag=True):
    L = [('c', 38, 38, '    public Hold Place(Seat s)'), ('c', 39, 39, '    {'), ('c', 40, 40, '        if (s.IsTaken) throw new SeatTaken(s.Id);'),
         ('r', 41, '', '        return _store.Add(new Hold(s.Id));'), ('a', '', 41, '        var until = Clock.Now.AddMinutes(10);'), ('a', '', 42, '        var hold = new Hold(s.Id, until);'), ('a', '', 43, '        return _store.Add(hold);'), ('c', 42, 44, '    }')]
    o = f'<div class="dfh"><span class="nm">{name}</span>{('<span class="tag">' + band + '</span>') if band_tag else ''}<span class="vbox {"on" if viewed else ""}"><i></i>viewed</span><span class="cl">callers</span><span class="sp"></span><span style="color:var(--ink-subtle);font-size:12px">owner @ada · +80 −12</span></div><div class="diff">{extra_top}'
    o += '<div class="hh"><span>@@ -38,5 +38,7 @@</span><span class="sy">public Hold Place(Seat s)</span></div>'
    for k, a, b, c in L:
        iscur = (b == cur)
        o += dl(k, a, b, c, cur=iscur, plus=(b == 42 and not composer and not inline))
        if b == 41 and fnd: o += '<div class="fnd"><b>draft</b>: static clock · from a finding, unedited · callers: partial (from main)</div>'
        if b == 41 and inline: o += inline
        if b == 41 and composer: o += composer
    o += '<div class="fold">▸ Load 24 more lines</div>'
    o += '<div class="hh"><span>@@ -72,3 +74,4 @@</span><span class="sy">private bool Expired(Hold h)</span></div>'
    o += dl('c', 72, 74, '        return h.Until &lt; Clock.Now;') + dl('a', '', 75, '        // TODO: use IClock') + dl('c', 73, 76, '    }')
    return o + '</div>'
def diffOpenSQL(inline_draft=True):
    o = '<div class="dfh"><span class="nm">0042_holds.sql</span><span class="vbox"><i></i>viewed</span><span class="cl">callers</span><span class="sp"></span><span style="color:var(--ink-subtle);font-size:12px">owner @ada · +14 −0</span></div><div class="diff">'
    o += '<div class="hh"><span>@@ -0,0 +1,14 @@</span><span class="sy">new file</span></div>'
    L = ['CREATE TABLE holds (', '  id          BIGSERIAL PRIMARY KEY,', '  seat_id     BIGINT NOT NULL,', '  buyer_id    BIGINT NOT NULL,', '  placed_at   TIMESTAMPTZ NOT NULL DEFAULT now(),',
         '  expires_at  TIMESTAMPTZ NOT NULL,', '  released_at TIMESTAMPTZ NULL,', "  state       TEXT NOT NULL DEFAULT 'held'", ');', '', 'ALTER TABLE seats ADD COLUMN held_until TIMESTAMPTZ NULL;',
         'CREATE INDEX holds_expires_idx ON holds (expires_at);', '-- backfill: existing rows get no hold', 'UPDATE seats SET held_until = NULL;']
    for i, t in enumerate(L, 1):
        o += dl('a', '', i, t, cur=(i == 1))
        if i == 9 and inline_draft: o += '<div class="fnd"><b>draft</b>: no index on seat_id · typed, with a suggestion</div>'
    return o + '</div>'
def diffOpenEndpoint():
    o = '<div class="dfh"><span class="nm">HoldEndpoint.cs</span><span class="tag">entry</span><span class="vbox on"><i></i>viewed</span><span class="cl">callers</span><span class="sp"></span><span style="color:var(--ink-subtle);font-size:12px">owner @ada · +18 −2</span></div><div class="diff">'
    o += '<div class="hh"><span>@@ -10,6 +10,10 @@</span><span class="sy">public static class HoldEndpoint</span></div>'
    o += dl('c', 10, 10, 'public static class HoldEndpoint') + dl('c', 11, 11, '{')
    o += dl('r', 12, '', '    public static void Map(IEndpointRouteBuilder app) => app.MapPost("/seats/hold", Place);')
    o += dl('a', '', 12, '    public static void Map(IEndpointRouteBuilder app) =>', cur=True)
    o += dl('a', '', 13, '        app.MapPost("/holds", (HoldRequest r, HoldService s) => s.Place(r.Seat));')
    o += dl('c', 13, 14, '}')
    return o + '</div>'
def diffSplit():
    L = [('c', 38, 38, '    public Hold Place(Seat s)', '    public Hold Place(Seat s)'), ('c', 39, 39, '    {', '    {'), ('c', 40, 40, '        if (s.IsTaken) throw new SeatTaken(s.Id);', '        if (s.IsTaken) throw new SeatTaken(s.Id);'),
         ('x', 41, 41, '        return _store.Add(new Hold(s.Id));', '        var until = Clock.Now.AddMinutes(10);'), ('x', '', 42, '', '        var hold = new Hold(s.Id, until);'),
         ('x', '', 43, '', '        return _store.Add(hold);'), ('c', 42, 44, '    }', '    }')]
    o = '<div class="dfh"><span class="nm">HoldService.cs</span><span class="vbox"><i></i>viewed</span><span class="cl">callers</span><span class="sp"></span><span style="color:var(--ink-subtle);font-size:12px">owner @ada · +80 −12</span><span class="seg"><span>Unified</span><span class="on">Split</span></span><span style="color:var(--ink-subtle);font-size:12px"><kbd>g l</kbd></span></div><div class="diff"><div class="hh" style="padding-left:12px"><span>@@ -38,5 +38,7 @@</span><span class="sy">public Hold Place(Seat s)</span></div><div class="split">'
    for k, a, b, l, r in L:
        if k == 'c': o += f'<div class="side"><span class="n">{a}</span><span class="m"> </span><span class="c">{l}</span></div><div class="side"><span class="n">{b}</span><span class="m"> </span><span class="c">{r}</span></div>'
        else:
            lc = 'r' if l else 'e'; rc = 'a'
            o += f'<div class="side {lc}"><span class="n">{a}</span><span class="m">{"−" if l else ""}</span><span class="c">{l}</span></div><div class="side {rc}"><span class="n">{b}</span><span class="m">+</span><span class="c">{r}</span></div>'
    return o + '</div></div>'
def pcol(open_threads=True, tstate='normal', fstate='normal', draft='Draft: 2 comments', dcs=True):
    P = [('Required checks', '2 of 2 pass', 'ok'), ('Doc map', '3 docs, 1 phase', ''), ('Test plan', '4 of 6 steps', 'att'), ('CI', '2 green, 0 red', 'ok'), ('Evidence', '1 run, 3 shots', ''), ('Risk', '2 areas', 'att'), ('Conversation', '3 comments', '')]
    o = '<aside class="pc">' + ''.join(f'<div class="pn"><span class="ch">▸</span><b>{a}</b><span class="sm {c}">{b}</span></div>' for a, b, c in P)
    o += threads_block(tstate) + findings_block(fstate)
    o += f'<div class="dc"><span>{draft}</span><span class="sp"></span><span class="btn ghost">Submit… <kbd>Ctrl+Enter</kbd></span></div></aside>'
    return o
def threads_block(state='normal', compact=False):
    h = '<div class="ph">Threads<span class="n">3</span><span class="right">on this file first</span></div>'
    if state == 'empty': return '<div class="ph">Threads<span class="n">0</span></div><div class="empty1">No threads</div>'
    if state == 'error': return '<div class="ph">Threads</div><div class="th err"><div class="tt"><span class="g fail"></span>Couldn’t load threads</div><div class="e">GitHub answered 502 Bad Gateway.</div><div class="ac"><span class="btn">Retry</span></div></div>'
    t1 = '<div class="th"><div class="tt"><span class="g msg"></span>HoldService.cs:41 <span class="st op">open</span></div><div class="tx"><b style="color:var(--ink);font-weight:600">@lee</b> why a static clock?</div><div class="ac"><span class="btn">Reply <kbd>r</kbd></span><span class="btn">Resolve <kbd>Ctrl+⏎</kbd></span></div></div>'
    if state == '019':
        t1 = '<div class="th err"><div class="tt"><span class="g msg"></span>HoldService.cs:41 <span class="st op">open</span></div><div class="tx"><b style="color:var(--ink);font-weight:600">@lee</b> why a static clock?</div><div class="e">Couldn’t resolve: GitHub refused it. The thread stays open here.<small>SND-REVIEW-019 · resolve</small></div><div class="ac"><span class="btn">Retry</span><span class="btn">Reply <kbd>r</kbd></span></div></div>'
    base1 = '<div class="th{cls}"><div class="tt"><span class="g msg"></span>HoldService.cs:41 <span class="st op">open</span></div><div class="tx"><b style="color:var(--ink);font-weight:600">@lee</b> why a static clock?</div>{mid}</div>'
    if state == 'sending': t1 = base1.format(cls='', mid='<div class="ev2" style="font-size:12px;color:var(--ink-muted);margin-top:4px"><span class="g ci-run" style="vertical-align:-2px;margin-right:6px"></span>Resolving… sending</div><div class="ac"><span class="btn">Discard</span></div>')
    if state == 'sendfail': t1 = base1.format(cls=' err', mid='<div class="e">Couldn’t resolve: GitHub refused it, the thread is outdated.<small>SND-REVIEW-019 · resolve</small></div><div class="ac"><span class="btn foc">Retry</span><span class="btn">Discard</span></div>')
    if state == 'expired': t1 = base1.format(cls='', mid='<div class="e" style="color:var(--att)">Not sent: expired after 10 minutes.' + det('SND-REVIEW-022') + '</div><div class="ac"><span class="btn foc">Repeat</span></div>')
    t2 = '<div class="th"><div class="tt"><span class="g done"></span>HoldPolicy.cs:12 <span class="st">resolved, outdated</span></div><div class="tx">@sam: rename to MaxHoldMinutes. Done.</div></div>'
    return h + t1 + t2
def findings_block(state='normal'):
    fd = lambda sv, cls, anchor, why, ev, acs, extra='': f'<div class="fd {extra}"><div class="tt"><span class="sv {cls}">{sv}</span>{anchor}</div><div class="ws">{why}</div><div class="ev">evidence: {ev}</div><div class="ac">{acs}</div></div>'
    acc = '<span class="btn">Accept <kbd>y</kbd></span><span class="btn">Edit <kbd>e</kbd></span><span class="btn">Drop <kbd>d</kbd></span>'
    f1 = fd('High', 'hi', 'HoldPolicy.cs:17', 'No cap on hold length.', '2 callers pass no limit', acc)
    f2 = fd('Question', 'me', 'HoldRequest.cs:8', 'Is the seat id checked before the hold is placed?', 'caller list partial (from main), 1 reader', acc)
    more = '<div class="empty1">2 more below</div>'
    if state == 'empty': return '<div class="ph">Findings</div><div class="empty1">No pre-review for this head. <span class="lk">Run one</span></div>'
    if state == 'new': return '<div class="ph">Findings<span class="n">4</span><span class="tag pri" style="margin-left:6px">2 new</span></div>' + f1 + f2 + more
    if state == 'stale':
        return '<div class="ph">Findings<span class="n">4</span><span class="tag att" style="margin-left:6px">stale</span></div>' + fd('High', 'hi', 'HoldPolicy.cs:17', 'No cap on hold length.', '2 callers pass no limit', '<span class="btn dis">Accept <kbd>y</kbd></span><span class="btn">Drop <kbd>d</kbd></span>', 'stale') + '<div class="empty1" style="padding-top:0;color:var(--att)">This line is not in the head’s diff. It can’t be accepted.' + det('SND-REVIEW-016') + '</div>'
    if state == 'partial':
        return '<div class="ph">Findings<span class="n">1</span><span class="tag att" style="margin-left:6px">run unfinished</span></div>' + f1 + '<div class="empty1" style="color:var(--att)">The run stopped after 1 finding: it hit the 2 KiB cap on a text.' + det('SND-REVIEW-024') + '</div>'
    return '<div class="ph">Findings<span class="n">4</span></div>' + f1 + f2 + more
INLINE = ('<div class="inl"><div class="who">@lee <span style="font-weight:400;color:var(--ink-subtle)">open · HoldService.cs:41</span></div><div class="tx">why a static clock?</div><div class="ac"><span class="btn">Reply <kbd>r</kbd></span><span class="btn">Resolve <kbd>Ctrl+⏎</kbd></span></div></div>')
def surfA(w=1440, title='Harbor ferry: seat holds', head=None, body_extra='', banner='', cur=41, inline=None, composer=False, split=False, kb=None, sr='', extra='', pc=None, filelist=None, dtop='', opening=False, dover=None, num=3120, primary='Submit review (2)', pk='Ctrl+Enter', **hk):
    hk.setdefault('order', False)
    h = head or rhead(title=title, num=num, primary=primary, pk=pk, w=w, **hk)
    if inline is None and not composer and not split and not opening: inline = INLINE
    d = dover if dover is not None else diffOpenSQL() if opening else (diffSplit() if split else diffA(cur=cur, inline=inline, composer=composer, band_tag=False))
    left = f'<div class="dpane">{banner}{filelist if filelist is not None else filelist_A(sel="0042_holds.sql" if opening else "HoldService.cs", one=(w < 1100))}{d}</div>'
    body = f'<div class="rbody" style="grid-template-columns:minmax(0,1fr) 420px">{left}{pc or pcol()}</div>'
    return shell(f'Reviews - {title}', w, 'rev', h, body, keys(RKEYS, None) if kb is None else kb, sr, extra)
ASR = 'File: "HoldService.cs, core, 80 lines added, unread." Line: "line 41, added: var until, cursor." Finding: "High finding, HoldService.cs line 41: static clock, evidence 2 hits."'
ASRO = 'File list: "0042_holds.sql, data, 14 lines added, unread." Line: "line 1, added: CREATE TABLE holds, cursor." The draft marker under line 9 reads "draft: no index on seat_id."'
write('a-1440', surfA(1440, opening=True, sr='Layout A, 1440, Files mode, on open: the cursor is on the first changed line of the first file. ' + ASRO))
write('a-1200', surfA(1200, opening=True, sr='Layout A, 1200, on open. ' + ASRO))
write('a-1440-walk', surfA(1440, sr='Layout A, 1440, mid-walk: the cursor moved to HoldService.cs line 41 (j then line keys). ' + ASR))
write('a-split-diff', surfA(1440, split=True, sr='Split diff, key g l. Each side reads "line 41, added" or "line 41, removed" with its text.', kb=keys(RKEYS, [('g l', 'split/unified'), ('w', 'whitespace')])))

# ---------------- three-pane walk ----------------
def card(title, sm, body, smc=''):
    return f'<div style="border:1px solid var(--hair);border-radius:var(--r3);background:var(--s1);margin-bottom:10px"><div style="display:flex;align-items:center;gap:8px;height:32px;padding:0 12px;border-bottom:1px solid var(--hair);font-size:12px"><span style="font-weight:600;color:var(--ink)">{title}</span><span style="margin-left:auto;color:var(--{smc or "ink-subtle"})">{sm}</span></div><div style="padding:6px 12px;font-size:12px;line-height:20px;color:var(--ink-muted)">{body}</div></div>'
def overview():
    o = '<div class="dfh"><span class="nm">Overview</span><span class="tag">order built from the code index at a1b2c3d</span><span class="sp"></span><span style="color:var(--ink-subtle);font-size:12px"><kbd>n</kbd> first file</span></div><div style="padding:16px 24px;overflow:hidden;flex:1;min-height:0">'
    o += '<div style="font-size:13px;line-height:21px;color:var(--ink)"><span style="color:var(--ink-subtle);font-size:11px;font-weight:500">Summary (agent)</span><br>Adds a 10-minute hold on a seat before payment. Touches 3 layers: the endpoint, the service and policy, one migration. <span style="color:var(--ink-subtle)">Risks 2 · Questions 4</span></div><div style="height:12px"></div>'
    o += '<div style="display:grid;grid-template-columns:1fr 1fr;column-gap:12px">'
    o += '<div>' + card('Required checks', '2 of 2 pass', '<span class="g done"></span> build &nbsp; <span class="g done"></span> test', 'ok') + card('Test plan', '4 of 6 steps', '☑ hold expires after 10 min<br>☑ second buyer refused<br>☑ hold survives a reload<br>☑ cancel frees the seat<br>☐ payment inside the hold<br>☐ migration on a full table', 'att') + card('Evidence', '1 run', 'api-run seat-hold: 3 checks pass') + '</div>'
    o += '<div>' + card('CI', '2 green, 0 red', 'build 2m10 · test 4m02') + card('Doc map', '3 docs, 1 phase', 'Holds spec §4 · covers 3 of 5 items') + card('Risk', '2 areas', 'Static clock in HoldService · no index on seat_id', 'att') + '</div></div></div>'
    return '<div class="dpane">' + o + '</div>'
def tpane(since=False, overview=False):
    o = '<aside class="pc">'
    if since:
        o += '<div class="ph" style="margin-bottom:0">Conversation<span class="n">3</span></div><div class="empty1" style="padding-top:0">Your 6 comments: 4 changed lines near the thread, 2 untouched.</div>'
        o += '<div class="th"><div class="tt"><span class="g msg"></span>HoldService.cs:41 <span class="st op">open</span></div><div class="tx">@lee: why a static clock?</div><div style="margin-top:6px;border:1px solid var(--hair-strong);border-radius:var(--r2);background:var(--s2);padding:6px 8px;font-size:12px;line-height:18px;color:var(--ink-muted)"><span class="tag pri">agent proposes: addressed</span><br>Pending reply, never posted: “Now injected through IClock in 1f3e9aa.”<div class="ac" style="display:flex;gap:6px;margin-top:6px"><span class="btn">Accept <kbd>y</kbd></span><span class="btn">Edit <kbd>e</kbd></span><span class="btn">Drop <kbd>d</kbd></span></div></div></div>'
        o += '<div class="th"><div class="tt"><span class="g msg"></span>HoldPolicy.cs:12 <span class="st">outdated</span></div><div class="tx">@sam: rename to MaxHoldMinutes.</div></div>'
    else: o += threads_block('normal') if not overview else threads_block('normal').replace('on this file first', '')
    o += findings_block('normal') if not since else '<div class="ph">Findings<span class="n">1</span></div><div class="empty1">1 finding on the commits since your review.</div>'
    o += '<div class="dc"><span>Draft: 2 comments</span><span class="sp"></span><span class="btn ghost">Submit… <kbd>Ctrl+Enter</kbd></span></div></aside>'
    return o
SINCE_FILES = None
def walk(w=1440, step='file', since=False, inline=False, popover=False, tree_dd=False, title='three-pane', tkw=None, sr='', extra='', center_extra='', dkw=None, hk=None, opening=True, center=None):
    tkw = tkw or {}
    hk = dict(hk or {})
    if since: hk.update(scope=True, since='Changes since your review of 9f8e7d6 (3 commits)')
    if inline: hk.update(chips=True)
    h = rhead(w=w, **hk)
    if center is not None: c = center
    elif step == 'overview': c = overview()
    else:
        inl = ('<div class="inl"><div class="who">@lee <span style="font-weight:400;color:var(--ink-subtle)">open · HoldService.cs:41</span></div><div class="tx">why a static clock?</div><div class="ac"><span class="btn">Reply <kbd>r</kbd></span><span class="btn">Resolve <kbd>Ctrl+⏎</kbd></span></div></div>') if inline else None
        c = f'<div class="dpane">{diffOpenEndpoint()}</div>' if opening else f'<div class="dpane">{diffA(inline=inl, **(dkw or {}))}</div>'
    if tree_dd:
        cols = 'minmax(0,1fr)'; inner = f'<div style="display:flex;align-items:center;gap:8px;height:34px;padding:0 var(--inset);border-bottom:1px solid var(--hair);background:var(--s1);font-size:12px;color:var(--ink-muted)"><span class="btn">Tree ▾ <kbd>[</kbd></span><span>HoldService.cs · file 3 of 8 in order</span></div>{c}'
        body = f'<div class="rbody" style="grid-template-columns:{cols}"><div class="dpane">{inner}</div></div>'
    elif inline:
        tw = 240
        body = f'<div class="rbody" style="grid-template-columns:{tw}px minmax(0,1fr)">{tree(**dict(dict(sel="Overview" if step=="overview" else ("HoldEndpoint.cs" if opening else "HoldService.cs")), **tkw))}{c}</div>'
    else:
        body = f'<div class="rbody" style="grid-template-columns:260px minmax(0,1fr) 360px">{tree(**dict(dict(sel="Overview" if step=="overview" else ("HoldEndpoint.cs" if opening else "HoldService.cs")), **tkw))}{c}{tpane(since, step == 'overview')}</div>'
    kb_ = keys([('j/k', 'file'), ('n', 'first file'), ('F6', 'region'), ('Ctrl+Enter', 'submit review'), ('?', 'all keys')], None) if step == 'overview' else keys(RKEYS + [], None)
    return shell(f'Reviews - {title}', w, 'rev', h, body, kb_, sr, extra)
WSR = 'Tree file: "HoldService.cs, core, 80 lines added, unread." Thread: "open thread, 2 comments, @lee, HoldService.cs line 41."'
write('walk-overview-1440', walk(1440, 'overview', title='three-pane Overview', sr='Overview step. Header reads the order source: "Order built from the code index at a1b2c3d". ' + WSR))
write('walk-1440', walk(1440, sr='Three-pane walk, 1440. ' + WSR))
write('walk-1200', walk(1200, inline=True, sr='1100 to 1439: threads inline under their line, tree 240, Findings and Draft chips open a popover. ' + WSR))
write('walk-1000', walk(1000, inline=True, tree_dd=True, sr='Below 1100: the tree is a drop-down, key [. Threads inline. ' + WSR))
pop = '<div class="pop" style="left:20px;top:118px;width:360px"><div class="mhead" style="padding:0 12px;font-size:11px;font-weight:500;color:var(--ink-subtle);height:22px;display:flex;align-items:center">Findings 4 · 2 new</div>' + findings_block('new').replace('<div class="ph">Findings<span class="n">4</span><span class="tag pri" style="margin-left:6px">2 new</span></div>', '') + '</div>'
write('walk-1280-popover', walk(1280, inline=True, extra=pop, sr='Header chip "Findings 4" opened a popover. Escape closes it and returns focus to the chip. ' + WSR))
write('walk-1200-thread', walk(1200, inline=True, opening=False, title='inline form, mid-walk', sr='1100 to 1439, mid-walk: the open thread draws under HoldService.cs line 41. ' + WSR))
write('since-review', walk(1440, since=True, opening=False, sr='Header reads: "Changes since your review of 9f8e7d6, 3 commits." Thread: "your 6 comments, 4 changed lines near, 2 untouched." Agent proposal reads "addressed, pending reply, accept, edit or drop."', tkw=dict(only=True, sel='HoldService.cs'), hk=dict(files='3 files since 9f8e7d6 +41 −6')))

# ---------------- sheets and popovers over layout A ----------------
def over(sheet_html, w=1440, **kw):
    return dict(extra='<div class="dim"></div>' + sheet_html, **kw)
def modal(title='Submit review on #3120', **kw):
    return surfA(1440, title='Harbor ferry: seat holds', pcls='', **kw)
def cm(at, tx, flag='', btns='<span class="btn">Edit</span><span class="btn">Remove</span>', cls=''):
    fg = f'<div class="fg">{flag}</div>' if flag else ''
    return f'<div class="cm {cls}"><div><div class="at">{at}</div><div class="tx">{tx}</div>{fg}</div><div class="bs">{btns}</div></div>'
def sheet(body_txt='Two small things: the static clock, the missing index.', comments=None, verdict='normal', banner='', sline=None, discard=True, bodyph=False, reason=''):
    comments = comments if comments is not None else ''
    dis = verdict in ('flagged', 'offline', 'moved')
    cdis = verdict == 'empty'
    dc = 'dis' if dis else ''
    cbtn = f'<span class="btn {"dis" if (dis or cdis) else "primary"} {"foc" if not (dis or cdis) else ""}">Comment</span>'
    ab = f'<span class="btn {dc}">Approve</span><span class="btn {dc or ("dis" if cdis else "")}">Request changes</span>'
    ta = f'<div class="ta2 ph foc">Add a body (optional)…</div>' if bodyph else f'<div class="ta2">{body_txt}</div>'
    n = comments.count('class="cm') if comments else 0
    sl = sline or f'<div class="sline"><span class="g done"></span>Required checks: 2 of 2 pass</div>'
    reason_h = f'<div class="sline" style="color:var(--att)">{reason}</div>' if reason else ''
    return f'''<aside class="sheet"><div class="sh"><h2>Submit review on #3120 · head <span class="mono">a1b2c3d</span></h2><div class="as">Posting as @joi</div></div>{banner}
<div class="sb"><div class="lab">Body</div>{ta}<div class="lab">{n} line comments</div>{comments or '<div style="color:var(--ink-subtle);font-size:12px">No line comments in the draft.</div>'}
<div class="sline"><span class="g draft"></span>4 findings not yet accepted or dropped. They never block the post.</div>{sl}{reason_h}</div>
<div class="sf">{'<span class="btn ghost">Discard draft</span>' if discard else ''}<span class="sp"></span>{cbtn}{ab}<span class="btn ghost">Cancel <kbd>Esc</kbd></span></div></aside>'''
SSR = 'Sheet reads: "Submit review on number 3120, head a1b2c3d. Posting as joi." '
KEPT = '<span style="color:var(--ink-subtle)">✓ from a finding, kept</span>'
FLAGGED = '⚠ from a finding, unedited'
c_find = lambda flag, btns: cm('HoldService.cs:41', 'Static clock; tests can’t pin it.', flag, btns, 'flag' if 'unedited' in flag else '')
c_typed = cm('0042_holds.sql:9', 'No index on seat_id. cc @priya, see <span class="lk">#3112</span>', '', '<span class="btn">Edit</span><span class="btn">Remove</span>')
NORMAL_C = c_find(KEPT, '<span class="btn">Edit</span><span class="btn">Remove</span>') + c_typed
write('submit-normal', modal(extra='<div class="dim"></div>' + sheet(comments=NORMAL_C), sr=SSR + 'Three verdict buttons: Comment, Approve, Request changes. Escape cancels. The typed comment with an at-mention and a link is not flagged.', kb=keys([('Tab', 'next'), ('Esc', 'cancel'), ('Enter', 'press')], None)))
write('submit-empty', modal(extra='<div class="dim"></div>' + sheet(comments='', bodyph=True, verdict='empty', reason='Comment is off: add a body or a comment first. Request changes needs a body.', sline='<div class="sline"><span class="g done"></span>Required checks: 2 of 2 pass</div>', discard=False), sr=SSR + 'Focus is in the Body field. Comment and Request changes are disabled: request changes needs a body. Approve can still post.', kb=keys([('Tab', 'next'), ('Esc', 'cancel')], None)))
write('submit-offline', modal(extra='<div class="dim"></div>' + sheet(comments=NORMAL_C, verdict='offline', banner='<div class="banner" style="background:var(--err-soft);flex-direction:column;align-items:flex-start;gap:2px;padding:10px var(--sp5)"><span>Offline: reconnect to post. Your draft is kept.</span>' + det('SND-REVIEW-021') + '</div>'), sr=SSR + 'All three verdicts disabled. Banner: "Offline: reconnect to post. Your draft is kept." The code is under Details.', kb=keys([('Tab', 'next'), ('Esc', 'cancel')], None)))
fl_c = c_find(FLAGGED, '<span class="btn foc">Keep</span><span class="btn">Edit</span><span class="btn">Remove</span>') + c_typed
write('submit-flagged', modal(extra='<div class="dim"></div>' + sheet(comments=fl_c, verdict='flagged', reason='Verdicts are off until the flagged comment gets Keep, Edit or Remove (1 left). Your own typed text is never flagged.'), sr=SSR + 'Flagged row reads: "from a finding, unedited. Keep, Edit, Remove." Verdicts disabled until it is cleared. The typed comment is not flagged.', kb=keys([('Tab', 'next'), ('Esc', 'cancel')], None)))
c2 = cm('HoldService.cs:41', 'Static clock; tests can’t pin it.', FLAGGED, '<span class="btn">Keep</span><span class="btn">Edit</span><span class="btn">Remove</span>', 'flag') + cm('HoldPolicy.cs:17', 'No cap on hold length.', FLAGGED, '<span class="btn">Keep</span><span class="btn">Edit</span><span class="btn">Remove</span>', 'flag')
keepall = '<div style="display:flex;align-items:center;gap:10px;margin:8px 0 2px"><span class="btn foc">Keep all (2)</span><span style="font-size:12px;color:var(--ink-subtle)">keeps every comment taken unedited from a finding</span></div>'
write('submit-flagged-two', modal(extra='<div class="dim"></div>' + sheet(comments=keepall + c2, verdict='flagged', reason='Verdicts are off until both flagged comments are kept, edited or removed (2 left).'), sr=SSR + 'Two flagged rows, one Keep all button above the list, focused. Verdicts disabled.', kb=keys([('Tab', 'next'), ('Esc', 'cancel')], None)))
mv = '<div class="banner" style="flex-direction:column;align-items:flex-start;gap:4px;padding:10px var(--sp5)"><span>The PR moved to <span class="mono">c4d5e6f</span>. Reload to see the new commits.</span><small>Kept: 2 comments. Outdated: 0. Viewed reset: 1 file (HoldService.cs).</small><span style="display:flex;gap:8px;align-items:center;margin-top:4px">' + btn('Reload', '', 'primary') + '<span class="mono" style="color:var(--ink-subtle);font-size:11px">SND-REVIEW-011</span></span></div>'
sh = sheet(comments=NORMAL_C, verdict='moved', banner=mv).replace('btn primary foc', 'btn')
write('submit-head-moved', modal(extra='<div class="dim"></div>' + sh, sr=SSR + 'Banner reads: "The PR moved to c4d5e6f. Kept 2 comments. Outdated 0. Viewed reset 1 file." Verdicts disabled. Button: Reload.', kb=keys([('Tab', 'next'), ('Esc', 'cancel')], None)))
disc = '<div class="toast" style="right:auto;left:24px"><span class="g linked"></span><span>Draft discarded. Viewed marks kept.</span><span class="bt"><span class="btn">Undo <kbd>Ctrl+Z</kbd></span></span><span class="life"></span></div>'
write('submit-discard-undo', surfA(1440, primary='Submit review', pk='Ctrl+Enter', pc=pcol(draft='No draft'), extra=disc, sr='Toast reads: "Draft discarded. Viewed marks kept. Button: Undo, Control Z. Holds 5 seconds." Draft counter reads "No draft".'))
# head moved banner on the surface
hb = '<div class="banner"><span class="g parked"></span><span>The PR moved to <span class="mono">c4d5e6f</span>. Reload to see the new commits. <span style="color:var(--ink-muted)">Kept: 2 comments. Outdated: 0. Viewed reset: 1 file (HoldService.cs).</span></span><span class="sp"></span>' + btn('Reload', '', 'primary') + '</div>'
write('head-moved-banner', surfA(1440, pcls='', banner=hb, sr='Banner reads: "The PR moved to c4d5e6f. Reload to see the new commits. Kept 2 comments, outdated 0, viewed reset 1 file." No auto reload. Reload keeps file, scroll, focus and order.'))
# merge picker
def mrg(state):
    conflict = state == 'conflict'
    ready = '<span class="dot">·</span><span class="tag err">Merge refused</span>' if conflict else '<span class="dot">·</span><span class="tag ok">Can merge</span>'
    items = [('Squash and merge', 'repo default'), ('Create a merge commit', ''), ('Rebase and merge', 'rewrites history')]
    dis = state in ('merging', 'checking')
    its = ''.join(f'<div class="it {"on" if i == 0 and not dis else ""} {"dis" if dis else ""}" style="{"opacity:.5" if dis else ""}"><span class="g {"linked" if i == 0 else "todo"}"></span><span>{a}</span><span class="w">{b}</span></div>' for i, (a, b) in enumerate(items))
    if dis: foot = '<div class="sf2"><span class="sp"></span><span class="btn dis">Cancel</span><span class="btn dis">Merge</span></div>'
    elif state == 'notmerged': foot = '<div class="sf2"><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="btn primary foc">Retry <kbd ' + KP + '>Enter</kbd></span></div>'
    else: foot = '<div class="sf2"><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="btn primary">Merge <kbd ' + KP + '>Enter</kbd></span></div>'
    st = ''
    if state == 'checking': st = '<div class="why" style="background:var(--s2)"><span class="g ci-run" style="vertical-align:-2px;margin-right:6px"></span>Checking whether it merged…</div>'
    if state == 'notmerged': st = '<div class="why">It didn’t merge.</div>'
    if state == 'merging': st = '<div class="why" style="background:var(--s2)"><span class="g ci-run" style="vertical-align:-2px;margin-right:6px"></span>Merging…</div>'
    if conflict: st = '<div class="why">Pull request is not mergeable: merge conflict between the head branch and main in <span class="mono">Pier/Schedule.cs</span>.' + det('SND-REVIEW-018') + '</div>'
    pop = f'<div class="pop" style="right:24px;top:84px;width:340px"><div class="ph" style="margin:0;height:24px">Merge #3109 into main</div>{its}{st}{foot}</div>'
    srs = {'idle': 'Picker reads: "Merge number 3109 into main. Squash and merge, repo default, selected. Merge commit. Rebase and merge, rewrites history. Buttons: Cancel, Merge."', 'merging': 'Picker reads: "Merging." Every method and both buttons are disabled until GitHub answers.', 'checking': 'Picker reads: "Checking whether it merged." Every method and both buttons are disabled until the check ends.', 'notmerged': 'Picker reads: "It didn’t merge. Button: Retry, focused."', 'conflict': 'Picker reads: "Pull request is not mergeable: merge conflict between the head branch and main." The picker stays open.'}
    kbs = keys([], None) if dis else keys([('Enter', 'retry'), ('Esc', 'close')], None) if state == 'notmerged' else keys([('↑↓', 'method'), ('Enter', 'merge'), ('Esc', 'close')], None)
    html = surfA(1440, title='Pier schedule: holiday rows', num=3109, who='you → main', sha='b7e2d41', files='2 files +64 −2', viewed='2/2 viewed', findings='Pre-review: not run', primary='Merge ▾', pk='', pcls='', ready=ready, order=False, conf='1 conflict' if conflict else '0 conflicts', inline=False,
                 pc=pcol(tstate='empty', fstate='empty', draft='No draft'), extra='<div class="dim"></div>' + pop, sr=srs[state], kb=kbs)
    for a, b in (('HoldEndpoint', 'PierEndpoint'), ('HoldRequest', 'HolidayRow'), ('HoldService', 'PierSchedule'), ('HoldPolicy', 'HolidayPolicy'), ('0042_holds.sql', '0017_holidays.sql'), ('Hold Place(Seat s)', 'Row Add(Day d)'), ('src/Booking/Holds', 'src/Pier/Schedule'), ('tests/Booking', 'tests/Pier'), ('holds.md', 'schedule.md'), ('owner @ada · +80 −12', 'owner you · +64 −2'), ('SeatTaken(s.Id)', 'DayClosed(d.Id)'), ('s.IsTaken', 'd.IsClosed')):
        html = html.replace(a, b)
    import re as _re
    html = _re.sub(r'<div class="fnd">.*?</div>', '', html)
    for a, b in (('Clock.Now.AddMinutes(10)', 'Calendar.Today.AddDays(1)'), ('new Hold(s.Id)', 'new Row(d.Id)'), ('var until', 'var next'), ('var hold = new Hold(s.Id, until)', 'var row = new Row(d.Id, next)'), ('Add(hold)', 'Add(row)'), ('Clock.Now', 'Calendar.Today'), ('Expired(Hold h)', 'Expired(Row r)'), ('h.Until', 'r.Until'), ('IClock', 'ICalendar')):
        html = html.replace(a, b)
    return html
write('merge-picker', mrg('idle')); write('merge-merging', mrg('merging')); write('merge-conflict', mrg('conflict')); write('merge-checking', mrg('checking')); write('merge-not-merged', mrg('notmerged'))
# palette
def pal(query, body, foot):
    return ('<div class="dim"></div><div class="pal"><div class="in"><span class="typed">' + query + '</span><span class="caret"></span></div>' + body + '<div class="ft">' + foot + '</div></div>')
PAL = pal('hold', '<div class="ph2">Files</div><div class="it on"><span class="g note"></span><span><mark>Hold</mark>Service.cs</span><span class="w">file 3 of 8</span></div><div class="it"><span class="g note"></span><span><mark>Hold</mark>Policy.cs</span><span class="w">file 4 of 8</span></div><div class="it"><span class="g note"></span><span><mark>Hold</mark>Endpoint.cs</span><span class="w">file 1 of 8 · viewed</span></div>'
  '<div class="ph2">Hunks</div><div class="it"><span class="g draft"></span><span><mark>Hold</mark>Service.cs · Place(Seat s)</span><span class="w">line 38</span></div>', '<span><kbd>Enter</kbd>go</span><span><kbd>↑↓</kbd>move</span><span><kbd>Esc</kbd>close</span>')
PAL0 = pal('zzq', '<div class="empty1" style="padding:18px 22px;display:flex;align-items:center;gap:12px;font-size:13px;color:var(--ink-muted)">No match for “zzq”<span style="margin-left:auto" class="btn">Clear <kbd>Esc</kbd></span></div>', '<span><kbd>Esc</kbd>clear</span>')
write('palette-no-match', surfA(1440, pcls='', extra=PAL0, sr='Palette reads: "No match for zzq. Button: Clear."', kb=keys([('Esc', 'clear')], None)))
write('palette', surfA(1440, pcls='', extra=PAL, sr='Palette reads: "hold, 3 files, 1 hunk. Selected: HoldService.cs, file 3 of 8." The hunk list is the screen reader jump list.', kb=keys([('↑↓', 'move'), ('Enter', 'go'), ('Esc', 'close')], None)))
# reviewer picker
def rv(state):
    people = [('ada', 'Ada Okafor · the author, can’t review', 'dis', 'on0'), ('@harbor-api', 'team', '', ''), ('lee', 'Lee Tran · requested', '', 'on'), ('sam', 'Sam Reyes', '', ''), ('priya', 'Priya Nair · reviewed at 9f8e7d6', '', '')]
    its = ''.join(f'<div class="it {d}"><span class="ck {"on" if o == "on" else ""}"></span><span>{n}</span><span class="w">{w}</span></div>' for n, w, d, o in people)
    if state == 'main': mf, body, foot = '<div class="mf focus">Search reviewers<span class="caret"></span></div>', its, '<span class="btn primary">Request</span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>'
    if state == 'loading': mf, body, foot = '<div class="mf focus">Search reviewers<span class="caret"></span></div>', '<div class="it dis"><span class="g ci-run"></span><span>Loading reviewers…</span><span></span></div>', '<span class="btn dis">Request</span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>'
    if state == 'nomatch': mf, body, foot = '<div class="mf focus typed">zz<span class="caret"></span></div>', '<div class="it dis"><span></span><span>No match for “zz”</span><span class="lk">Clear</span></div>', '<span class="btn dis">Request</span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>'
    if state == 'error': mf, body, foot = '<div class="mf">Search reviewers</div>', '<div class="why">Couldn’t load reviewers: GitHub answered 502.' + det('SND-REVIEW-019') + '</div>', '<span class="btn">Retry</span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>'
    if state == 'requesting': mf, body, foot = '<div class="mf">Search reviewers</div>', its.replace('class="it ', 'class="it dis ', 3), '<span class="btn dis">Requesting…</span><span class="btn dis">Cancel</span>'
    return f'<div class="pop" style="width:100%;position:relative;box-shadow:none"><div class="ph" style="margin:0;height:24px">Request review on #3120</div>{mf}{body}<div class="sf2"><span class="sp"></span>{foot}</div></div>'
def anchored(state):
    return rv(state).replace('style="width:100%;position:relative;box-shadow:none"', 'style="right:24px;top:84px;width:340px"')
write('reviewer-picker', surfA(1440, pcls='', extra='<div class="dim"></div>' + anchored('main'), sr='Picker reads: "Request review on number 3120. Search field, focused. lee, selected. Space toggles a name. Button: Request." Focus starts in the search field.', kb=keys([('↓', 'names'), ('Space', 'toggle on a name'), ('Tab', 'to Request'), ('Esc', 'close')], None)))

# ---------------- composer ----------------
cmp1 = ('<div class="cmp"><div class="ta">Can we inject IClock here? A static clock makes the 10-minute hold hard to pin in a test.<span class="caret"></span></div>'
        '<div class="tb2"><span>Line 41 · HoldService.cs · goes to the draft, not to GitHub</span><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>' + btn('Add to draft', 'Ctrl+Enter', 'primary') + '</div></div>')
write('composer', surfA(1440, pcls='', composer=cmp1, cur=41, sr='Composer reads: "Comment on line 41, HoldService.cs. Text field. Add to draft, Control Enter. Goes to the draft, not to GitHub." The draft file is written as you type.', kb=keys([('Ctrl+Enter', 'add to draft'), ('Esc', 'close'), ('Ctrl+Z', 'text undo')], None)))
cmp2 = ('<div class="cmp"><div class="ta">Use the injected clock:<span class="caret"></span></div><div class="sg"><div class="r">−        var until = Clock.Now.AddMinutes(10);</div><div class="a">+        var until = _clock.Now.AddMinutes(10);</div></div>'
        '<div class="tb2"><span>Suggestion for line 41 only · Sindri copies the patch, it never commits to a branch you don’t own</span><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>' + btn('Add to draft', 'Ctrl+Enter', 'primary') + '</div></div>')
write('composer-suggestion', surfA(1440, pcls='', composer=cmp2, cur=41, sr='Composer with a suggestion block for line 41. Release 1 suggests one line only.', kb=keys([('Ctrl+Enter', 'add to draft'), ('Esc', 'close'), ('Ctrl+Z', 'text undo')], None)))
# tree failing + 50 of 300
write('tree-failing-50of300', walk(1440, title='tree, failing file, 50 of 300', tkw=dict(fail='HoldPolicy.cs', more=True), hk=dict(files='300 files +5,210 −1,840', viewed='5/300 viewed', findings='Pre-review: not run (over 60 files)'), sr='Tree row reads: "HoldPolicy.cs, couldn’t read." Last row reads: "showing 50 of 300, load 50 more."'))

# ---------------- state sheets ----------------
def sheetpage(title, cls, panels, sr, w=1440, hdr=None):
    body = f'<div class="ss {cls}">{panels}</div>'
    return shell(title, w, 'rev', hdr or '<div class="rhead" style="justify-content:center"><div class="l1"><span class="ttl">' + title.replace('Reviews - ', '') + '</span><span class="meta">States sheet · every panel is one state of the same control</span></div></div>', body, keys([('F6', 'region'), ('?', 'all keys')], None), sr)
def panel(t, k, inner, pad=False, h=None):
    return f'<div class="panel"{(" style=height:" + h) if h else ""}><div class="pt">{t}<span class="k">{k}</span></div><div class="pb {"pad" if pad else ""}">{inner}</div></div>'
def pcw(inner): return f'<div class="pc" style="width:100%;border-left:0;height:100%">{inner}</div>'
write('threads-states', sheetpage('Reviews - threads states', 'c3', panel('Threads: empty', 'No threads', pcw(threads_block('empty'))) + panel('Threads: error', 'Couldn’t load threads', pcw(threads_block('error'))) + panel('Threads: failed resolve inline', 'SND-REVIEW-019', pcw(threads_block('019'))), 'Empty reads "No threads". Error reads "Couldn’t load threads: GitHub answered 502, retry". A failed resolve reads inside the thread: "Couldn’t resolve, GitHub refused it."'))
fnd_fail = '<div class="ph">Findings</div><div class="th err"><div class="tt"><span class="g fail"></span>Pre-review failed</div><div class="e">The run hit the model limit after 4 minutes.' + det('SND-REVIEW-017') + '</div><div class="ac"><span class="btn">Run again</span></div></div>'
write('findings-states', sheetpage('Reviews - findings states', 'c2', panel('Findings: stale', 'prereview-stale', pcw(findings_block('stale'))) + panel('Findings: partial', 'run unfinished', pcw(findings_block('partial'))) + panel('Findings: “2 new” chip', 'never reorders the list', pcw(findings_block('new'))) + panel('Findings: empty and failed', 'run one / failed', pcw(findings_block('empty') + fnd_fail)), 'Stale finding: "stale, accept disabled." Chip reads "2 new findings". Partial reads "run unfinished."'))
tpan = lambda **k: f'<div style="height:100%;display:flex">{tree(**k).replace("<aside class=\"tree\"", "<aside class=\"tree\" style=\"width:100%;border-right:0\"")}</div>'
write('tree-states', sheetpage('Reviews - tree states', 'c2', panel('Tree: a file that failed', 'one file, the rest load', tpan(fail='HoldPolicy.cs')) + panel('Tree: showing 50 of 300', 'load-more row', tpan(more=True)) + panel('Tree: Order unavailable', 'Files mode shown, never an invented order', tpan(unavailable='the code index is not built for this repo')) + panel('Tree: ordering', 'after 300 ms', tpan(ordering=True)), 'Failing file reads: "HoldPolicy.cs, couldn’t read." Load-more reads: "showing 50 of 300."'))
# reviewer states
write('reviewer-states', sheetpage('Reviews - reviewer picker states', 'c2', panel('Loading reviewers', 'past 300 ms', f'<div style="padding:16px;position:relative;height:100%">{rv("loading")}</div>') + panel('No match', '“No match for zz” and Clear', f'<div style="padding:16px">{rv("nomatch")}</div>') + panel('Couldn’t load reviewers', 'Retry, SND-REVIEW-019', f'<div style="padding:16px">{rv("error")}</div>') + panel('Requesting…', 'buttons off', f'<div style="padding:16px">{rv("requesting")}</div>'), 'Loading reads "Loading reviewers". No match reads "No match for zz, Clear". Error reads "Couldn’t load reviewers, Retry."'))
# focus order
def fo(items): return '<div class="fo">' + ''.join(f'<div class="st"><span class="num">{i}</span>{t}</div>' for i, t in items) + '</div>'
mp = panel('Merge picker', 'Esc returns to the Merge button', '<div style="padding:14px 16px">' + fo([(1, 'Squash and merge (the repo default, focus starts here)'), (2, 'Create a merge commit'), (3, 'Rebase and merge'), (4, 'Cancel'), (5, 'Merge (primary)')]) + '<p class="note2">Tab follows the footer. When the default is rebase, focus starts on 4, Cancel: rebase rewrites history. After Merged, focus lands on the queue cursor row. After a refusal it stays in the picker.</p></div>')
rp = panel('Reviewer picker', 'Esc or success returns to the opener', '<div style="padding:14px 16px">' + fo([(1, 'Search field (focus starts here, never on a name)'), (2, 'Names: ↑↓ move, Space toggles'), (3, 'Request'), (4, 'Cancel')]) + '<p class="note2">Request is its own button. Enter in the field never requests.</p></div>')
pp = panel('Review surface', 'F6 cycles regions', '<div style="padding:14px 16px">' + fo([(1, 'Header'), (2, 'File list or tree'), (3, 'Diff'), (4, 'Panel column or threads'), (5, 'Draft counter')]) + '<p class="note2">After a composer closes, focus returns to the line. After a finding is accepted, it moves to the next finding. After Posted, it lands on the queue cursor row.</p></div>')
write('focus-order', sheetpage('Reviews - focus order', 'c3', mp + rp + pp, 'Each stop is named: "Squash and merge, selected, 1 of 3"; then "Cancel, button, 4 of 5" and "Merge, button, 5 of 5". Escape closes and returns focus to the control that opened the picker.'))
# focus rings
fr_row = '<div class="list" style="padding:8px 0">' + qrow('need', 3120, 'Harbor ferry: seat holds', 'asked of you', '', 310, 42, 'CI green', 'ok', 'ada', '1h40', 'unread cursor') + qrow('need', 3127, 'Harbor ferry: fare table', 'asked of @harbor-api', '', 88, 9, 'CI red', 'err', 'lee', '3h', 'unread') + '</div>'
fr_file = '<div style="padding:12px 16px;width:420px">' + tf('✓', 'HoldEndpoint.cs', 'entry', 18, 2, '', 'v') + tf('▸', 'HoldService.cs', 'core', 80, 12, 'fx sel') + tf('▸', 'HoldPolicy.cs', 'core', 22, 3) + '</div>'
fr_line = '<div class="diff" style="padding:8px 0">' + dl('c', 40, 40, '        if (s.IsTaken) throw new SeatTaken(s.Id);') + dl('a', '', 41, '        var until = Clock.Now.AddMinutes(10);', cur=True) + dl('a', '', 42, '        var hold = new Hold(s.Id, until);') + '</div>'
fr_btn = '<div style="padding:16px;display:flex;gap:16px;align-items:center;flex-wrap:wrap">' + btn('Submit review (2)', '', 'primary foc') + btn('Reply', 'r', 'foc') + btn('Cancel', '', 'ghost foc') + '<span class="seg foc"><span class="on">Order</span><span>Files</span></span></div><div style="padding:0 16px;font-size:12px;color:var(--ink-subtle)">Buttons and toggles: a 2 px primary-hi outline, 1 px off the edge.</div>'
write('focus-rings', sheetpage('Reviews - focus rings', 'c2', panel('Queue row', 'inset 1 px primary-hi ring (the grammar’s cursor)', fr_row) + panel('File in the list or tree', 'same inset ring, row stays selected', fr_file) + panel('Diff line', 'ring plus a 3 px gutter bar, so it shows on a tinted line', fr_line) + panel('Buttons and toggles', 'outline 2 px, offset 1 px', fr_btn), 'Row: "Needs you, number 3120 …, cursor." File: "HoldService.cs, core, 80 lines added, unread." Line: "line 41, added: …, cursor."'))

# ---------------- hover / pressed states sheet ----------------
def cellrow(name, items): return f'<div class="cell"><span class="nm">{name}</span>' + ''.join(f'<span>{i}</span>' for i in items) + '</div>'
def b3(label, k, base): return [btn(label, k, base), btn(label, k, base + ' hv2'), btn(label, k, base + ' pr2'), btn(label, k, base + ' foc'), btn(label, k, base + ' dis')]
rows = '<div class="cell hd"><span>Control</span><span>Rest</span><span>Hover</span><span>Pressed</span><span>Focus</span><span>Disabled</span></div>'
rows += cellrow('Primary button', b3('Submit review', '', 'primary'))
rows += cellrow('Secondary button', b3('Reply', 'r', ''))
rows += cellrow('Ghost button', b3('Cancel', '', 'ghost'))
rowmini = lambda extra: f'<div class="row rv read {extra}" style="grid-template-columns:16px 1fr 60px;padding:0 8px;height:28px;width:150px"><span class="g need"></span><span class="title"><span class="t">#3120 seat holds</span></span><span class="age">1h40</span></div>'
rows += cellrow('Queue row', [rowmini(''), rowmini('hv'), rowmini('').replace('class="row', 'style2="" class="row').replace('height:28px', 'height:28px;background:#222326;box-shadow:inset 0 0 0 1px var(--hair-3);transform:scale(.98)'), rowmini('cursor'), '<span style="opacity:.4">' + rowmini('') + '</span>'])
tfm = lambda c, st='': f'<div style="width:150px">' + tf('▸', 'HoldService.cs', 'core', 80, 12, c).replace('class="tf', f'style="{st}" class="tf', 1) + '</div>'
rows += cellrow('File in tree', [tfm(''), tfm('', 'background:#1c1d20;box-shadow:inset 0 0 0 1px var(--hair-strong)'), tfm('', 'background:#222326;box-shadow:inset 0 0 0 1px var(--hair-3);transform:scale(.98)'), tfm('fx'), '<span style="opacity:.4">' + tfm('') + '</span>'])
gut = lambda bg, vis: f'<span style="display:inline-block;width:150px;height:20px;background:var(--ok-soft);position:relative;font:400 12px/20px var(--mono);color:var(--ink)"><span style="position:absolute;left:6px;top:3px;width:14px;height:14px;border-radius:3px;background:{bg};color:#fff;font:600 12px/14px var(--font);text-align:center;opacity:{vis}">+</span><span style="padding-left:28px">var hold = …</span></span>'
rows += cellrow('Diff gutter “+”', [gut('var(--primary)', 0), gut('#535fc6', 1), gut('var(--primary)', 1).replace('var(--primary)', 'var(--primary)').replace('var(--primary)', '#4c57be').replace('opacity:1', 'opacity:1;transform:scale(.98)'), gut('var(--primary-hi)', 1), '<span style="opacity:.4">' + gut('var(--primary)', 0) + '</span>'])
seg = lambda st: f'<span class="seg"><span class="on">Order</span><span style="{st}">Files</span></span>'
rows += cellrow('Order / Files toggle', [seg(''), seg('background:#1c1d20;color:var(--ink)'), seg('background:#222326;color:var(--ink);box-shadow:inset 0 0 0 1px var(--hair-3)'), '<span class="foc">' + seg('') + '</span>', '<span style="opacity:.4">' + seg('') + '</span>'])
ck = lambda st, on='': f'<span class="vbox {on}"><i style="{st}"></i>viewed</span>'
rows += cellrow('Viewed box', [ck(''), ck('border-color:var(--ink-subtle)'), ck('background:#222326;border-color:var(--hair-3);transform:scale(.98)'), ck('outline:2px solid var(--primary-hi);outline-offset:1px'), '<span style="opacity:.4">' + ck('') + '</span>'])
mi = lambda c: f'<span class="pop" style="position:relative;display:block;width:150px;padding:2px 0;border:0;box-shadow:none;background:transparent"><span class="it {c}" style="margin:0;height:28px"><span></span><span>Resolve</span><span></span></span></span>'
rows += cellrow('Menu item', [mi(''), mi('on'), mi('on').replace('class="it on"', 'class="it" style2=""').replace('margin:0;', 'margin:0;background:var(--s3);color:var(--ink);'), mi('on').replace('margin:0;', 'margin:0;box-shadow:inset 0 0 0 1px var(--primary-hi);'), mi('dis')])
write('states-hover-pressed', sheetpage('Reviews - hover and pressed states', 'c1', f'<div class="panel"><div class="pt">Every control: rest, hover, pressed, focus, disabled<span class="k">pressed = #222326 + tertiary border, scale 0.98</span></div><div class="pb pad">{rows}<p class="note2">These are the app’s own values (Theme/ClickableStyles.axaml, Tokens.axaml): hover fill #1c1d20 with the strong hairline; pressed fill #222326 with the tertiary hairline and the surface scales to 0.98 about its centre. Primary: hover #535fc6, pressed #4c57be, also scaled. Colours ease over Motion.Hover, zero under reduced motion. Focus is the ring on the other frames.</p></div></div>', 'Each control keeps its name in every state. Disabled controls stay readable and say why in text next to them.'))
# ---------------- motion ----------------
def tlrow(name, what, ms, scale=1.6):
    bar = f'<span class="tl"><i style="width:{int(ms*scale)}px"></i>&nbsp;{ms} ms</span>' if ms else '<span class="tl none"><i></i>&nbsp;0 ms, appears at once</span>'
    return f'<div class="cell" style="grid-template-columns:150px 1.4fr 1fr"><span class="nm">{name}</span><span>{what}</span>{bar}</div>'
def motion(reduced):
    ms = 0 if reduced else 120
    w = lambda a, b: a if not reduced else b
    rows = '<div class="cell hd" style="grid-template-columns:150px 1.4fr 1fr"><span>Thing</span><span>What changes</span><span>Time</span></div>'
    rows += tlrow('Toast in and out', w('fades in and rises 4 px; out: fades', 'shown and hidden, no fade, no rise'), ms)
    rows += tlrow('Fold (section, panel)', w('height eases, chevron turns 90 deg', 'height jumps, chevron flips'), ms)
    rows += tlrow('Sheet and picker', w('slides in 24 px from its edge, scrim fades', 'appears in place, scrim at once'), ms)
    rows += tlrow('Scroll to anchor', w('keeps its own ease, under 100 ms to the line', 'jumps to the line, no scroll animation'), 0 if reduced else 100)
    rows += tlrow('Spinner (Merging, Syncing)', w('one turn per second', 'stands still, the word “Merging…” carries the state'), ms)
    pic = ''.join(f'<span class="mini"><b style="left:10px;right:10px;bottom:{8 + (0 if reduced else d)}px;height:14px;opacity:{1 if reduced else o}"></b></span>' for d, o in ((4, .0), (2, .5), (0, 1)))
    cap = ('<p class="note2">Toast at 0, 60 and 120 ms:</p>' if not reduced else '<p class="note2">Toast at 0 ms and at 120 ms (the same frame):</p>') + ('<div>' + pic + '</div>' if not reduced else '<div><span class="mini"><b style="left:10px;right:10px;bottom:8px;height:14px"></b></span></div><p class="note2">One frame: the toast is there at 0 ms, and leaves after 5 s with no fade.</p>')
    return f'<div class="panel"><div class="pt">{"Reduced motion on" if reduced else "Motion"}<span class="k">{"the OS setting; Sindri follows it" if reduced else "toasts, folds and sheets: 120 ms"}</span></div><div class="pb pad">{rows}{cap}<p class="note2">Toasts hold 5 s either way; the 5 s bar is a static fill under reduced motion, since a shrinking bar is motion. Nothing is decided by motion alone: each outcome is also text a screen reader can focus.</p></div></div>'
write('motion', sheetpage('Reviews - motion', 'c1', motion(False), 'Toast, fold and sheet each animate for 120 ms. Every outcome is also focusable text.'))
write('reduced-motion', sheetpage('Reviews - reduced motion', 'c1', motion(True), 'With reduced motion on, nothing animates: toasts and sheets appear at once and scroll-to-anchor jumps.'))
# ---------------- ready line ----------------
rl_rows = '<div class="list" style="padding:6px 0">' + qrow('ready', 3109, 'Pier schedule: holiday rows', 'Can merge', 'ok', 64, 2, 'CI green', 'ok', 'you', '2d', 'read wide') + qrow('wait', 3115, 'Pier schedule: night ferry', 'build failing, required', 'att', 31, 5, 'CI red', 'err', 'you', '1d', 'read wide') + qrow('wait', 3116, 'Quay lights: map', 'Checking mergeability', '', 12, 1, 'CI green', 'ok', 'you', '3h', 'read wide') + '</div>'
rl_peek = '<div style="padding:14px 16px"><div class="sec" style="font-size:11px;color:var(--ink-subtle);font-weight:500">Checks</div><div class="chk" style="display:flex;gap:6px;margin:4px 0 8px"><span class="chip ok">test</span><span class="chip ok">format</span><span class="chip err">build</span></div><div class="rl" style="margin-top:0"><span class="g fail"></span><span class="wt" style="color:var(--att);font-weight:500">Waiting on: required check <span class="mono">build</span></span></div><div class="rl"><span class="g ready"></span><span class="ok" style="color:var(--ok);font-weight:500">Can merge</span><span style="color:var(--ink-subtle)">required checks 2 of 2, no open thread, approved</span></div></div>'
rl_head = '<div style="padding:8px 16px">' + rhead(title='Pier schedule: night ferry', num=3115, who='you → main', sha='c91a0e2', files='2 files +31 −5', viewed='2/2 viewed', findings='Pre-review: not run', ci='CI red', req='Required 1 of 2', primary='Merge ▾', pk='', pcls='dis', order=None, ready='<span class="dot">·</span><span class="tag att">Waiting on: required check build</span>').replace('class="rhead"', 'class="rhead" style="border:0;padding:0"') + '</div>' + '<div style="padding:8px 16px">' + rhead(title='Pier schedule: holiday rows', num=3109, who='you → main', sha='b7e2d41', files='2 files +64 −2', viewed='2/2 viewed', findings='Pre-review: not run', primary='Merge ▾', pk='', pcls='primary', order=None, ready='<span class="dot">·</span><span class="tag ok">Can merge</span>').replace('class="rhead"', 'class="rhead" style="border:0;padding:0"') + '</div>'
write('ready-line', sheetpage('Reviews - the ready line', 'c1', f'<div style="display:grid;grid-template-rows:auto auto auto;gap:16px;height:100%">' + panel('On the queue row (reason chip)', 'blank-safe: never “-”', rl_rows) + panel('In the peek', 'one phrase, from checks, threads and decision', rl_peek) + panel('In the review header', 'tag names what is missing; Merge is primary only when ready', rl_head) + '</div>', 'Row reads: "Ready, number 3109, … Can merge." A blocked row reads: "Waiting on: required check build."'))
# ================= UX pass (spec 8a6d74c200): first run, states, menus =================
def det_open(lines): return '<details class="details" open><summary>Details</summary>' + ''.join(f'<code>{l}</code>' for l in lines) + '</details>'
FIELD_DOTS = '<div class="field {f}" style="width:420px"><span style="letter-spacing:2px">{t}</span><span class="caret"></span><span style="margin-left:auto;font-family:var(--font);color:var(--ink-subtle)">Show</span></div>'
def tokcard(h, field_f='focus', field_t='••••••••••••••••••••', btn_html=None, msg='', extra_btn='', details=None, ro=False, sr='', name=None, kbs=None):
    btn_ = btn_html or btn('Save and connect', 'Enter', 'primary')
    fld = FIELD_DOTS.format(f=field_f, t=field_t).replace('class="field ', 'class="field ' + ('' if not ro else 'busy ') )
    steps = ('<div class="guide"><div class="step"><span class="no">1</span><span class="say">Make a fine-grained token: Pull requests, Contents and Actions, read and write.</span>' + btn('Open github.com') + '</div>'
             '<div class="step now" style="align-items:start;padding:6px 0"><span class="no">2</span><span class="say" style="display:flex;flex-direction:column;gap:8px">Paste it here. It is stored in your keychain, never in a file you read.'
             + (f'<span style="color:var(--err);font-size:12px">{msg}</span>' if msg else '') + fld + '</span><span style="display:flex;gap:8px;align-items:center;padding-top:30px">' + extra_btn + btn_ + '</span></div></div>')
    body = empty('linked', h, 'Reviews needs your own token to read and post your pull requests. About 2 minutes, once.', '', details or '') + steps
    return body
def frfr(name, **kw):
    sr = kw.pop('sr', '')
    write(name, qpage('Reviews - ' + name, tokcard(kw.pop('h', 'Connect GitHub to review here'), **kw), head=qhead(count='', asof='GitHub not connected'), base=[('Tab', 'next'), ('Enter', 'press'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], sr=sr, ncount=None, donelist=False))
OSLINE = ['secret-tool store --label=sindri-review service sindri-review', 'sindri review connect']
frfr('queue-first-run', details=det_open(OSLINE), sr='Connect GitHub to review here, two steps. Step 1: Open github.com. Step 2: Paste token, masked, with a Show toggle, focused. Button: Save and connect. Details: the keychain command and sindri review connect.')
frfr('first-run-connecting', field_f='', btn_html=btn('Connecting…', '', 'primary dis'), ro=True, sr='Connecting. The button reads Connecting, disabled. The token field is read-only.')
frfr('first-run-refused', field_t='', msg='GitHub refused this token: Bad credentials. Paste a new one.' , details='<details class="details"><summary>Details</summary><code>SND-REVIEW-009</code></details>', sr='GitHub refused this token: Bad credentials. Paste a new one. The field is cleared and focused. Details: SND-REVIEW-009.')
frfr('first-run-org-approval', field_t='', msg='Your org must approve this token first: SAML SSO authorization is required for acme.', extra_btn=btn('Open github.com'), sr='Your org must approve this token first. Buttons: Open github.com, Save and connect. The field is cleared and focused.')
frfr('first-run-store-failed', field_t='', msg='Couldn’t store the token: the keychain is locked. Nothing was saved.', details='<details class="details"><summary>Details</summary><code>SND-REVIEW-025</code></details>', sr='Couldn’t store the token: the keychain is locked. Nothing was saved. Details: SND-REVIEW-025.')
# watch repos
REPOS = ['acme/harbor', 'acme/harbor-web', 'acme/ferry-ops', 'acme/billing', 'acme/quay-lights', 'acme/pier-schedule', 'acme/booking-export', 'acme/infra', 'acme/design-tokens', 'acme/docs', 'acme/status-page', 'acme/sandbox']
def watchcard(ticked):
    o = '<div class="empty" style="max-width:640px;padding-bottom:var(--sp3)"><h2>Watch these repos</h2><p>Reviews reads pull requests from the repos you tick. This token can read 12.</p></div>'
    o += '<div style="padding:0 var(--inset) 8px"><div class="field" style="width:360px;font-family:var(--font)"><span style="color:var(--ink-subtle)">Filter 12 repos</span></div></div>'
    for i, rp in enumerate(REPOS):
        on = i in ticked
        o += f'<div class="row read{" cursor" if i == 0 else ""}" style="grid-template-columns:var(--col-glyph) minmax(200px,1fr) 120px"><span class="g box{" on" if on else ""}"></span><span class="title"><span class="t">{rp}</span></span><span class="fact">pushed {i + 1}d ago</span></div>'
    n = len(ticked)
    o += f'<div class="actionbar"><span>{n} ticked</span><span class="spacer"></span>' + (btn('Save', 'Enter', 'primary') if n else btn('Save', '', 'primary dis')) + '</div>'
    return o
write('watch-repos', qpage('Reviews - watch these repos', watchcard(set()), head=qhead(count='', asof='token works'), base=[('j/k', 'move'), ('Space', 'toggle'), ('/', 'filter'), ('Tab', 'to Save')], kb=[], sr='Watch these repos. 12 repos, none ticked, focus on row 1. Save is disabled until a box is ticked.', ncount=None, donelist=False))
write('watch-repos-ticked', qpage('Reviews - watch these repos, 3 ticked', watchcard({0, 2, 3}), head=qhead(count='', asof='token works'), base=[('j/k', 'move'), ('Space', 'toggle'), ('/', 'filter'), ('Tab', 'to Save')], kb=[], sr='Watch these repos. 3 repos ticked: harbor, ferry-ops, billing. Save is enabled.', ncount=None, donelist=False))
write('watch-repos-none', qpage('Reviews - token sees no repo', empty('fail', 'This token can’t see any repo', 'Give it repo access on github.com, then check again.', btn('Open github.com', '', 'primary')), head=qhead(count='', asof='token works'), base=[('Enter', 'open github.com'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], sr='This token can’t see any repo. Give it repo access on github.com. Button: Open github.com.', ncount=None, donelist=False))
# start sindri
SD_DET = '<details class="details" open><summary>Details</summary><code>log: ~/.agentic-workflow/sindri-app/serve.log</code><code>sindri serve</code></details>'
def sdpage(name, h, p, acts, extra, sr):
    write(name, qpage('Reviews - ' + name, empty('dn', h, p, acts, extra), head=qhead(count='', asof='not live'), base=[('Enter', 'press'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], sr=sr, ncount=None, donelist=False).replace('class="status-pill">Connected', 'class="status-pill off">Not running'))
sdpage('queue-serve-down-nocache', 'Sindri is not running', 'Reviews reads from Sindri, and nothing is stored yet on this box. Start Sindri to read your pull requests.', btn('Start Sindri', 'Enter', 'primary foc'), SD_DET, 'Page reads: "Sindri is not running. Button: Start Sindri, focused. Details: the log path and sindri serve." The tab shows no count.')
sdpage('start-sindri-starting', 'Sindri is not running', 'Starting Sindri…', btn('Starting Sindri…', '', 'primary dis'), SD_DET, 'Starting Sindri. The button is disabled while the app polls sindri status, up to 30 seconds.')
sdpage('start-sindri-failed', 'Sindri is not running', 'Sindri didn’t start: address already in use (port 7475).', btn('Retry', 'Enter', 'primary foc'), SD_DET, 'Sindri didn’t start: address already in use. Button: Retry, focused. Details: the log path and sindri serve.')

# ---- queue states (MUST 2) and Done / snooze (MUST 5)
def skrow(n=8):
    return ''.join('<div class="row read" style="grid-template-columns:var(--col-glyph) minmax(200px,1fr) 160px 88px 72px 44px"><span class="g todo" style="opacity:.3"></span><span class="title"><span style="display:block;width:%dpx;height:8px;border-radius:2px;background:var(--s2)"></span></span><span class="fact"><i style="display:block;width:90px;height:8px;border-radius:2px;background:var(--s2)"></i></span><span class="fact"><i style="display:block;width:50px;height:8px;border-radius:2px;background:var(--s2)"></i></span><span class="fact"><i style="display:block;width:44px;height:8px;border-radius:2px;background:var(--s2)"></i></span><span class="age"></span></div>' % (260 + (i * 47) % 220) for i in range(n))
write('queue-loading', qpage('Reviews - loading', grp('Needs your review', '') + skrow(3) + grp('New commits since you reviewed', '') + skrow(1) + grp('Returned to you', '') + skrow(1) + grp('Waiting on others', '') + skrow(1) + grp('Ready to merge', '') + skrow(2), head=qhead(count='', asof='Reading…'), base=[('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], sr='Loading: 8 skeleton rows under the section headers. The header reads Reading. The tab shows no count.', ncount=None, donelist=False))
WAITING = qrow('wait', 3140, 'Booking export: retry cap', 'review pending', '', 66, 12, 'CI green', 'ok', 'you', '1d') + qrow('wait', 3141, 'Quay lights: map tiles', 'review pending', '', 20, 3, 'CI green', 'ok', 'you', '2d')
READYR = qrow('ready', 3109, 'Pier schedule: holiday rows', 'Can merge', 'ok', 64, 2, 'CI green', 'ok', 'you', '2d') + qrow('ready', 3112, 'Harbor ferry: seat map', 'Can merge', 'ok', 40, 11, 'CI green', 'ok', 'you', '1d')
SNZ_LINE = '<div class="state-line" style="background:var(--s1)"><span class="g parked" style="border-color:var(--ink-subtle)"></span><span class="what">2 snoozed, the next back Mon 9:00</span><span class="btns"><span class="btn ghost">Show snoozed</span></span></div>'
write('queue-empty-some', qpage('Reviews - nothing needs you', SNZ_LINE + grp('Needs your review', 0) + '<div class="empty1" style="padding:8px var(--inset);font-size:13px;color:var(--ink-muted)">Nothing needs your review.</div>' + grp('Waiting on others', 2) + WAITING + grp('Ready to merge', 2) + READYR, head=qhead(count='0 need you · 2 ready to merge'), ncount=0, sr='Nothing needs your review. Needs your review has 0 rows. Waiting on others 2, Ready to merge 2. 2 snoozed, the next back Monday 9:00. The tab reads 0.'))
write('queue-empty-all', qpage('Reviews - nothing at all', empty('done', 'Nothing waiting on you and no open PRs of yours.', 'Pull requests from the repos you watch show here. The watched repos are set in Settings, Reviews.', btn('Refresh', 'F5', 'primary')), head=qhead(count='0 need you'), ncount=0, base=[('F5', 'refresh'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], donelist=False, sr='Nothing waiting on you and no open PRs of yours. Button: Refresh.'))
write('queue-empty-stale', qpage('Reviews - empty and stale', '<div class="state-line att"><span class="g parked"></span><span class="what">List is from 08:12: GitHub search failed: rate limit reached, resets 09:58.</span><span class="btns"><span class="det">Details ▸</span><span class="btn">Retry <kbd>F5</kbd></span></span></div>' + empty('todo', 'Nothing waiting in what I could read', 'The list above is from 08:12. Newer pull requests may exist.', btn('Refresh', 'F5', 'primary')), head=qhead(count='0 need you', asof='as of 08:12 · joi'), ncount=0, base=[('F5', 'refresh'), ('Ctrl+K', 'palette'), ('?', 'all keys')], kb=[], donelist=False, sr='Nothing waiting in what I could read. The list is from 08:12. Button: Refresh.'))
def menu(items, top, left, w=300, foot='↑/↓ move · Enter pick · Esc cancel', rel=False, head=None):
    o = f'<div class="menu" style="{"" if rel else "left:%dpx;top:%dpx;" % (left, top)}width:{w}px;{"position:relative;" if rel else ""}">'
    if head: o += f'<div class="mhead">{head}</div>'
    for lab, k, dis, on in items:
        k_ = f'<kbd>{k}</kbd>' if k and not dis else ''
        why = f'<span class="when" style="max-width:150px;white-space:normal;line-height:14px;font-size:11px">{dis}</span>' if dis else ''
        o += f'<div class="ment{" on" if on else ""}{" dis" if dis else ""}"><span class="lab">{lab}</span>{why}{k_}</div>'
    return o + f'<div class="mfoot"><span>{foot}</span></div></div>'
DOT = [('Review', 'Enter', '', True), ('Snooze', 'h', '', False), ('Done', 'e', '', False), ('Request reviewer', 'a', '', False), ('Run pre-review', '', '', False), ('Merge when ready', '', 'not your PR', False), ('Start babysitter', '', '', False), ('Open on GitHub', '', '', False), ('Copy link', '', '', False)]
write('queue-dot-menu', qpage('Reviews - . menu', qrows(sel=3120), extra=menu(DOT, 164, 540, 300), sr='Actions menu on number 3120: Review, Snooze, Done, Request reviewer, Run pre-review, Merge when ready (disabled: not your PR), Start babysitter, Open on GitHub, Copy link. Up and down move, Enter picks, Escape cancels.'))
DOT2 = [(('Cancel run' if l == 'Run pre-review' else l), k, d, o) for l, k, d, o in DOT]
write('queue-dot-menu-running', qpage('Reviews - . menu, pre-review running', qrows(sel=3120), extra=menu(DOT2, 164, 540, 300), sr='Actions menu while a pre-review runs: Cancel run replaces Run pre-review.'))
CE1 = [('Merge', '', 'waiting on your review', False), ('Approve', '', '', False), ('Comment', '', '', True), ('Request changes', '', '', False)]
CE2 = [('Merge', '', '', True), ('Approve', '', 'your own PR', False), ('Comment', '', '', False), ('Request changes', '', 'your own PR', False)]
write('queue-ctrl-enter-menu', sheetpage('Reviews - Ctrl+Enter menu', 'c2', panel('On #3120 (ada’s PR, waiting on you)', 'Comment preselected', '<div style="padding:24px">' + menu(CE1, 0, 0, 320, rel=True, head='Review #3120') + '</div>') + panel('On #3109 (your own PR, ready)', 'Merge preselected', '<div style="padding:24px">' + menu(CE2, 0, 0, 320, rel=True, head='Review #3109') + '</div>'), 'Ctrl+Enter on a queue row opens Home’s PR menu. On 3120: Merge disabled, Comment preselected. On 3109: Merge preselected, Approve and Request changes disabled, your own PR. Nothing posts from the menu.').replace('<div class="ss c2">', '<div class="ss c2" style="grid-template-rows:1fr">'))
# done, snooze
done_t = '<div class="toast"><span class="g done"></span><span>Marked done. Undo (Ctrl+Z)</span><span class="bt"><span class="btn">Undo <kbd>Ctrl+Z</kbd></span></span><span class="life"></span></div>'
def rows_without(num):
    return re.sub(r'<div class="row rv[^>]*>(?:(?!</div>).)*?#' + str(num) + r'.*?</div>\n', '', qrows(sel=3131), flags=re.S)
import re
write('queue-done-toast', qpage('Reviews - done', qrows(done=3127, sel=3131, cur=3131), head=qhead(count='3 need you · 2 ready to merge'), ncount=3, extra=done_t, sr='Toast: Marked done. Undo, Control Z, holds 5 seconds. Number 3127 left the list. The cursor is on the next row, 3131. The header reads 3 need you.'))
SN = [('Later today', '17:00', '1', True), ('Tomorrow', 'Fri 9:00', '2', False), ('Monday', 'Mon 9:00', '3', False), ('Next week', 'Thu 9:00', '4', False), ('Pick a date or time', '', '5', False)]
def snmenu(top, left):
    o = f'<div class="menu" style="left:{left}px;top:{top}px;width:300px"><div class="mhead">Snooze until</div><div class="mfield"><span style="color:var(--ink-subtle)">Type a date: fri 14:00, nov 20, in 2h</span></div>'
    for lab, w, k, on in SN:
        o += f'<div class="ment{" on" if on else ""}"><span class="lab">{lab}</span><span class="when">{w}</span><kbd>{k}</kbd></div>'
    return o + '<div class="mfoot"><span>1 to 5 pick · Esc close</span></div></div>'
write('queue-snooze-picker', qpage('Reviews - snooze picker', qrows(sel=3127), extra=snmenu(204, 540), kb=[('1-5', 'pick'), ('Enter', 'snooze'), ('Esc', 'close')], sr='Home’s snooze picker on number 3127: later today 17:00, tomorrow 9:00, Monday 9:00, next week, a date or time. Keys 1 to 5.'))
snz_row = qrow('need', 3127, 'Harbor ferry: fare table', 'back Mon 9:00', '', 88, 9, 'CI red', 'err', 'lee', '3h', 'read dimr')
write('queue-snoozed-toggle', qpage('Reviews - snoozed shown', grp('Needs your review', 1) + qrow('need', 3120, 'Harbor ferry: seat holds', 'asked of you', '', 310, 42, 'CI green', 'ok', 'ada', '1h40', 'unread cursor') + grp('New commits since you reviewed', 1) + qrow('new', 3131, 'Quay lights: dim at night', '3 new commits', '', 20, 4, 'CI green', 'ok', 'sam', '5h') + grp('Snoozed', 1) + snz_row, head=qhead(count='3 need you · 2 ready to merge').replace('<div class="filter">', '<span class="seg" style="margin-left:12px"><span class="on">Snoozed (1)</span></span><div class="filter">'), ncount=3, sr='Snoozed (1) is on. Number 3127, snoozed, back Monday 9:00. Snoozed rows are not counted in 3 need you.'))
back_row = qrow('need', 3127, 'Harbor ferry: fare table', 'back: 2 new commits', 'att', 94, 12, 'CI green', 'ok', 'lee', '10m', 'unread cursor')
pk_back = peek().replace('Harbor ferry: seat holds', 'Harbor ferry: fare table').replace('#3120', '#3127').replace('Asked of', 'Back').replace('you, 1h40 ago', '2 new commits since you snoozed it')
write('queue-back', qpage('Reviews - back', '<div class="group"><span class="chev">▾</span>New<span class="n">1</span></div>' + back_row + qrows().split('</div>', 1)[1] if False else '<div class="group"><span class="chev">▾</span>New<span class="n">1</span></div>' + back_row + grp('Needs your review', 1) + qrow('need', 3120, 'Harbor ferry: seat holds', 'asked of you', '', 310, 42, 'CI green', 'ok', 'ada', '1h40', 'unread') + grp('New commits since you reviewed', 1) + qrow('new', 3131, 'Quay lights: dim at night', '3 new commits', '', 20, 4, 'CI green', 'ok', 'sam', '5h'), pk_back.replace('<div class="sec">Pre-review</div>', '<div class="sec">Why it is back</div><div class="pre att"><span class="g newc"></span><span><b>Back: 2 new commits</b> since it was snoozed until Mon 9:00. The snooze ended early.</span></div><div class="sec">Pre-review</div>'), head=qhead(count='4 need you · 2 ready to merge'), sr='Number 3127 is back in the New band: back, 2 new commits. The peek says why it is back.'))

# ---- review surface states (MUST 2)
def skl(n=12, w0=60):
    return ''.join(f'<div class="dl2"><span class="o"></span><span class="n"></span><span class="m"></span><span class="c"><i style="display:block;height:8px;width:{w0 + (i * 53) % 340}px;background:var(--s2);border-radius:2px;margin-top:6px"></i></span></div>' for i in range(n))
def msgpane(title_html, body_html):
    return f'<div class="dfh"><span class="nm">{title_html}</span></div><div style="padding:20px var(--inset);font-size:13px;line-height:20px;color:var(--ink-muted)">{body_html}</div>'
SKFILES = '<div class="fl one">' + ''.join(f'<div class="tf"><span class="mk"></span><span class="nm"><i style="display:block;height:8px;width:{w}px;background:var(--s2);border-radius:2px"></i></span><span></span><span></span></div>' for w in (140, 110, 170, 120, 150)) + '</div>'
write('diff-loading', surfA(1440, filelist=SKFILES, dover='<div class="dfh"><span class="nm" style="color:var(--ink-muted)">Reading 8 files…</span></div><div class="diff">' + skl() + '</div>', sr='Diff loading: Reading 8 files, after 300 milliseconds, skeleton lines. J and K move on the file list.', pc=pcol(draft='Draft: 2 comments')))
write('diff-error', surfA(1440, filelist=' ', dover='<div class="state-line err" style="height:auto;padding:12px var(--inset)"><span class="g fail"></span><span class="what" style="white-space:normal">Couldn’t read the diff: GitHub answered 502 Bad Gateway.' + det('SND-REVIEW-503') + '</span><span class="btns">' + btn('Retry', 'Enter', 'primary foc') + btn('Open on GitHub') + '</span></div>', sr='Couldn’t read the diff: GitHub answered 502 Bad Gateway. Buttons: Retry, focused, and Open on GitHub.'))
FAILFL = filelist_A(sel='HoldEndpoint.cs').replace('<span class="mk ">▸</span><span class="nm">HoldPolicy.cs</span>', '<span class="mk x">!</span><span class="nm">HoldPolicy.cs</span>').replace('<span class="mk ">▸</span><span class="nm">0042_holds.sql</span>', '<span class="mk x">!</span><span class="nm">0042_holds.sql</span>')
write('diff-partial', surfA(1440, filelist=FAILFL, dover=diffOpenEndpoint().replace('<div class="diff">', '<div class="diff">', 1)[:-6] + '<div class="state-line err" style="margin-top:8px"><span class="g fail"></span><span class="what">2 files failed: HoldPolicy.cs, 0042_holds.sql</span><span class="btns">' + btn('Retry', 'Enter', 'foc') + '</span></div></div>', sr='The files that were read, then one row: 2 files failed, HoldPolicy.cs and 0042_holds.sql. Button: Retry.'))
OFFH = '<span class="dot">·</span><span class="tag att">Offline · as of 09:41</span>'
write('offline-surface', surfA(1440, opening=True, pcls='dis', ready=OFFH, banner='<div class="banner" style="background:var(--s1)"><span class="g dn"></span><span>Offline: reconnect to post. Your draft is kept.</span><span class="sp"></span><span class="btn">Refresh <kbd>F5</kbd></span></div>', sr='Offline, as of 09:41. The cached diff is shown. Write controls are disabled: Offline, reconnect to post. Local keys work. F5 refreshes.'))
write('offline-surface-uncached', surfA(1440, filelist=' ', pcls='dis', ready=OFFH, dover=msgpane('#3120', '#3120 isn’t cached on this box. Reconnect to read it.' + det('SND-REVIEW-503') + '<div style="margin-top:10px">' + btn('Refresh', 'F5') + '</div>'), pc='<aside class="pc"><div class="empty1">Nothing cached.</div></aside>', sr='#3120 isn’t cached on this box. Reconnect to read it. Button: Refresh.'))
for nm, st, sr_ in (('thread-sending', 'sending', 'Thread: Resolving, sending. Button: Discard.'), ('thread-send-failed', 'sendfail', 'Thread: Couldn’t resolve, GitHub refused it. Buttons: Retry, focused, and Discard. The code SND-REVIEW-019 is shown on the thread.'), ('thread-expired', 'expired', 'Thread: Not sent, expired after 10 minutes. Button: Repeat, focused. Details: SND-REVIEW-022.')):
    write(nm, surfA(1440, pc=pcol(tstate=st), sr=sr_))
# since your review states
def since_walk(name, center, sr, tkw=None):
    write(name, walk(1440, since=True, opening=False, center=center, tkw=tkw or dict(only=True, sel='HoldService.cs'), sr=sr))
since_walk('since-loading', '<div class="dpane">' + msgpane('<span style="color:var(--ink-muted)">Comparing a1b2c3d with 9f8e7d6…</span>', '') + '<div class="diff">' + skl(10) + '</div></div>', 'Since your review loading: Comparing a1b2c3d with 9f8e7d6, after 300 milliseconds. G S shows the whole PR.')
since_walk('since-empty', '<div class="dpane">' + msgpane('Nothing changed', 'Nothing changed since your review of 9f8e7d6.<div style="margin-top:10px">' + btn('Show the whole PR', 'g s') + '</div>') + '</div>', 'Nothing changed since your review of 9f8e7d6. G S shows the whole PR.')
since_walk('since-error', '<div class="dpane"><div class="state-line att" style="height:auto;padding:8px var(--inset)"><span class="g parked"></span><span class="what" style="white-space:normal">Couldn’t compare 9f8e7d6 with a1b2c3d: GitHub answered 502. Showing the whole PR.</span></div>' + diffOpenEndpoint() + '</div>', 'Couldn’t compare 9f8e7d6 with a1b2c3d: GitHub answered 502. Showing the whole PR.')
# callers
def callers(title, body):
    return f'<div class="pop" style="position:relative;width:100%;box-shadow:none"><div class="ph" style="margin:0;height:24px">Callers of HoldService.Place</div>{body}<div class="sf2"><span style="font-size:11px;color:var(--ink-subtle)"><kbd>Ctrl+o</kbd> back</span></div></div>'
mk = lambda m: f'<span class="tag" style="margin-left:6px">{m}</span>'
c1 = callers('', '<div class="it dis"><span class="g ci-run"></span><span>Finding callers…</span><span></span></div>')
c2 = callers('', '<div style="padding:8px 14px;font-size:12px;color:var(--ink-muted)">No parser for SQL: callers unknown.' + mk('none (no parser)') + '</div>')
c3 = callers('', '<div style="padding:8px 14px;font-size:12px;color:var(--ink-muted)">No callers found, list may be incomplete.' + mk('partial (from main)') + '</div>')
c4 = callers('', '<div style="padding:8px 14px;font-size:12px;color:var(--ink-muted)">Callers unavailable: code index not built for acme/harbor.' + det('SND-CONTEXT-001') + '</div>')
write('callers-states', sheetpage('Reviews - callers states', 'c2', panel('Loading', 'after 300 ms', '<div style="padding:16px">' + c1 + '</div>') + panel('none (no parser)', 'D5: never “no callers”', '<div style="padding:16px">' + c2 + '</div>') + panel('partial, empty', 'the mark is part of the answer', '<div style="padding:16px">' + c3 + '</div>') + panel('Unavailable', 'code index not built', '<div style="padding:16px">' + c4 + '</div>'), 'Callers popover states: Finding callers. None (no parser): No parser for SQL, callers unknown. Partial and empty: No callers found, list may be incomplete. Unavailable: code index not built.'))
# draft save failed
cmpf = ('<div class="cmp"><div class="ta">Can we inject IClock here? A static clock makes the 10-minute hold hard to pin in a test.<span class="caret"></span></div>'
        '<div class="state-line err" style="height:auto;padding:6px 12px;background:var(--err-soft)"><span class="g fail"></span><span class="what" style="white-space:normal">Couldn’t save the draft: disk full. Your text is still here.</span><span class="btns">' + btn('Retry', 'Enter', 'foc') + '</span></div>'
        '<div class="tb2"><span>Line 41 · HoldService.cs · retrying every 5 s</span><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span>' + btn('Add to draft', 'Ctrl+Enter', 'primary') + '</div></div>')
write('draft-save-failed', surfA(1440, pcls='', composer=cmpf, cur=41, pc=pcol(draft='Draft: 2 comments · 1 not saved'), sr='Composer: Couldn’t save the draft: disk full. Your text is still here. Button: Retry. Ctrl Enter keeps the comment in memory and marks it not saved. Draft counter: 2 comments, 1 not saved.', kb=keys([('Enter', 'retry'), ('Ctrl+Enter', 'keep in memory'), ('Esc', 'close')], None)))
# submit failures
def fail_sheet(banner_html, comments=None, verdict='moved', keep_primary=False):
    sh = sheet(comments=comments if comments is not None else NORMAL_C, verdict=verdict, banner=banner_html)
    return sh.replace('btn primary foc', 'btn')
def fbanner(sentence, code, extra='', cls='background:var(--att-soft)'):
    return f'<div class="banner" style="{cls};flex-direction:column;align-items:flex-start;gap:4px;padding:10px var(--sp5)"><span>{sentence}</span>{det(code)}<span style="display:flex;gap:8px;margin-top:4px">{extra}</span></div>'
SK = keys([('Tab', 'next'), ('Esc', 'cancel')], None)
write('submit-failed-010', modal(extra='<div class="dim"></div>' + fail_sheet(fbanner('The draft changed in another window. Reopen the sheet to see it.', 'SND-REVIEW-010', btn('Reopen', 'Enter', 'primary foc'))), sr=SSR + 'The draft changed in another window. Reopen the sheet to see it. Button: Reopen, focused. Details: SND-REVIEW-010.', kb=keys([('Enter', 'reopen'), ('Esc', 'cancel')], None)))
write('submit-failed-012', modal(extra='<div class="dim"></div>' + fail_sheet(fbanner('GitHub refused the review: Resource protected by organization SAML enforcement.', 'SND-REVIEW-012', btn('Switch to read-only'), 'background:var(--err-soft)'), verdict='normal').replace('class="btn ghost">Cancel', 'class="btn ghost foc">Cancel'), sr=SSR + 'GitHub refused the review: resource protected by organization SAML enforcement. Button: Switch to read-only. Focus on Cancel. Details: SND-REVIEW-012.', kb=keys([('Esc', 'cancel')], None)))
refused = cm('HoldService.cs:41', 'Static clock; tests can’t pin it.', '<span style="color:var(--err)">⚠ GitHub refused this line comment: the line is not in the diff</span>', '<span class="btn foc">Edit</span><span class="btn">Remove</span>', 'flag') + c_typed
write('submit-failed-013', modal(extra='<div class="dim"></div>' + fail_sheet(fbanner('GitHub refused 1 line comment: the line is not in the diff. It’s marked below.', 'SND-REVIEW-013', '', 'background:var(--err-soft)'), comments=refused), sr=SSR + 'GitHub refused 1 line comment. The row for HoldService.cs line 41 is marked; focus is on its Edit. The verdicts come back after Edit or Remove. Details: SND-REVIEW-013.', kb=SK))
write('submit-failed-014', modal(extra='<div class="dim"></div>' + fail_sheet(fbanner('This sheet expired. Reopen it to post.', 'SND-REVIEW-014', btn('Reopen', 'Enter', 'primary foc'))), sr=SSR + 'This sheet expired. Reopen it to post. Button: Reopen, focused. Details: SND-REVIEW-014.', kb=keys([('Enter', 'reopen'), ('Esc', 'cancel')], None)))
chk = '<div class="banner" style="background:var(--s2);gap:10px"><span class="g ci-run"></span><span>Checking whether it posted…</span></div>'
write('submit-checking', modal(extra='<div class="dim"></div>' + fail_sheet(chk, verdict='moved'), sr=SSR + 'Checking whether it posted. Every button is disabled; Escape is off until the check ends, 15 seconds at most. A polite live region reads the status.', kb=keys([], None)))
write('submit-not-posted', modal(extra='<div class="dim"></div>' + fail_sheet(fbanner('It didn’t post. Nothing was sent twice.', '', btn('Retry', 'Enter', 'primary foc')).replace('<div class="dt">Details ▸</div>', ''), verdict='moved'), sr=SSR + 'It didn’t post. Nothing was sent twice. Button: Retry, focused.', kb=keys([('Enter', 'retry'), ('Esc', 'cancel')], None)))
# read-only
RO_LINE = '<div class="banner" style="background:var(--s1);min-height:32px"><span class="g linked"></span><span>Read-only: writes open github.com</span>' + det('review.writes is off') .replace('<div class="dt">', '<span class="dt" style="margin:0 0 0 8px">').replace('</div>', '</span>') + '</div>'
ro_pc = pcol().replace('Resolve <kbd>Ctrl+⏎</kbd>', 'Open on GitHub').replace('Reply <kbd>r</kbd>', 'Reply <kbd>r</kbd>')
write('read-only', surfA(1440, opening=True, primary='Open on GitHub', pk='', banner=RO_LINE, pc=ro_pc, sr='Read-only: writes open github.com. The header’s primary, Resolve and the verdicts all read Open on GitHub. Local keys, the draft and viewed marks still work.'))
sh_ro = sheet(comments=NORMAL_C).replace(re.search(r'<div class="sf">.*</div></aside>', sheet(comments=NORMAL_C), flags=re.S).group(0), '<div class="sf"><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="sp"></span><span class="btn">Copy as text</span><span class="btn primary foc">Open on GitHub</span></div></aside>')
write('read-only-sheet', modal(extra='<div class="dim"></div>' + sh_ro, primary='Open on GitHub', pk='', sr=SSR + 'Read-only. The sheet shows the draft with Copy as text and Open on GitHub in place of the three verdicts.', kb=SK))
# thread focused footer
pcf = pcol().replace('<div class="th">', '<div class="th fx2">', 1)
write('thread-focused-footer', surfA(1440, pc=pcf, kb=keys([('Ctrl+Enter', 'resolve thread'), ('r', 'reply'), ('.', 'actions'), ('Esc', 'back to line')], None), sr='A thread has focus. The footer changes: Control Enter resolves the thread, r replies, period opens actions, Escape goes back to the line.'))
# 300 files
more_rows = ''.join(tf('▸', f'Module{i:03d}/Service{i:03d}.cs', '', 40 + i, i % 7, '', '') for i in range(1, 7))
CAP = '<div class="fl one" style="max-height:230px;overflow:hidden;position:relative;display:block"><div class="fg">▾ db/Migrations <span style="font-weight:400">1</span></div>' + tf('▸', '0042_holds.sql', '', 14, 0, 'sel', '') + '<div class="fg">▾ src <span style="font-weight:400">showing 50 of 300</span></div>' + tf('✓', 'HoldEndpoint.cs', '', 18, 2, '', 'v') + more_rows + '<div style="position:absolute;right:6px;top:8px;width:6px;height:210px;background:var(--s2);border-radius:3px"><div style="width:6px;height:46px;background:var(--hair-3);border-radius:3px"></div></div></div>'
write('a-300files', surfA(1440, opening=True, filelist=CAP, files='300 files +5,210 −1,840', viewed='3/300 viewed', findings='Pre-review: not run (over 60 files)', sr='Layout A with 300 files: the file list is capped at 8 rows with its own scrollbar; it never pushes the diff down. The list says showing 50 of 300.'))

# ---------------- index ----------------
(D / 'frames.txt').write_text('\n'.join(FRAMES) + '\n')
print(len(FRAMES), 'frames')
