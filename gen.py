import pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'
FACTS = '--facts:var(--col-fact) var(--col-fact) var(--col-fact-wide)'

class P:
    """Builds a list body and tracks the y of each row (for popups)."""
    def __init__(s, y=96): s.h=''; s.y=y; s.first=True; s.at={}
    def group(s, label, n=None, right='', chev='▾', cur=False, rcls=''):
        if not s.first: s.y += 8
        s.first=False
        nn = f' <span class="n">{n}</span>' if n is not None else ''
        r = f'<span class="right {rcls}">{right}</span>' if right else ''
        s.h += f'<div class="group{" cursor" if cur else ""}"><span class="chev">{chev}</span>{label}{nn}{r}</div>\n'
        s.y += 28
    def raw(s, h, dy): s.h += h; s.y += dy
    def row(s, key, **k):
        s.at[key] = s.y; s.h += row(**k) + '\n'; s.y += 32

def person(n, kind='p'):
    if not n: return '<div class="fact"></div>'
    if kind=='a': return f'<div class="fact"><span class="av agent">{n[0].upper()}</span>{n}</div>'
    return f'<div class="fact"><span class="av">{n[0]}</span>{n}</div>'

def row(g, t, why='', obj='', frm=None, age='', wait='', cls='read', id='', sub='', whycls='', acts='', agek='p', agecls='', style=FACTS):
    idh = f'<span class="id">{id}</span>' if id else ''
    sb = f'<span class="sub">{sub}</span>' if sub else ''
    if why:
        if whycls == 'err': wf = f'<div class="fact"><span class="chip err">{why}</span></div>'
        else: wf = f'<div class="fact {"blk" if why.startswith("blocks") else ""}">{why}</div>'
    else: wf = '<div class="fact"></div>'
    of = f'<div class="fact mono">{obj}</div>'
    ff = person(frm, agek) if frm else '<div class="fact"></div>'
    if wait == 'you': wh = '<div class="wait you">you</div>'
    elif wait: wh = f'<div class="wait">{wait}</div>'
    else: wh = '<div class="wait"></div>'
    ah = f'<span class="actions">{acts}</span>' if acts else ''
    return (f'<div class="row {cls}" style="{style}"><span class="g {g}"></span><div class="title">{idh}<span class="t">{t}</span>{sb}</div>'
            f'{wf}{of}{ff}<div class="age {agecls}">{age}</div>{wh}{ah}</div>')

def btn(t, p=False, k=''):
    kk = f' <kbd>{k}</kbd>' if k else ''
    return f'<button class="btn{" primary" if p else ""}">{t}{kk}</button>'
ACT_REPLY = btn('Reply', True) + btn('Snooze') + btn('Done')
ACT_META = btn('Copy digest request', True) + btn('Snooze') + btn('Done')
ACT_OPEN = btn('Open', True) + btn('Snooze') + btn('Done')

NAV = ['Home','Reviews','Items','Workflows','Agents','Projects','Context']
def topbar():
    n = ''.join(f'<a{" class=on" if x=="Home" else ""}>{x}</a>' for x in NAV)
    return (f'<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav">{n}</nav><div class="spacer"></div>'
            '<div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><div class="status-pill">Connected</div></div>')

def tabs(on='All', counts=(9,3,2,1,3)):
    names = ['All','Reviews','Messages','Work','Agents']
    return ''.join(f'<a{" class=on" if nm==on else ""}>{nm}<span class="n">{c}</span></a>' for nm, c in zip(names, counts))

def header(on='All', counts=(9,3,2,1,3), snz=3, chip=('Health ok',''), upd=False):
    u = '<span class="upd">Updating…</span>' if upd else ''
    return (f'<div class="header"><h1>Home</h1><div class="tabs">{tabs(on,counts)}</div>{u}'
            f'<span class="htoggle"><span class="sw"></span>Snoozed {snz}</span>'
            f'<div class="filter">Filter<kbd style="margin-left:auto">/</kbd></div></div>')

