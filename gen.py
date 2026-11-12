import re, html, pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'

def head(title, tab):
    tabs = [('Needs me', 4), ('My work', 23), ('Projects', 16), ('Triage', 13)]
    t = ''.join('<a%s>%s<span class="n">%d</span></a>' % (' class="on"' if n == tab else '', n, c) for n, c in tabs)
    return f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>{title}</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="projects.css">
</head>
<body><div class="app">
<div class="topbar"><span class="wordmark">Sindri</span>
<nav class="nav"><a>Home<span class="n">9</span></a><a>Reviews<span class="n">4</span></a><a>Items<span class="n">31</span></a><a>Workflows</a><a>Agents<span class="n">3</span></a><a class="on">Projects</a><a>Context</a></nav>
<span class="spacer"></span><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><span class="status-pill">Connected</span></div>
<div class="header"><h1>Projects</h1><div class="tabs">{t}</div>
<div class="filter">Filter<kbd style="margin-left:auto">/</kbd></div></div>
'''

def keys(page):
    base = [('j/k','move'),('Space','peek'),('Enter','open'),('Esc','back'),('/','filter'),('c','create'),('.','actions'),('x','select'),('Ctrl+K','palette')]
    s = ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in base)
    s += '<span class="keys-div"></span>' + ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in page)
    return f'<div class="keys">{s}</div>\n</div>'

END = '</body></html>\n'

# ---------- Projects list ----------
FACTS = 'style="--facts: var(--col-fact-wide) repeat(4, var(--col-fact)); --facts-peek: var(--col-fact-wide) repeat(2, var(--col-fact))"'
GL = {'On track':'doing','At risk':'doing','Off track':'doing','Planned':'todo','Paused':'parked'}
FACTS = 'style="--facts: var(--col-fact-wide) repeat(4, var(--col-fact)); --facts-peek: var(--col-fact-wide) repeat(2, var(--col-fact))"'
GL = {'On track':'doing','At risk':'doing','Off track':'doing','Planned':'todo','Paused':'parked'}
def person(n, extra=''):
    return f'<span class="fact{extra}"><span class="av">{n[0]}</span>{n}</span>' if n else f'<span class="fact{extra}"></span>'
def wait(w):
    if not w: return '<span class="wait"></span>'
    if w == 'you': return '<span class="wait you">you</span>'
    if w in ('CI','reviewer'): return f'<span class="wait">{w}</span>'
    if w == 'agent': return '<span class="wait"><span class="av agent">A</span>agent</span>'
    return f'<span class="wait"><span class="av">{w[0]}</span>{w}</span>'
def prow(pid, name, sub, prog, der, hand, lead, target, age, w, cls='read', mism=False, flag=''):
    # no short project id: real ids are prj_<ULID> or linear:project/<uuid>. Lifecycle (Planned/Paused) rides on the glyph;
    # the hand-set cell holds health only. Mismatch = derived at risk/blocked AND hand-set On track.
    life = hand in ('Planned', 'Paused')
    mism = der in ('at risk', 'blocked') and hand == 'On track'
    gl = GL[hand]
    if prog:
        d, t = prog; pct = int(100*d/t)
        bc = {'on track':'ok','at risk':'att','blocked':'att'}.get(der,'')
        pf = f'<span class="fact"><span class="bar {bc}"><i style="width:{pct}%"></i></span>{d}/{t}</span>'
    else: pf = '<span class="fact"></span>'
    dc = {'on track':'ok','at risk':'att','blocked':'err'}.get(der)
    df = f'<span class="fact"><span class="chip {dc}">{der}</span></span>' if der else '<span class="fact"></span>'
    if life: hf = '<span class="fact"></span>'
    elif mism: hf = f'<span class="fact att"><span class="mm">≠</span> {hand}</span>'
    else: hf = f'<span class="fact">{hand}</span>'
    sb = f'<span class="sub">{sub}</span>' if sub else ''
    return (f'<div class="row {cls} {flag}" {FACTS}><span class="g {gl}"></span>'
            f'<span class="title"><span class="t">{name}</span>{sb}</span>'
            f'{pf}{df}{hf}{person(lead," drop-peek")}<span class="fact drop-peek">{target}</span>'
            f'<span class="age">{age}</span>{wait(w)}</div>\n')
def grp(label, n, right='', chev='▾'):
    r = f'<span class="right">{right}</span>' if right else ''
    return f'<div class="group"><span class="chev">{chev}</span>{label} <span class="n">{n}</span>{r}</div>\n'

def plist(flag_mism=''):
    o = grp('At risk', 5, 'derived from each project’s PRs, CI and reviews')
    o += prow('prj-09','Billing rework','blocked: BIL-208 waits on you',(14,22),'blocked','At risk','Mira','Dec 5','1d','you','unread')
    o += prow('prj-14','Exports v2','2 PRs red, 1 review stale 3d',(6,9),'at risk','On track','Dani','Nov 28','3d','CI','unread',True,flag_mism)
    o += prow('prj-21','Workspace transfer','target in 9d, 8 of 11 still open',(3,11),'at risk','At risk','Tomas','Nov 21','2d','Tomas')
    o += prow('prj-17','SSO rollout','3 PRs wait on review 5d+',(5,12),'at risk','Off track','Priya','Dec 1','5d','reviewer')
    o += prow('prj-30','Audit log retention','CI red on main for 2d, 4 PRs held',(2,6),'blocked','At risk','Leo','Nov 24','6d','CI')
    o += grp('On track', 7)
    o += prow('prj-26','Webhook retries','',(4,5),'on track','On track','Leo','Nov 20','3h','you','unread')
    o += prow('prj-12','Usage dashboards','',(18,20),'on track','On track','Priya','Nov 19','1d','reviewer')
    o += prow('prj-24','Role-based access','',(9,14),'on track','On track','Tomas','Dec 3','4h','CI')
    o += prow('prj-08','Invoice PDF redesign','',(7,10),'on track','On track','Mira','Nov 26','2d','Mira')
    o += prow('prj-27','Queue depth dashboards','',(10,13),'on track','On track','Priya','Dec 8','2d','reviewer')
    o += prow('prj-19','Parquet export format','',(2,8),'on track','On track','Dani','Dec 12','1d','agent')
    o += prow('prj-31','Seat management API','',(1,9),'on track','Planned','Mira','Jan 16','4d','Mira')
    o += grp('No links · hand-set only', 4)
    o += prow('prj-35','On-call handbook rewrite','',None,'','At risk','Mira','Nov 30','2w','Mira')
    o += prow('prj-34','Offsite planning','',None,'','On track','you','Dec 14','1w','you')
    o += prow('prj-33','Q1 hiring loop','',None,'','Planned','Dani','Jan 15','6d','')
    o += prow('prj-36','Vendor security review','',None,'','Paused','Priya','','3w','')
    return o

PK = [('g','group by'),('s','state…'),('a','lead…')]
def write(name, s): (D/name).write_text(s)

write('projects-list.html', head('Projects - list','Projects') +
  '<div class="body"><div class="list">\n' + plist('cursor') + '</div></div>\n' + keys(PK) + END)

def mini(g, i, t, chips='', cls=''):
    return f'<div class="mini {cls}"><span class="g {g}"></span><span class="id">{i}</span><span class="t">{t}</span><span class="r">{chips}</span></div>\n'
peek = ('''<aside class="peek">
<div class="peek-head"><h2>Exports v2</h2><span class="icon">⤢</span><span class="icon">×</span></div>
<dl class="peek-fields" style="margin:0">
<dt>State</dt><dd>On track<span style="color:var(--ink-3)">hand-set</span></dd>
<dt>Derived</dt><dd><span class="chip att">at risk</span><span>2 PRs red, 1 review stale 3d</span></dd>
<dt>Lead</dt><dd><span class="av">D</span>Dani Okoye</dd>
<dt>Target</dt><dd>Nov 28<span style="color:var(--ink-3)">in 16d</span></dd>
<dt>Progress</dt><dd><span class="bar att"><i style="width:66%"></i></span>6/9</dd>
</dl>
<div class="peek-body" style="padding-top:4px">
<div class="sect" style="margin-top:6px">Work <span class="n">3 open of 9</span></div>
'''
 + mini('pr','#2931','Stream large CSV exports in chunks','<span class="chip err">failing</span>')
 + mini('pr','#2887','Export size limit message names the plan','<span class="chip err">failing</span>')
 + mini('pr','#2905','Retry failed export uploads with backoff','<span class="chip ok">passing</span><span style="color:var(--att)">review stale 3d</span>')
 + '<div class="sect"><span class="chev">▾</span>Done <span class="n">6</span></div>\n'
 + mini('merged','#2920','Export history page: filter by requester','merged 1d','done')
 + mini('merged','#2912','Export job progress in the toolbar','merged 3d','done')
 + mini('merged','#2899','Cache tax lookups for previews','merged 4d','done')
 + '<div class="mini done"><span></span><span></span><span class="t">3 more</span><span></span></div>\n'
 + '''<div class="sect" style="margin-top:12px">Latest update</div>
