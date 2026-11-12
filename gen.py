import pathlib, re
D = pathlib.Path(__file__).parent
OLD = D.parent / 'projects-2961'
src = (OLD / 'gen.py').read_text().replace("def write(name, s): (D/name).write_text(s)", "def write(name, s): pass")
src = src.replace("D = pathlib.Path(__file__).parent", f"D = pathlib.Path({str(OLD)!r})")
ns = {'__file__': str(OLD / 'gen.py')}
exec(compile(src, 'old-gen', 'exec'), ns)
head, keys, END, plist, grp, prow, person, G = (ns[k] for k in ('head', 'keys', 'END', 'plist', 'grp', 'prow', 'person', 'G'))
def write(name, s): (D / f'pr-{name}.html').write_text(s)

GL, FACTS, wait, person = (ns[k] for k in ('GL', 'FACTS', 'wait', 'person'))
def prow(name, sub, prog, der, hand, lead, target, age, w, cls='read', flag='', link=False):
    # health words are Linear's: Off track / At risk / On track (spec 14). The hand-set value shows beside the derived one;
    # a difference is spelled out in the sub-line, never carried by a glyph or colour alone.
    gl = GL.get(hand, 'doing') if hand in ('Planned', 'Paused') else 'doing'
    if prog:
        d, t = prog; pct = int(100 * d / t)
        bc = {'on track': 'ok', 'at risk': 'att', 'off track': 'att'}.get(der, '')
        pf = f'<span class="fact"><span class="bar {bc}"><i style="width:{pct}%"></i></span>{d}/{t}</span>'
    else: pf = '<span class="fact"></span>'
    dc = {'on track': 'ok', 'at risk': 'att', 'off track': 'err'}.get(der)
    df = f'<span class="fact"><span class="chip {dc}">{der}</span></span>' if der else '<span class="fact"></span>'
    life = hand in ('Planned', 'Paused')
    hf = '<span class="fact"></span>' if life else f'<span class="fact">{hand}</span>'
    sb = f'<span class="sub">{sub}</span>' if sub else ''
    return (f'<div class="row {cls} {flag}" {FACTS}><span class="g {gl}"></span><span class="title"><span class="t">{name}</span>{sb}</span>'
            f'{pf}{df}{hf}{person(lead, " drop-peek")}<span class="fact drop-peek">{target}</span><span class="age">{age}</span>{wait(w)}</div>\n')
def plist(flag='', who='Exports v2'):
    def f(n): return flag if n == who else ''
    def mm(derived, hand, reason): # mismatch is spelled in words, never by a glyph or colour alone
        return f'{derived} · Linear says {hand.lower()} · {reason}'
    o = grp('Off track', 2, 'derived from each project’s PRs, CI and reviews')
    o += prow('Audit log retention', mm('Off track', 'At risk', 'main red 2d (api)'), (2, 6), 'off track', 'At risk', 'Leo', 'Nov 24', '6d', 'CI', 'read', f('Audit log retention'))
    o += prow('SSO rollout', 'Off track: main red 3d (web)', (5, 12), 'off track', 'Off track', 'Priya', 'Dec 1', '5d', 'reviewer', 'read', f('SSO rollout'))
    o += grp('At risk', 4)
    o += prow('Billing rework', 'At risk: 3 of 9 PRs red > 2 days', (14, 22), 'at risk', 'At risk', 'Mira', 'Dec 5', '1d', 'you', 'unread', f('Billing rework'))
    o += prow('Exports v2', mm('At risk', 'On track', '2 of 3 PRs red > 2 days'), (6, 9), 'at risk', 'On track', 'Dani', 'Nov 28', '3d', 'CI', 'unread', f('Exports v2'))
    o += prow('Workspace transfer', 'At risk: 3 of 11 done, 9 days left', (3, 11), 'at risk', 'At risk', 'Tomas', 'Nov 21', '2d', 'Tomas', 'read', f('Workspace transfer'))
    o += prow('Role-based access', 'At risk: 2 reviews waiting > 3 days', (9, 14), 'at risk', 'At risk', 'Tomas', 'Dec 3', '4h', 'reviewer', 'read', f('Role-based access'))
    o += grp('On track', 6)
    o += prow('Webhook retries', '', (4, 5), 'on track', 'On track', 'Leo', 'Nov 20', '3h', 'you', 'unread')
    o += prow('Usage dashboards', '', (18, 20), 'on track', 'On track', 'Priya', 'Nov 19', '1d', 'reviewer')
    o += prow('Invoice PDF redesign', '', (7, 10), 'on track', 'On track', 'Mira', 'Nov 26', '2d', 'Mira')
    o += prow('Queue depth dashboards', '', (10, 13), 'on track', 'On track', 'Priya', 'Dec 8', '2d', 'reviewer')
    o += prow('Parquet export format', '', (2, 8), 'on track', 'On track', 'Dani', 'Dec 12', '1d', 'agent')
    o += prow('Seat management API', '', (1, 9), 'on track', 'Planned', 'Mira', 'Jan 16', '4d', 'Mira')
    o += grp('No links', 4)
    o += prow('On-call handbook rewrite', 'Link a repo', None, '', 'At risk', 'Mira', 'Nov 30', '2w', 'Mira')
    o += prow('Offsite planning', 'Link a repo', None, '', 'On track', 'you', 'Dec 14', '1w', 'you')
    o += prow('Q1 hiring loop', 'Link a repo', None, '', 'Planned', 'Dani', 'Jan 15', '6d', '')
    o += prow('Vendor security review', 'Link a repo', None, '', 'Paused', 'Priya', '', '3w', '')
    return o