BASE = [('j/k','move'),('Space','peek'),('Enter','open'),('Esc','back'),('/','filter'),('c','create'),('.','actions'),('x','select'),('Ctrl+K','palette')]
PAGE_NARROW = [('h','snooze'),('t','hand'),('e','done'),('?','all keys')]
PAGE = [('h','snooze'),('t','hand to agent'),('e','done'),('R','re-rank')]
def keys(page=None, hint=False, mode=None, done=True, narrow=False):
    if mode == 'picker':
        s = '<span><kbd>↑/↓</kbd>move</span><span><kbd>Enter</kbd>pick</span><span><kbd>Esc</kbd>cancel</span>'
        return f'<div class="keys">{s}</div>'
    s = ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in BASE)
    page = page if page is not None else (PAGE_NARROW if narrow else PAGE)
    s += '<span class="keys-div"></span>' + ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in page)
    if hint: s += '<span><kbd>Left</kbd>fold</span><span><kbd>Right</kbd>open</span>'
    if done and not narrow: s += '<span class="right">Done list</span>'
    return f'<div class="keys">{s}</div>'

def page(title, body, foot, hdr, extra=('',''), bodycls='body', appcls='', w=''):
    return (f'<!doctype html>\n<html lang="en"><head><meta charset="utf-8"><title>{title}</title>'
            f'<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="home.css"></head>\n'
            f'<body class="{w}"><div class="app {appcls}">\n{topbar()}\n{hdr}\n<div class="{bodycls}"><div class="list">\n{body}</div>{extra[0]}</div>\n{foot}\n'
            f'{extra[1]}</div></body></html>\n')

def write(name, h): (D/f'{name}.html').write_text(h)

# ---------------- rows ----------------
LONG = 'Review requested: System grid, rebuilt token table and spacing scale for every list page, plus the new peek footer and fold keys'
def r_qa(): return dict(g='you', t='qa is asking to run rm -rf build/', why='asking', obj='session qa', frm='qa', agek='a', age='new', agecls='mark', cls='unread')
def r_meta(): return dict(g='you', t='meta: at 205k, recycle from a digest', why='blocks 3', obj='lane meta', frm='meta', agek='a', age='15m', cls='unread')
def r_rev(t='Review requested: System grid'): return dict(g='pr', t=t, why='blocks 2', id='#3120', obj='ledgerly…/ui', frm='Dani Okoye', age='1h40', cls='unread')
def r_99(): return dict(g='pr', t='Ready to merge: spacing scale on the 4 px grid', why='blocks 2', id='#3099', obj='ledgerly…/ui', frm='qa', agek='a', age='68m', cls='unread')
def r_ideas(): return dict(g='you', t='ideas: at 210k, recycle from a digest', why='blocks 2', obj='lane ideas', frm='ideas', agek='a', age='20m', cls='unread')
def r_101(): return dict(g='pr', t='Ready to merge: Home rows on RowList', why='blocks 1', id='#3101', obj='ledgerly…/ui', frm='design', agek='a', age='30m', cls='read')
def r_fail(): return dict(g='fail', t='Nightly usage rollup failed at step aggregate-seats', why='failed', whycls='err', id='r-0412', obj='wf/rollup', frm='Sindri', agek='a', age='2h', cls='read')
def r_ment(): return dict(g='mention', t='Marco mentioned you: can you confirm the SSO cutoff date?', why='mention', id='ENG-412', obj='linear', frm='Marco Reyes', age='1h10', cls='read')
def r_dm(): return dict(g='msg', t='Sam asks if the export row limit applies to trial workspaces', why='DM', obj='dm sam.ortiz', frm='Sam Ortiz', age='8m', cls='unread')

def need_set(long_title=False):
    return [('meta', r_meta()), ('rev', r_rev(LONG) if long_title else r_rev()), ('r99', r_99()), ('ideas', r_ideas()),
            ('r101', r_101()), ('fail', r_fail()), ('ment', r_ment()), ('dm', r_dm())]
ACTS = {'qa': ACT_REPLY, 'meta': ACT_META, 'rev': btn('Open review', True) + btn('Snooze') + btn('Done')}

def needs(p, cursor=None, sel=None, stale=(), long_title=False, skip=()):
    for key, k in need_set(long_title):
        if key in skip: continue
        k = dict(k)
        if cursor == key: k['cls'] += ' cursor'; k['acts'] = ACTS.get(key, ACT_OPEN)
        if sel == key: k['cls'] += ' sel cursor'
        if key in stale: k['cls'] += ' stale'
        p.row(key, **k)

def brief_row(g, t, l1='', l2='', age='', sum=False):
    return (f'<div class="row brief read{" sum" if sum else ""}" style="{FACTS}"><span class="g {g}"></span><div class="title"><span class="t">{t}</span></div>'
            f'<div class="fact"></div><div class="fact mono">{l1}</div><div class="fact mono">{l2}</div><div class="age">{age}</div><div class="wait"></div></div>\n')