<div class="msgline"><span class="av">D</span><div><div class="upd-head"><span class="who">Dani Okoye<span class="when">4d</span></span><span class="chip ok">on track</span></div>
<div class="txt">Chunked upload landed. Retry work is the last big piece, expecting to wrap this week. Size limit copy is still in review.</div></div></div>
</div>
<div class="peek-actions"><button class="btn primary">Open <kbd>Enter</kbd></button><button class="btn">Post update… <kbd>Ctrl+Enter</kbd></button><button class="btn ghost">Copy id</button></div>
</aside>''')
write('projects-peek.html', head('Projects - peek','Projects') +
  '<div class="body peeking"><div class="list">\n' + plist('sel') + '</div>\n' + peek + '</div>\n' + keys(PK) + END)

TF = 'style="--facts: var(--col-fact) var(--col-fact-wide) var(--col-fact)"'
def trow(i, t, src, ref, who, age, sub='', cls='read', flag='', actions=''):
    sb = f'<span class="sub">{sub}</span>' if sub else ''
    return (f'<div class="row {cls} {flag}" {TF}><span class="g todo"></span><span class="title"><span class="id">{i}</span><span class="t">{t}</span>{sb}</span>'
            f'<span class="fact">{src}</span><span class="fact">{ref}</span><span class="fact"><span class="av">{who[0]}</span>{who}</span>'
            f'<span class="age">{age}</span><span class="wait"></span>{actions}</div>\n')
acts = '<span class="actions"><button class="btn primary">Accept</button><button class="btn">Decline</button><button class="btn">Duplicate…</button><button class="btn">Snooze…</button></span>'
tr = grp('New · nobody owns them', 13, 'all waiting on you · oldest first')
tr += trow('BIL-298','Refund email shows the wrong currency for CAD customers','Linear','Billing','Sana','6d','','unread')
tr += trow('gh-3088','CSV export drops rows with embedded newlines','GitHub','acme/exports-api','d-okafor','5d','1 comment','unread')
tr += trow('EXP-431','Export job stays queued after a worker restart','Linear','Exports','Mira','4d','maybe a duplicate of EXP-398','unread','cursor',acts)
tr += trow('gh-3094','Docs: bucket policy example uses a removed action','GitHub','acme/docs','h-lindqvist','4d')
tr += trow('WSP-110','Invite email lands in spam for new workspaces','Linear','Workspaces','Tomas','3d')
tr += trow('BIL-304','Proration preview rounds half cents the wrong way','Linear','Billing','Priya','3d','','unread')
tr += trow('gh-3101','Export history page is empty after a timezone change','GitHub','acme/web','j-marsh','2d','2 reactions')
tr += trow('OPS-38','Alert on exports queue depth over 500','Linear','Platform','Leo','2d')
tr += trow('EXP-436','Allow exporting a saved view, not only the full table','Linear','Exports','Dani','1d')
tr += trow('gh-3107','Question: can exports be scheduled hourly?','GitHub','acme/exports-api','k-ito','1d')
tr += trow('WSP-114','Audit log filter forgets the date range on reload','Linear','Workspaces','Sana','9h','','unread')
tr += trow('BIL-312','Webhook secret rotation docs mention the old header','Linear','Billing','Mira','5h','','unread')
tr += trow('gh-3110','Crash on login with an expired SSO session','GitHub','acme/web','p-vance','2h','','unread')
tr += grp('Snoozed until tomorrow', 2, '', '▸')
write('projects-triage.html', head('Projects - triage','Triage') +
  '<div class="body"><div class="list">\n' + tr + '</div></div>\n' + keys([('i','assign to me…'),('h','snooze…')]) + END)

mw = (D/'../grammar-2978/projects.html').read_text()
a = '<div class="body peeking"><div class="list">'
rows = mw[mw.index(a)+len(a):mw.index('</div>\n<aside class="peek">')]
rows = rows.replace('row sel unread','row cursor unread')
MK = [('g','group by'),('m','mine only'),('s','state…'),('a','assignee…'),('p','priority…'),('i','self-assign…')]
def popup(top, field, items):
    o = f'<div class="menu" style="left:44px;top:{top}px"><div class="mfield focus typed">{field}<span class="caret"></span></div>\n'
    for g, lab, cur, hl in items:
        o += f'<div class="ment{" on" if hl else ""}">{g}<span class="lab">{lab}</span>{"<span class=when>✓</span>" if cur else ""}</div>\n'
    o += '<div class="mfoot"><span><kbd>Enter</kbd>pick</span><span><kbd>Esc</kbd>cancel</span></div></div>\n'
    return o
st = [('<span class="g draft"></span>','Backlog',0,0),('<span class="g todo"></span>','Todo',0,0),('<span class="g doing"></span>','In progress',0,0),
      ('<span class="g review"></span>','In review',1,1),('<span class="g done"></span>','Done',0,0),('<span class="g fail"></span>','Canceled',0,0)]
av = lambda c: f'<span class="av">{c}</span>'
ps = [('<span class="g draft"></span>','No assignee',0,0),(av('Y'),'you',1,1),(av('M'),'Mira Voss',0,0),(av('T'),'Tomas Berg',0,0),(av('P'),'Priya Nair',0,0),(av('D'),'Dani Okoye',0,0),(av('L'),'Leo Hart',0,0)]
for name, pop in (('projects-picker.html', popup(420,'',st)), ('projects-picker-assign.html', popup(420,'',ps))):
    write(name, head('Projects - picker','My work') + '<div class="body"><div class="list">' + rows + '</div></div>\n' + keys(MK) + END.replace('</body>', pop + '</body>'))

def panel(t, body, k='', style=''):
    return f'<div class="panel" style="{style}"><div class="pt">{t}<span class="k">{k}</span></div><div class="pb">{body}</div></div>'
e1 = ('<div class="empty"><div class="eg"><span class="g done"></span></div><h2>Nothing to triage</h2>'
      '<p>New Linear and GitHub issues that nobody owns show up here. Right now every issue has an owner.</p>'
      '<div class="acts"><button class="btn primary">Open My work <kbd>Enter</kbd></button></div></div>')
e2 = ('<div class="empty"><div class="eg"><span class="g todo"></span></div><h2>No projects yet</h2>'
      '<p>A project groups issues and PRs and shows whether they are on track. Create one, or connect Linear to bring yours in.</p>'
      '<div class="acts"><button class="btn primary">Create project <kbd>c</kbd></button><button class="btn ghost">Connect Linear</button></div></div>')
def srow(pid,name,prog,der,age,cls=''):
    d,t=prog; bc={'on track':'ok','at risk':'att','blocked':'att'}[der]; dc={'on track':'ok','at risk':'att','blocked':'err'}[der]
    return (f'<div class="row read"><span class="g doing"></span><span class="title"><span class="t">{name}</span></span>'
            f'<span class="fact"><span class="bar {bc}"><i style="width:{int(100*d/t)}%"></i></span>{d}/{t}</span><span class="fact"><span class="chip {dc}">{der}</span></span><span class="age">{age}</span></div>')
rows3 = ''.join([grp('At risk',3), srow('prj-09','Billing rework',(14,22),'blocked','1d'), srow('prj-14','Exports v2',(6,9),'at risk','3d'), srow('prj-21','Workspace transfer',(3,11),'at risk','2d'),
   grp('On track',4), srow('prj-26','Webhook retries',(4,5),'on track','3h'), srow('prj-12','Usage dashboards',(18,20),'on track','1d'), srow('prj-24','Role-based access',(9,14),'on track','4h'), srow('prj-08','Invoice PDF redesign',(7,10),'on track','2d')])
e3 = ('<div class="state-line att"><span class="g parked"></span><span class="what">Linear connection expired<span class="sub">Linear projects last updated 3d ago</span></span>'
      '<span class="btns"><button class="btn primary">Reconnect Linear</button></span></div><div class="dim">' + rows3 + '</div>')
sheet = ('<div class="sheet">' + panel('Triage, empty','<div>'+e1+'</div>','#2961') + panel('Linear connection expired','<div>'+e3+'</div>','#2961','grid-row:span 2')
         + panel('Projects, no projects','<div>'+e2+'</div>','#2961') + '</div>')
write('projects-states.html', '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Projects, states</title>\n<link rel="stylesheet" href="'+G+'"><link rel="stylesheet" href="projects.css"></head><body>'+sheet+'</body></html>\n')

# self-assign confirm from Triage (key a): one popup under the cursor row, cancellable, Enter commits
conf = ('<div class="menu" style="left:44px;top:CONFTOPpx;width:320px"><div class="mhead">Assign EXP-431 to you?</div>\n'
        '<div class="parse">It leaves Triage and shows in My work.</div>\n'
        '<div class="ment on"><span class="g doing"></span><span class="lab">Assign to me</span><kbd>Enter</kbd></div>\n'
        '<div class="ment"><span class="g fail"></span><span class="lab">Cancel</span><kbd>Esc</kbd></div>\n'
        '<div class="mfoot"><span><kbd>Enter</kbd>assign</span><span><kbd>Esc</kbd>cancel</span></div></div>\n')
write('projects-assign-confirm.html', head('Projects - assign confirm','Triage') +
  '<div class="body"><div class="list">\n' + tr + '</div></div>\n' + keys([('i','assign to me…'),('h','snooze…')]) + END.replace('</body>', conf.replace('CONFTOPpx', '222px') + '</body>'))