def colhead():
    return (f'<div class="row colhead" {FACTS}><span></span><span class="title"></span><span class="fact"></span><span class="fact">Health (derived)</span>'
            '<span class="fact">Lead’s call</span><span class="fact drop-peek">Lead</span><span class="fact drop-peek">Target</span><span class="age">Updated</span><span class="wait">Waiting on</span></div>\n')
SR_STYLE = 'position:absolute;bottom:36px;font:400 11px/16px var(--font);color:var(--ink-subtle);max-width:560px;z-index:2'
def sr(text, side='left', bottom=36):
    pos = 'left:24px' if side == 'left' else 'right:24px;text-align:right'
    return f'<div class="sr" style="{SR_STYLE};bottom:{bottom}px;{pos}"><b style="font-weight:500;color:var(--ink-muted)">Screen reader:</b> {text}</div>'
def btn(t, p=False, k='', cls=''):
    kk = f' <kbd>{k}</kbd>' if k else ''
    return f'<button class="btn{" primary" if p else ""} {cls}">{t}{kk}</button>'
def svg(d): return f'<svg viewBox="0 0 16 16">{d}</svg>'
PK = [('g', 'group by'), ('s', 'state…'), ('a', 'lead…')]

# ================= 1. #3168 peek: burn-up, PR throughput, health rules =================
def burnup():
    X0, X1, Y0, Y1 = 30, 404, 8, 72           # plot box; 44 days: 28 behind, 16 ahead
    dx = (X1 - X0) / 44; y = lambda v: Y1 - (Y1 - Y0) * v / 10
    tx = lambda d: X0 + dx * d
    today, tgt = tx(28), X1
    total = f'M{tx(0)} {y(7)}H{tx(12)}V{y(9)}H{tgt}'
    pts = [(0,0),(3,1),(6,2),(10,3),(13,4),(18,5),(24,6),(28,6)]
    done = 'M' + 'L'.join(f'{tx(d):.1f} {y(v):.1f}' for d, v in pts)
    grid = ''.join(f'<path d="M{X0} {y(v):.1f}H{X1}" stroke="#26272a" stroke-width="1"/><text x="4" y="{y(v)+3:.1f}">{v}</text>' for v in (0, 5, 10))
    return (f'<svg class="chart" width="408" height="92" viewBox="0 0 408 92">{grid}'
      f'<path d="M{today:.1f} {Y0}V{Y1}" stroke="#686870" stroke-dasharray="3 3"/><text x="{today-12:.1f}" y="86">today</text>'
      f'<path d="M{tgt} {Y0}V{Y1}" stroke="#8a8f98"/><text x="{tgt-34}" y="86">Nov 28</text>'
      f'<path d="{total}" fill="none" stroke="#8a8f98" stroke-width="1.5"/><path d="{done}" fill="none" stroke="#828fff" stroke-width="1.8"/>'
      f'<circle cx="{tx(28):.1f}" cy="{y(6):.1f}" r="3" fill="#828fff"/>'
      f'<text x="{tx(12)+6:.1f}" y="{y(9)+14:.1f}">scope +2, Nov 6</text><text x="{X0}" y="86">Oct 15</text></svg>')
def throughput():
    vals = [2, 3, 4, 2, 1, 1]; lab = ['6w', '5w', '4w', '3w', '2w', 'now']
    bars = ''
    for i, v in enumerate(vals):
        h = 7 * v; x = 34 + i * 62
        cur = i == len(vals) - 1
        bars += (f'<rect x="{x}" y="{44-h}" width="38" height="{h}" rx="2" fill="{"#828fff" if cur else "#686870"}"/>'
                 f'<text x="{x+19-3}" y="{41-h}">{v}</text><text x="{x+8}" y="56">{lab[i]}</text>')
    return f'<svg class="chart" width="408" height="58" viewBox="0 0 408 58"><path d="M30 44H404" stroke="#26272a"/>{bars}</svg>'