BRIEF = [
    brief_row('agent', 'Quiet night: #3078 merged, main went red at 09:10, ideas reached the recycle mark.', 'main', '#3078', '09:12', sum=True),
    brief_row('merged', 'PR #3078 merged; CI red on main since 09:10.', '#3078', 'main', '2m'),
    brief_row('done', 'Run r-0418 (release notes 2026.11) ended; the draft is in docs/releases.', 'r-0418', '', '2h'),
]
def brief(p, n=6, rest=3, lines=3):
    p.group('Since you last looked', n, 'written by the brief agent, 09:12')
    for b in BRIEF[:lines]: p.raw(b, 32)
    p.raw(f'<div class="more-line"><kbd>b</kbd>{rest} more lines</div>\n', 28)
def todo(p):
    p.raw('<div class="todoline"><span class="g todo"></span>Todo, 4 open<span class="right">Enter opens the panel</span></div>\n', 36)

MOVING = [
 ('agent','design: building RowList (12 min)','building','lane design','design','12m','agent'),
 ('agent','Fixing the flaky export-queue test','run','r-0419|run/flaky','Sindri','6m','agent'),
 ('agent','Drafting release notes for 2026.11','run','r-0420|run/relnotes','Sindri','11m','agent'),
 ('agent','qa: running the integration lane for #3101','lane','lane qa','qa','4m','agent'),
 ('ci-run','CI: 4 of 7 checks done','CI 4/7','#3120|ledgerly…/ui','','3m','CI'),
 ('ci-run','CI: 6 of 9 checks done','CI 6/9','#3125|ledgerly…/ui','','9m','CI'),
]
def moving(p, open_=False, cur=False):
    p.group('Moving: 4 agents, 2 CI runs', None, '', chev='▾' if open_ else '▸', cur=cur)
    if not open_: return
    for i, (g, t, why, obj, frm, age, w) in enumerate(MOVING):
        i_, _, o_ = obj.partition('|')
        p.row(f'mv{i}', g=g, t=t, why=why, id=i_ if o_ else '', obj=o_ or obj, frm=frm or None, agek='a', age=age, wait=w)
    p.raw('<div class="more-line">Show all 6 in Agents</div>\n', 28)

def all_list(cursor='qa', long_title=False):
    p = P()
    p.group('New', 1)
    k = r_qa()
    if cursor == 'qa': k['cls'] += ' cursor'; k['acts'] = ACT_REPLY
    p.row('qa', **k)
    p.group('Needs you', 8, '', rcls='ink')
    needs(p, cursor, long_title=long_title)
    moving(p); brief(p); todo(p)
    return p

# ---------------- 1. All ----------------
p = all_list()
write('home-all-1440', page('Sindri · Home', p.h, keys(), header()))
write('home-all-1000', page('Sindri · Home (1000)', p.h, keys(narrow=True), header(), w='w1000'))

# ---------------- 2. Peek ----------------
PEEK = '''<aside class="peek">
<div class="peek-head"><h2>Ready to merge: spacing scale on the 4 px grid</h2><span class="id">#3099</span><span class="icon">⤢</span><span class="icon">×</span></div>
<dl class="peek-fields">
<dt>Source</dt><dd>GitHub, aw-meta merge queue</dd>
<dt>Waiting since</dt><dd>08:02 · 68m</dd>
<dt>Blocks</dt><dd><span class="linkid">#3101</span><span class="linkid">#3040</span><span style="color:var(--ink-3)">merge-order, closes</span></dd>
<dt>From</dt><dd><span class="av agent">Q</span>qa</dd>
<dt>Linked</dt><dd><span class="linkid">#3040</span> issue · <span class="linkid">#3101</span> PR</dd>
<dt>headSha</dt><dd class="sha">9f3c2ab41d</dd>
<dt>CI</dt><dd>7/7 passed</dd>
</dl>
<div class="peek-body">
<div class="eyebrow" style="margin-top:0">Diff summary</div>
<div class="mono-files">
<div><span class="fact mono">src/Ui/Theme/Spacing.cs</span><span class="diffstat"><span class="a">+64</span> <span class="d">−21</span></span></div>
<div><span class="fact mono">src/Ui/Views/Controls/RowList.cs</span><span class="diffstat"><span class="a">+88</span> <span class="d">−30</span></span></div>
<div><span class="fact mono">tests/Aw.Sindri.Ui.Tests/SpacingTests.cs</span><span class="diffstat"><span class="a">+60</span> <span class="d">−13</span></span></div>
<div style="color:var(--ink-3);font-size:12px">3 files · +212 −64</div>
</div>
<div class="eyebrow">Checks</div>
<div class="checks">
<div><span class="g done"></span>build<span class="r">1m 12s</span></div>
<div><span class="g done"></span>unit lane<span class="r">3m 40s</span></div>
<div><span class="g done"></span>integration lane<span class="r">6m 05s</span></div>
<div><span class="g done"></span>format<span class="r">48s</span></div>
</div>
<div class="eyebrow">Closes</div>
<div class="checks"><div><span class="g linked"></span><span class="linkid">#3040</span>Home screenshots are missing the fold frame</div></div>
<div class="eyebrow">Merge</div>
<p class="note">Merge runs <code>aw-meta merge-run</code>, which runs the same merge-check meta runs. It is refused if the head moves from <code>9f3c2ab</code> and Merge then writes nothing. Meta is not merging it yet.</p>
</div>
<div class="peek-actions"><button class="btn primary">Merge… <kbd>Ctrl+Enter</kbd></button><button class="btn">Open <kbd>Enter</kbd></button><button class="btn ghost">Hand to agent <kbd>t</kbd></button></div>
</aside>'''
def svg(d): return f'<svg viewBox="0 0 16 16">{d}</svg>'
def menu_submit():
    ents = [
      ('Merge', 'm', 'aw-meta merge-run', '<circle cx="4.5" cy="4" r="1.6"/><circle cx="4.5" cy="12" r="1.6"/><circle cx="11.5" cy="8" r="1.6"/><path d="M4.5 5.6V10.4M4.5 8H9.9"/>', 1),
      ('Approve', 'a', 'at 9f3c2ab', '<circle cx="8" cy="8" r="5.5"/><path d="M5.6 8.2L7.3 9.9L10.6 6.3"/>', 0),
      ('Comment…', 'c', '', '<path d="M3 4h10v6.5H7.5L5 13v-2.5H3z"/>', 0),
      ('Request changes…', 'r', '', '<circle cx="8" cy="8" r="5.5"/><path d="M8 5v3.5M8 10.8v.4"/>', 0)]
    o = '<div class="menu" style="left:1016px;bottom:92px;width:400px"><div class="mhead">Submit for #3099</div>\n'
    for lab, k, w, ic, on in ents:
        wh = f'<span class="when">{w}</span>' if w else ''
        o += f'<div class="ment{" on" if on else ""}">{svg(ic)}<span class="lab">{lab}</span>{wh}<kbd>{k}</kbd></div>\n'
    return o + '</div>\n'
pp = P()
pp.group('New', 1); pp.row('qa', **r_qa())
pp.group('Needs you', 8, '', rcls='ink')
needs(pp, sel='r99')
moving(pp); brief(pp); todo(pp)
write('home-peek', page('Sindri · Home · peek', pp.h, keys(), header(), bodycls='body peeking', extra=(PEEK, '')))
write('home-peek-submit', page('Sindri · Home · peek · submit menu', pp.h, keys(mode='picker'), header(), bodycls='body peeking', extra=(PEEK, menu_submit())))

# ---------------- 3. Fold ----------------
f = P()
f.group('New', 1); f.row('qa', **r_qa())
f.group('Needs you', 8, '', chev='▸', cur=True, rcls='ink')
moving(f, open_=True); brief(f); todo(f)
write('home-fold', page('Sindri · Home · fold', f.h, keys(hint=True), header()))

# ---------------- 4. Pickers ----------------
CLOCK = '<circle cx="8" cy="8.5" r="5.5"/><path d="M8 5.5V8.5L10 10"/>'
def hand_menu(top):
    o = f'<div class="menu wide" style="left:1056px;top:{top}px"><div class="mhead">Hand PR #3120 to an agent</div>\n'
    o += '<div class="mfield focus"><span class="ph">Filter sessions</span><span class="caret"></span></div>\n'
    o += '<div class="parse"><span class="ok">→</span><span>Goes along: PR #3120, 2 linked issues, the thread, about 4k tokens</span></div>\n'
    o += '<div class="mhead">New session</div>\n'
    o += '<div class="ment on"><span class="av agent">CC</span><span class="lab">Claude Code</span><span class="when">new</span></div>\n'
    o += '<div class="ment"><span class="av agent">CX</span><span class="lab">Codex</span><span class="when">new</span></div>\n'
    o += '<div class="mdiv"></div><div class="mhead">Running in agentic-workflow</div>\n'
    o += '<div class="ment"><span class="av agent">D</span><span class="lab">design</span><span class="when">idle 4m</span></div>\n'
    o += '<div class="ment"><span class="av agent">I</span><span class="lab">ideas</span><span class="when">working</span></div>\n'
    o += '<div class="ment dis"><span class="av agent">Q</span><span class="lab">qa</span><span class="when">asking you</span></div>\n'
    return o + '</div>\n'