def sec(t, n=''): return f'<div class="sect" style="margin-top:10px">{t}{" <span class=n>" + n + "</span>" if n else ""}</div>'
FIELDS = ('<dl class="peek-fields" style="margin:0;padding-top:4px;padding-bottom:4px">'
  '<dt>Derived</dt><dd><span class="chip att">at risk</span><span>At risk: 2 of 3 PRs red > 2 days</span></dd>'
  '<dt>Linear says</dt><dd>on track<span style="color:var(--ink-subtle)">differs: Sindri says at risk</span></dd>'
  '<dt>Lead</dt><dd><span class="av">D</span>Dani Okoye</dd><dt>Target</dt><dd>Nov 28<span style="color:var(--ink-subtle)">in 16d</span></dd>'
  '<dt>Progress</dt><dd><span class="bar att"><i style="width:66%"></i></span>6/9</dd></dl>')
def rrule(grp, text, state='clear', hit=False):
    return f'<div class="{"hit" if hit else ""}"><b>{grp}</b><span>{text}</span><span>{state}</span></div>'
rules = ('<div class="rules">'
  + rrule('Off track', 'the target date has passed with open work')
  + rrule('', 'main CI has been red 2 days or more')
  + rrule('At risk', 'a linked PR’s CI has been red 2 days or more', 'matched: 2 of 3 PRs red', True)
  + rrule('', 'a review request or a changes-requested review has waited 3 days or more')
  + rrule('', 'under half done with 14 days or less to the target')
  + rrule('On track', 'it has links and no rule above matches', '')
  + rrule('No links', 'no PR or repo is linked; the row offers Link a repo', '') + '</div>')
PEEK1 = ('<aside class="peek"><div class="peek-head"><h2>Exports v2</h2><span class="icon">⤢</span><span class="icon">×</span></div>' + FIELDS
  + '<div class="peek-body" style="padding-top:0">'
  + sec('Burn-up', 'done over total, by day')
  + burnup() + '<div class="legend"><span><i style="background:#828fff"></i>done 6</span><span><i style="background:#8a8f98"></i>total 9</span></div>'
  + sec('PRs merged per week') + throughput()
  + sec('How health is worked out', 'first match wins; fixed') + rules
  + '</div><div class="peek-actions"><button class="btn primary">Open <kbd>Enter</kbd></button><button class="btn">Draft update… <kbd>Ctrl+Enter</kbd></button><button class="btn ghost">Copy id</button></div></aside>')
write('peek-burnup', head('Projects - peek, burn-up', 'Projects') + '<div class="body peeking"><div class="list nopad">\n' + colhead() + plist('sel') + '</div>\n' + PEEK1 + '</div>\n' + keys(PK) + END.replace('</body>', sr('row read as “Exports v2, project, at risk, 2 of 3 PRs red more than 2 days, Linear says on track, 6 of 9 done, lead Dani, target Nov 28, 3 days”. Each chart has a table behind it.') + '</body>'))

# ================= 2. #3169 triage: several rows picked, bulk bar =================
TF = 'style="--facts: var(--col-fact) var(--col-fact-wide) var(--col-fact)"'
ROWS = [('BIL-298','Refund email shows the wrong currency for CAD customers','Linear','Billing','Sana','6d','',1),
 ('gh-3088','CSV export drops rows with embedded newlines','GitHub','acme/exports-api','d-okafor','5d','1 comment',1),
 ('EXP-431','Export job stays queued after a worker restart','Linear','Exports','Mira','4d','maybe a duplicate of EXP-398',0),
 ('gh-3094','Docs: bucket policy example uses a removed action','GitHub','acme/docs','h-lindqvist','4d','',0),
 ('WSP-110','Invite email lands in spam for new workspaces','Linear','Workspaces','Tomas','3d','',0),
 ('BIL-304','Proration preview rounds half cents the wrong way','Linear','Billing','Priya','3d','',1),
 ('gh-3101','Export history page is empty after a timezone change','GitHub','acme/web','j-marsh','2d','2 reactions',0),
 ('OPS-38','Alert on exports queue depth over 500','Linear','Platform','Leo','2d','',0),
 ('EXP-436','Allow exporting a saved view, not only the full table','Linear','Exports','Dani','1d','',0),
 ('gh-3107','Question: can exports be scheduled hourly?','GitHub','acme/exports-api','k-ito','1d','',0),
 ('WSP-114','Audit log filter forgets the date range on reload','Linear','Workspaces','Sana','9h','',1),
 ('BIL-312','Webhook secret rotation docs mention the old header','Linear','Billing','Mira','5h','',1),
 ('gh-3110','Crash on login with an expired SSO session','GitHub','acme/web','p-vance','2h','',1)]
TACTS_GH = '<span class="actions"><button class="btn primary">Accept</button><button class="btn">Decline</button><button class="btn dis">Duplicate…</button><button class="btn">Snooze…</button></span>'
TACTS = '<span class="actions"><button class="btn primary">Accept</button><button class="btn">Decline</button><button class="btn">Duplicate…</button><button class="btn">Snooze…</button></span>'
def trow(r, picked=False, box=False, cursor=False, acts=''):
    i, t, s, ref, who, age, sub, unread = r
    cls = ('unread' if unread else 'read') + (' pk' if picked else '') + (' cursor' if cursor else '')
    g = f'<span class="g box{" on" if picked else ""}"></span>' if box else '<span class="g todo"></span>'
    sb = f'<span class="sub">{sub}</span>' if sub else ''
    return (f'<div class="row {cls}" {TF}>{g}<span class="title"><span class="id">{i}</span><span class="t">{t}</span>{sb}</span>'
            f'<span class="fact">{s}</span><span class="fact">{ref}</span><span class="fact"><span class="av">{who[0]}</span>{who}</span>'
            f'<span class="age">{age}</span><span class="wait"></span>{acts}</div>\n')
picked = {'EXP-431', 'gh-3094', 'WSP-110', 'BIL-304', 'gh-3107'}
def triage(rows, picked, cursor, n, acts, toast='', bulk=True, name='triage-select', box=True, hdrchip=True, srtext='', srside='left', extra=''):
    h = grp('New · nobody owns them', n, 'oldest first')
    for r in rows: h += trow(r, r[0] in picked, box, r[0] == cursor, ((TACTS_GH if r[0].startswith('gh-') else TACTS) if (r[0] == cursor and not picked) else ''))
    h += grp('Snoozed until tomorrow', 2, '', '▸')
    bar = ''
    if bulk:
        bar = (f'<div class="bulk"><span><b>{len(picked)} picked</b> of {n}</span><span style="color:var(--ink-subtle)">x toggles a row, Shift+j / Shift+k extends, Esc clears. Accept, Decline and Snooze share one 5 s Undo.</span><span class="sp"></span>'
               + btn('Snooze…') + btn('Duplicate…', cls='dis') + btn('Decline') + btn(f'Accept {len(picked)}', True, 'Ctrl+Enter') + '</div>')
    hd = head('Projects - triage', 'Triage')
    if hdrchip: hd = hd.replace('<div class="filter"', f'<span class="hpicked"><span class="g box on"></span>{len(picked)} picked</span><div class="filter"')
    kp = [('x','pick'),('Shift+j/k','extend'),('Ctrl+Enter','act on picked')] if bulk else [('x','pick'),('i','assign to me…'),('h','snooze…')]
    ft = keys(kp).replace('<span><kbd>c</kbd>create</span>', '').replace('<span><kbd>x</kbd>select</span>', '')
    if not bulk: hd = hd.replace('Triage<span class="n">13', f'Triage<span class="n">{n}')
    body = f'<div class="body col"><div class="list">\n{h}</div>{bar}</div>\n'
    return hd + body + ft + END.replace('</body>', toast + extra + (sr(srtext, srside, 96 if bulk else 36) if srtext else '') + '</body>')
TIP = 'position:absolute;z-index:6;background:var(--s3);border:1px solid var(--hair-strong);border-radius:var(--r2);padding:6px 10px;font-size:12px;line-height:18px;color:var(--ink);box-shadow:0 8px 24px rgba(0,0,0,.45)'
write('triage-select', triage(ROWS, picked, 'BIL-304', 13, TACTS, srtext='row read as “EXP-431, Linear, Billing, Mira, 4 days, picked, 3 of 5”; picking is announced as “5 picked”.', extra=f'<div style="{TIP};right:300px;bottom:84px">Duplicate works on Linear items only. 2 of the 5 picked are GitHub.</div>'))
rest = [r for r in ROWS if r[0] not in picked]
write('triage-after', triage(rest, set(), 'WSP-114', 8, TACTS, toast='<div class="toast" style="flex-direction:column;align-items:stretch;gap:6px;width:360px"><div style="display:flex;gap:12px;align-items:center"><span class="g done"></span><span>Accepted 5</span><span style="color:var(--ink-subtle)">·</span><u>Undo</u> <kbd>Ctrl+Z</kbd></div><div style="font-size:12px;color:var(--ink-subtle)">The Linear writes go out when the 5 s end. 3 s left.</div><div style="height:3px;background:var(--s4);border-radius:2px"><i style="display:block;height:100%;width:60%;background:var(--primary-hi);border-radius:2px"></i></div></div>', bulk=False, box=False, hdrchip=False, srtext='toast read once as “Accepted 5. Undo, Control Z, 5 seconds”; focus moves to the next row, WSP-114.', srside='right'))
write('triage-github', triage(ROWS, set(), 'gh-3088', 13, TACTS, bulk=False, box=False, hdrchip=False, srtext='row read as “gh-3088, GitHub issue, acme/exports-api, d-okafor, 5 days”; Duplicate is read as “unavailable, Linear items only”.', srside='left', extra=f'<div style="{TIP};right:24px;top:196px;width:380px">GitHub item. Accept adds it to the project in Sindri and writes nothing to GitHub. Decline hides it from Triage. Snooze works as for Linear. Duplicate works on Linear items only.</div>'))