hp = all_list(cursor='rev')
write('home-hand-to-agent', page('Sindri · Home · hand to agent', hp.h, keys(mode='picker'), header(), extra=('', hand_menu(hp.at['rev'] + 32))))
CAL = '<rect x="2.5" y="3.5" width="11" height="10" rx="1.5"/><path d="M2.5 6.5h11M5.5 2v3M10.5 2v3"/>'
def snooze_menu(top):
    o = f'<div class="menu" style="left:1096px;top:{top}px"><div class="mhead">Snooze until</div>\n'
    o += '<div class="mfield"><span class="ph">Type a date: fri 14:00, nov 20, in 2h</span></div>\n'
    for lab, w, k, on in [('Later today','17:00','1',1),('Tomorrow','Fri 9:00','2',0),('Monday','Mon 9:00','3',0),('Next week','Thu 9:00','4',0)]:
        o += f'<div class="ment{" on" if on else ""}">{svg(CLOCK)}<span class="lab">{lab}</span><span class="when">{w}</span><kbd>{k}</kbd></div>\n'
    o += f'<div class="mdiv"></div><div class="ment">{svg(CAL)}<span class="lab">Pick a date or time</span><kbd>5</kbd></div>\n'
    return o + '</div>\n'
sp = all_list(cursor='dm')
write('home-snooze', page('Sindri · Home · snooze', sp.h, keys(mode='picker'), header(), extra=('', snooze_menu(sp.at['dm'] + 32))))

# ---------------- 5. Split ----------------
s = P()
s.group('Needs you', 3, '', rcls='ink')
for key, k in [('rev', r_rev()), ('r99', r_99()), ('r101', r_101())]:
    k = dict(k)
    if key == 'rev': k['cls'] += ' cursor'; k['acts'] = btn('Open review', True) + btn('Snooze') + btn('Done')
    s.row(key, **k)
s.group('Moving: 2 CI runs', None, '', chev='▸')
write('home-split', page('Sindri · Home · Reviews split', s.h, keys(page=PAGE + [('[ ]','split')]), header(on='Reviews')))

# ---------------- 6. States ----------------
def bd(h1, h2): return f'<div class="bigdone"><h2>{h1}</h2><p>{h2}</p></div>\n'
d = P()
d.raw(bd("You're done.", '3 snoozed, the next back Mon 9:00'), 100)
d.group('Since you last looked', 4, 'written by the brief agent, 09:12')
for b in BRIEF: d.raw(b, 32)
d.raw('<div class="more-line"><kbd>b</kbd>1 more line</div>\n', 28)
todo(d)
write('home-done', page('Sindri · Home · done', d.h, keys(), header(counts=(0,0,0,0,0))))

st = P()
st.raw('<div class="state-line att"><span class="g parked"></span><span class="what">GitHub was last read 12 minutes ago, so its rows may be missing.</span><span class="btns"><button class="btn">Refresh</button></span></div>\n', 32)
st.raw(bd('Nothing waiting in what I could read', 'Needs you holds no open row, but GitHub is stale, so I can not say you are done.'), 100)
todo(st)
write('home-stale', page('Sindri · Home · stale source', st.h, keys(), header(counts=(0,0,0,0,0), chip=('GitHub stale','att'))))

lo = all_list(cursor=None)
write('home-loading', page('Sindri · Home · loading', lo.h, keys(), header(upd=True)))

SK = ('<div class="row skel skbar" style="--facts:var(--col-fact) var(--col-fact) var(--col-fact-wide)"><span class="g todo" style="opacity:.3"></span>'
      '<div class="title"><span class="t" style="width:%dpx"></span></div><div class="fact"><span class="sk" style="width:48px"></span></div>'
      '<div class="fact"><span class="sk" style="width:64px"></span></div><div class="fact"><span class="sk" style="width:80px"></span></div>'
      '<div class="age"><span class="sk" style="width:22px;margin-left:auto"></span></div><div class="wait"></div></div>\n')
skel = '<div class="group"><span class="chev">▾</span>Needs you</div>\n' + ''.join(SK % w for w in (360, 280, 420, 320, 240, 380))
write('home-skeleton', page('Sindri · Home · first open', skel, keys(), header(counts=(0,0,0,0,0))))