# ================= 3. #3171 the . menu over the projects list =================
def menu(items, field, top=320, left=44, title='Exports v2'):
    o = f'<div class="menu" style="left:{left}px;top:{top}px;width:340px"><div class="mhead">{title}</div><div class="mfield focus{" typed" if field else ""}">{field or "<span class=ph>Type to filter</span>"}<span class="caret"></span></div>\n'
    for ic, lab, sub, k, on in items:
        o += f'<div class="ment{" on" if on else ""}{" dis" if sub == "No failing PR" else ""}">{svg(ic)}<span class="lab">{lab}</span><span class="sub">{sub}</span><kbd>{k}</kbd></div>\n'
    o += '<div class="mfoot"><span><kbd>letter</kbd>jump</span><span><kbd>Enter</kbd>pick</span><span><kbd>Esc</kbd>close</span></div></div>\n'
    return o
I_PR = '<circle cx="4.5" cy="4" r="1.6"/><circle cx="4.5" cy="12" r="1.6"/><circle cx="11.5" cy="12" r="1.6"/><path d="M4.5 5.6V10.4M11.5 10.4V7a2 2 0 0 0-2-2H8"/>'
I_RE = '<path d="M3 8a5 5 0 0 1 9-3M13 8a5 5 0 0 1-9 3"/><path d="M12 2v3h-3M4 14v-3h3"/>'
I_SC = '<circle cx="8" cy="8" r="5.5"/><circle cx="8" cy="8" r="2"/>'
I_AG = '<rect x="3" y="4" width="10" height="8" rx="2"/><path d="M6 8h.01M10 8h.01M8 2v2"/>'
ALL = [(I_PR, 'Open PR', '#2931, failing', 'o', 1), (I_RE, 'Re-run CI', 'on #2931', 'r', 0), (I_SC, 'Scope this', '', 's', 0), (I_AG, 'Ask an agent to fix', '', 'a', 0)]
MKS = '<div class="keys"><span><kbd>↑/↓</kbd>move</span><span><kbd>letter</kbd>jump</span><span><kbd>Enter</kbd>pick</span><span><kbd>Esc</kbd>close</span></div>'
def menupage(name, field, items, who='Exports v2', top=320, srtext='', extra=''):
    write(name, head('Projects - . menu', 'Projects') + '<div class="body"><div class="list nopad">\n' + colhead() + plist('cursor', who) + '</div></div>\n' + MKS + '</div>' + END.replace('</body>', menu(items, field, top, 44, who) + extra + sr(srtext, 'right') + '</body>'))
menupage('menu', '', ALL, srtext='menu read as “Exports v2 actions, menu, 4 items. Open PR, number 2931 failing, 1 of 4.” Letters move the highlight; Enter runs it.')
menupage('menu-filtered', 'ci', [(I_RE, 'Re-run CI', 'on #2931', 'r', 1)], srtext='“1 of 1 match: Re-run CI.” The match count is announced as you type.')
NOFAIL = [(I_PR, 'Open PR', 'No failing PR', 'o', 0), (I_RE, 'Re-run CI', 'No failing PR', 'r', 0), (I_SC, 'Scope this', '', 's', 1), (I_AG, 'Ask an agent to fix', '', 'a', 0)]
NOFAIL = [(a_, b_, c_, d_, e_) for a_, b_, c_, d_, e_ in NOFAIL]
menupage('menu-no-failing', '', NOFAIL, 'Workspace transfer', 352, 'Open PR and Re-run CI are read as “unavailable, no failing PR”.',
         f'<div style="{TIP};left:400px;top:380px">No failing PR on this project</div>')

# ================= 4. #3172 Draft update in the peek, and its reminder on Home =================
DRAFT = ('<div class="draft"><div class="hrow"><span>Health</span><span class="chip att">at risk</span><span>At risk: 2 of 3 PRs red > 2 days. Linear says on track</span></div>'
  '<div class="ta"><p>At risk.</p>'
  '<p><span class="k">Merged since Nov 8:</span></p><ul><li>#2920 export history filter by requester</li><li>#2912 export job progress in the toolbar</li><li>#2899 cache tax lookups for previews</li></ul>'
  '<p><span class="k">Open risks:</span> 2 of 3 PRs red > 2 days.</p>'
  '<p><span class="k">Next:</span> <span class="caret"></span></p></div>'
  '<div class="src">Drafted from the health reason and the PRs merged since the last update. Edit anything. Nothing is sent until you post. Esc closes and keeps the draft. Discard has a 5 s Undo.</div></div>')
PEEK4 = ('<aside class="peek"><div class="peek-head"><h2>Exports v2</h2><span class="id">update</span><span class="icon">⤢</span><span class="icon">×</span></div>' + FIELDS
  + '<div class="peek-body"><div class="sect" style="margin-top:2px">Project update <span class="n">last one 4d ago, on track</span></div>' + DRAFT
  + '</div><div class="peek-actions"><button class="btn primary">Post to Linear <kbd>Ctrl+Enter</kbd></button><button class="btn">Redraft</button><button class="btn ghost">Discard</button></div></aside>')
write('draft-update', head('Projects - draft update', 'Projects') + '<div class="body peeking"><div class="list nopad">\n' + colhead() + plist('sel') + '</div>\n' + PEEK4 + '</div>\n'
      + '<div class="keys"><span><kbd>Ctrl+Enter</kbd>post to Linear</span><span><kbd>Esc</kbd>close, the draft is kept</span><span><kbd>Tab</kbd>next field</span></div></div>' + END.replace('</body>', sr('“Project update, draft, editable text, at risk.” Posting is announced when Linear confirms; Discard is announced with its Undo.') + '</body>'))

# Home: the reminder row
HF = 'style="--facts:var(--col-fact) var(--col-fact) var(--col-fact-wide)"'
def hperson(n, k='p'): return f'<div class="fact"><span class="av{" agent" if k == "a" else ""}">{n[0].upper()}</span>{n}</div>' if n else '<div class="fact"></div>'
def hrow(g, t, why, obj, frm, age, wait='', cls='read', acts='', agek='p', agecls=''):
    w = '<div class="wait you">you</div>' if wait == 'you' else f'<div class="wait">{wait}</div>'
    a = f'<span class="actions">{acts}</span>' if acts else ''
    return (f'<div class="row {cls}" {HF}><span class="g {g}"></span><div class="title"><span class="t">{t}</span></div><div class="fact">{why}</div>'
            f'<div class="fact mono">{obj}</div>{hperson(frm, agek)}<div class="age {agecls}">{age}</div>{w}{a}</div>\n')
hh = grp('Needs you', 6)
hh += hrow('agent', 'meta: at 205k, recycle from a digest', 'blocks 3', 'lane meta', 'meta', '15m', '', 'unread', agek='a')
hh += hrow('todo', 'Post the Exports v2 update: your Friday check-in, last one 4d ago', 'update due', 'Exports v2', 'Sindri', 'today', 'you', 'unread cursor',
           btn('Draft update', True) + btn('Snooze'), agek='a', agecls='mark')