ce = P()
ce.raw('<div class="state-line err"><span class="g fail"></span><span class="what">Couldn\'t read GitHub: the token expired, so 3 rows are from the last good read.</span><span class="btns"><button class="btn">Connect GitHub</button><span class="det" style="margin:0 0 0 8px">Details ▸</span></span></div>\n', 32)
ce.group('Needs you', 8, '', rcls='ink')
needs(ce, cursor='meta', stale=('rev','r99','r101'))
moving(ce); brief(ce); todo(ce)
write('home-collector-error', page('Sindri · Home · collector error', ce.h, keys(), header(chip=('GitHub not read','att'))))

def drow(t, obj, frm, when, ak='p', hv=False, id=''):
    return (f'<div class="row {"hv " if hv else ""}read" style="{FACTS}"><span class="g done muted"></span><div class="title">' + (f'<span class="id">{id}</span>' if id else '') + f'<span class="t">{t}</span></div>'
            f'<div class="fact"></div><div class="fact mono">{obj}</div>{person(frm, ak)}<div class="age wide">{"" if hv else "done " + when}</div><div class="wait"></div>'
            + (f'<span class="actions"><span style="font-size:12px;color:var(--ink-subtle);align-self:center;margin-right:8px;white-space:nowrap">done {when}</span><button class="btn">Restore</button></span>' if hv else '') + '</div>\n')
dl = '<div class="group"><span class="chev">▾</span>Today <span class="n">3</span></div>\n'
dl += drow('Review requested: audit export paging', 'ledgerly…/ui', 'Nadia Kowalski', '08:41', id='#3114')
dl += drow('meta: at 180k, recycle from a digest', 'lane meta', 'meta', '07:55', 'a', hv=True)
dl += drow('Keep or drop the health strip?', 'decision', 'Sindri', '07:20', 'a')
dl += '<div class="group"><span class="chev">▾</span>Yesterday <span class="n">2</span></div>\n'
dl += drow('Marco mentioned you: can the SSO cutoff move to Dec 1?', 'linear', 'Marco Reyes', '17:31', id='ENG-409')
dl += drow('Ready to merge: retry failed CSV exports', 'ledgerly…/ui', 'qa', '10:05', 'a', id='#3082')
dl += '<div class="group"><span class="chev">▾</span>Earlier this week <span class="n">2</span></div>\n'
dl += drow('Lena asks for the seat-limit banner copy', 'dm lena.okafor', 'Lena Okafor', 'Tue 16:40')
dl += drow('ideas: at 205k, recycle from a digest', 'lane ideas', 'ideas', 'Mon 13:12', 'a')
dh = ('<div class="header"><div class="crumb">Home / <b>Done</b></div><span class="count">last 7 days · 7</span>'
      '<div class="filter" style="margin-left:auto">Filter<kbd style="margin-left:auto">/</kbd></div></div>')
write('home-done-list', page('Sindri · Home · Done list', dl, keys(page=[('Ctrl+Z','undo')], done=False), dh))

# ---------------- 7. Back from snooze ----------------
b = P()
b.group('New', 2)
b.row('lena', g='msg', t='Lena: can we ship the seat-limit banner on Thursday?', sub='snoozed until 9:00', why='DM', obj='dm lena.okafor', frm='Lena Okafor', age='back', agecls='mark', cls='unread cursor', acts=btn('Reply', True) + btn('Snooze') + btn('Done'))
b.row('marco', g='mention', t='Marco mentioned you: can you confirm the SSO cutoff date?', sub='back early: new reply', why='mention', id='ENG-412', obj='linear', frm='Marco Reyes', age='back', agecls='mark', cls='unread')
b.group('Needs you', 7, '', rcls='ink')
needs(b, skip=('ment',))
moving(b); brief(b); todo(b)
write('home-back-from-snooze', page('Sindri · Home · back from snooze', b.h, keys(), header(counts=(9,3,3,1,2), snz=1)))

# ---------------- 8. Scale 1.3 ----------------
sc = all_list(cursor='qa', long_title=True)
y = 40 + 48 + 8 + 36 + 40 + 8 + 36 + 40 + 40   # top of the third Needs-you row at 40 px rows
write('home-all-scale-1.3', page('Sindri · Home · text scale 1.3', sc.h, keys(), header(), appcls='s13',
      extra=('', f'<div class="tip" style="left:420px;top:{y + 4}px">{LONG}</div>')))