hh += hrow('pr', 'Review requested: System grid', 'blocks 2', '#3120', 'Dani Okoye', '1h40', '', 'unread')
hh += hrow('pr', 'Ready to merge: spacing scale on the 4 px grid', 'blocks 2', '#3099', 'qa', '68m', '', 'unread', agek='a')
hh += hrow('fail', 'Nightly usage rollup failed at step aggregate-seats', 'failed', 'wf/rollup', 'Sindri', '2h', '', 'read', agek='a')
hh += hrow('mention', 'Marco mentioned you: can you confirm the SSO cutoff date?', 'mention', 'linear', 'Marco Reyes', '1h10', '', 'read')
hh += grp('Moving: 3 agents', '', '', '▸')
tabs = ''.join(f'<a{" class=on" if n == "All" else ""}>{n}<span class="n">{c}</span></a>' for n, c in zip(['All', 'Reviews', 'Messages', 'Work', 'Agents'], [9, 3, 2, 2, 3]))
home = (f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Home - project update reminder</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="../home-3091/home.css"><link rel="stylesheet" href="projects.css"></head>
<body><div class="app">
<div class="topbar"><span class="wordmark">Sindri</span><nav class="nav"><a class="on">Home<span class="n">9</span></a><a>Reviews<span class="n">4</span></a><a>Items<span class="n">31</span></a><a>Workflows</a><a>Agents<span class="n">3</span></a><a>Projects</a><a>Context</a></nav>
<span class="spacer"></span><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><span class="status-pill">Connected</span></div>
<div class="header"><h1>Home</h1><div class="tabs">{tabs}</div><div class="filter">Filter<kbd style="margin-left:auto">/</kbd></div></div>
<div class="body"><div class="list">
{hh}</div></div>
<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Space</kbd>peek</span><span><kbd>Enter</kbd>open</span><span><kbd>Esc</kbd>back</span><span><kbd>/</kbd>filter</span><span><kbd>.</kbd>actions</span><span><kbd>Ctrl+K</kbd>palette</span></div>
</div></body></html>
''')
write('home-update-reminder', home.replace('</body>', sr('row read as “Post the Exports v2 update, update due, project Exports v2, from Sindri, today, waits on you”. Actions are buttons only: Draft update, Snooze.') + '</body>'))

# ================= 5. #3170 the one-line create window from the tray hotkey =================
def desk(inner, srtext=''):
    return (f'<!doctype html>\n<html lang="en"><head><meta charset="utf-8"><title>Create an issue</title><link rel="stylesheet" href="{G}"><link rel="stylesheet" href="projects.css"></head>'
            f'<body><div class="desk"></div><div class="deskbar"><span class="tray">Sindri</span><span>New issue <kbd>Ctrl+Alt+I</kbd></span><span class="sp"></span><span>Thu 12 Nov 09:14</span></div>{inner}' + (f'<div class="sr" style="position:absolute;bottom:20px;left:0;right:0;text-align:center;font:400 11px/16px var(--font);color:var(--ink-subtle)"><b style="font-weight:500;color:var(--ink-muted)">Screen reader:</b> {srtext}</div>' if srtext else '') + '</body></html>\n')
KIND = '<span class="kind"><a class="on">Issue</a><a>Note</a></span>'
CW = ('<div class="cwin"><div class="in">' + KIND + '<span class="g todo"></span><span class="t">Retry timeout on export upload is 5 s, make it configurable<span class="caret" style="display:inline-block;vertical-align:-4px;margin-left:2px"></span></span></div>'
      '<div class="pre"><span class="pc"><span>Project</span>Exports v2 ▾</span><span class="pc"><span>Assignee</span>you</span><span class="sp"></span><span class="pc"><span>in</span>Linear · EXP</span></div>'
      '<div class="ft"><span><kbd>Enter</kbd>create in Linear</span><span><kbd>Esc</kbd>close</span><span><kbd>Tab</kbd>change project</span><span><kbd>Ctrl+Tab</kbd>Issue / Note</span></div></div>')
CWNS = CW.replace(KIND, '').replace('<span><kbd>Ctrl+Tab</kbd>Issue / Note</span>', '')
NOTE1 = '<div class="cnote" style="position:absolute;left:50%;top:372px;transform:translateX(-50%);font-size:12px;color:var(--ink-subtle);width:600px">Ctrl+Tab switches the kind. The window opens on the last kind used.</div>'
NOTE0 = NOTE1.replace('Ctrl+Tab switches the kind. The window opens on the last kind used.', 'No notes folder is set in Settings, so the switch is hidden and the window reads issue-only.')
write('create-window', desk(CW + NOTE1, '“New issue, dialog. Kind switch, Issue selected, 1 of 2. Issue title, edit text. Project Exports v2. Assignee you.” Control Tab switches kind; Enter creates, Escape closes.'))
write('create-window-no-switch', desk(CWNS + NOTE0, '“New issue, dialog. Issue title, edit text. Project Exports v2. Assignee you.” There is no kind switch until a notes folder is set.'))
write('create-window-empty', desk(CW.replace('Retry timeout on export upload is 5 s, make it configurable<span class="caret" style="display:inline-block;vertical-align:-4px;margin-left:2px"></span>', '<span class="t ph" style="color:var(--ink-subtle)">Issue title</span>'), '“New issue, dialog. Issue title, edit text, empty.”'))
CWERR = CW.replace('<div class="pre">', '<div class="state-line err" style="margin:0;border-radius:0"><span class="g fail"></span><span class="what">Linear can’t be reached. Your text is kept.</span><span class="btns">' + btn('Retry', True) + '</span></div><div class="pre">')
write('create-window-error', desk(CWERR, '“Linear can’t be reached. Your text is kept. Retry button.” The window stays open and focus stays in the title.'))
write('create-window-done', desk('<div class="notif"><div class="h">Sindri</div><div>Created <span class="id">EXP-437</span> in Exports v2</div><div style="color:var(--ink-subtle);font-size:12px">Retry timeout on export upload is 5 s, make it configurable</div><div class="acts">' + btn('Open in Linear') + btn('Copy link') + '</div></div>', '“Notification: Created EXP-437 in Exports v2. Actions: Open in Linear, Copy link.”'))

# ================= #3170 Settings: the shortcut the system refused =================
# Layout copied from the real SettingsView (src/Sindri/Aw.Sindri.Ui/Views/SettingsView.axaml): a page title, then sections stacked
# 24 px apart. A section is a detail-title heading over a Surface1 card (hairline border, radius 12, padding 16) whose rows are
# a body-strong label with a caption under it on the left and the control on the right, hairline between rows.
def setrow(label, sub, right, first=False):
    d = '' if first else '<div class="sdiv"></div>'
    return f'{d}<div class="srw"><div class="stx"><div class="sl">{label}</div><div class="sc">{sub}</div></div><div class="sctl">{right}</div></div>'
SHORT = ('<div class="ssec"><div class="shd">Shortcuts</div><div class="scard">'
  + setrow('New issue from anywhere', 'Opens the one-line create window, even with the main window closed.', '<kbd>Ctrl+Alt+I</kbd>' + btn('Change'), True)
  + '<div class="serr"><span class="g fail"></span><span>The OS refused this hotkey (taken, or no global-shortcuts portal on Wayland). Use the tray menu’s New issue.</span><span class="sp"></span>' + btn('Choose another shortcut', True) + '</div>'
  + '</div></div>')
THISAPP = ('<div class="ssec"><div class="shd">This app</div><div class="scard">'
  + setrow('Reduce motion', 'Turns off the slides and fades.', '<span class="tgl"><i></i></span>', True)
  + '<div class="sdiv"></div><div class="srw" style="display:block"><div style="display:flex;justify-content:space-between"><div class="sl">Text size</div><div class="sm">100%</div></div>'
    '<div class="trk"><i></i><b></b></div><div class="ticks"><span>90%</span><span>100%</span><span>130%</span></div></div></div></div>')
cont = (f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Settings - shortcut refused</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="projects.css"></head>
<body><div class="app" style="grid-template-rows:var(--bar-h) 1fr var(--keys-h)">
<div class="topbar"><span class="wordmark">Sindri</span><nav class="nav"><a>Home<span class="n">9</span></a><a>Reviews<span class="n">4</span></a><a>Items<span class="n">31</span></a><a>Workflows</a><a>Agents<span class="n">3</span></a><a>Projects</a><a>Context</a></nav>
<span class="spacer"></span><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><span class="status-pill">Connected</span></div>
<div class="settings"><div class="spt">Settings</div>
{SHORT}{THISAPP}</div>
<div class="keys"><span><kbd>Tab</kbd>next</span><span><kbd>Enter</kbd>change</span><span><kbd>Esc</kbd>back</span><span><kbd>Ctrl+K</kbd>palette</span></div>
</div></body></html>
''')
TRAY = ('<div class="menu" style="right:14px;top:34px;left:auto;width:200px;padding:4px 0">'
        '<div class="ment on"><span class="lab">New issue</span></div><div class="ment"><span class="lab">Open Sindri</span></div></div>')
write('settings-hotkey-refused', cont.replace('</body>', TRAY + sr('the Shortcuts heading is a level 2 heading; the row reads “New issue from anywhere, shortcut Control Alt I”, then the error line, then “Choose another shortcut, button”.') + '</body>'))

# ================= round 3d: projects-list and projects-peek redrawn on section 14's final rules =================
mini = ns['mini']
def writeraw(name, html): (D / f'{name}.html').write_text(html)
HOVER = (f'<div style="{TIP};left:690px;top:50px;white-space:nowrap">Health derived from PRs, CI and reviews</div>'
         f'<div style="{TIP};left:940px;top:50px;white-space:nowrap">Lead’s call: the health the project lead set in Linear</div>')
writeraw('projects-list', head('Projects - list', 'Projects') + '<div class="body"><div class="list nopad">\n' + colhead() + plist('cursor') + '</div></div>\n' + keys(PK)
         + END.replace('</body>', HOVER + sr('column headers read “Health, derived”, “Lead’s call”, “Lead”, “Target”, “Updated”, “Waiting on”; a row reads “Exports v2, project, at risk, Linear says on track, 6 of 9 done, lead Dani, target Nov 28, 3 days, waits on CI”.') + '</body>'))
PEEKL = ('<aside class="peek"><div class="peek-head"><h2>Exports v2</h2><span class="icon">⤢</span><span class="icon">×</span></div>' + FIELDS
  + '<div class="peek-body" style="padding-top:0"><div class="sect" style="margin-top:6px">Work <span class="n">3 open of 9</span></div>'
  + mini('pr', '#2931', 'Stream large CSV exports in chunks', '<span class="chip err">failing</span>')
  + mini('pr', '#2887', 'Export size limit message names the plan', '<span class="chip err">failing</span>')
  + mini('pr', '#2905', 'Retry failed export uploads with backoff', '<span class="chip ok">passing</span>')
  + '<div class="sect"><span class="chev">▾</span>Done <span class="n">6</span></div>'
  + mini('merged', '#2920', 'Export history page: filter by requester', 'merged 1d', 'done')
  + mini('merged', '#2912', 'Export job progress in the toolbar', 'merged 3d', 'done')
  + '<div class="mini done"><span></span><span></span><span class="t">4 more</span><span></span></div>'
  + '<div class="sect" style="margin-top:10px">Latest update</div>'
  + '<div class="msgline"><span class="av">D</span><div><div class="upd-head"><span class="who">Dani Okoye<span class="when">4d</span></span><span class="chip ok">on track</span></div>'
  + '<div class="txt">Chunked upload landed. Retry work is the last big piece.</div></div></div></div>'
  + '<div class="peek-actions"><button class="btn primary">Open <kbd>Enter</kbd></button><button class="btn">Draft update… <kbd>Ctrl+Enter</kbd></button><button class="btn ghost">Copy id</button></div></aside>')
writeraw('projects-peek', head('Projects - peek', 'Projects') + '<div class="body peeking"><div class="list nopad">\n' + colhead() + plist('sel') + '</div>\n' + PEEKL + '</div>\n' + keys(PK)
         + END.replace('</body>', sr('the peek reads “Exports v2, project. Derived: at risk, 2 of 3 PRs red more than 2 days. Linear says: on track.” Work and Done are lists with PR state.') + '</body>'))
