import pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'
NAV = ['Home','Reviews','Items','Workflows','Agents','Projects','Context']
def topbar(off=False):
    a = ''.join(f'<a{" class=on" if x=="Agents" else ""}>{x}' + ('<span class="n">2</span>' if x=='Agents' else '') + '</a>' for x in NAV)
    return f'<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav">{a}</nav><div class="spacer"></div><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><div class="status-pill">Healthy</div></div>'
def kbd(k): return f'<kbd>{k}</kbd>'
def btn(t, k='', cls=''): return f'<span class="btn {cls}">{t}{(" " + kbd(k)) if k else ""}</span>'

# ---- list column (spec 15.3) ----
LROWS = [
 ('grp','Needs you',2),
 ('you','#3078','ideas: at 210k, recycle from a digest','recycle','sel'),
 ('pr','#3067','qa: PR ready in the merge queue','PR ready',''),
 ('grp','Working',6),
 ('agent','GH-2959','context: mapping the 10M-line index','working',''),
 ('agent','#3101','design: building RowList','working',''),
 ('agent','','engine: wiring the terminal route','working',''),
 ('agent','','perf: timing the pane repaint','working',''),
 ('agent','','ui: drawing the cell grid','working',''),
 ('agent','','ux: walking the new-agent form','working',''),
 ('grp','Idle',1),
 ('todo','','api: idle 31 min','idle',''),
]
def lst(rows, focus, narrow=False, cursor_i=None):
    h = ''
    for i, r in enumerate(rows):
        if r[0] == 'grp':
            lab = f'<span class="lab">{r[1]} <span class="n">{r[2]}</span></span>'
            h += f'<div class="group">{lab if not narrow else "<span>"+str(r[2])+"</span>"}</div>'
        else:
            g, id_, t, why, cls = r
            chip = {'recycle':'<span class="chip att">recycle</span>','PR ready':'<span class="chip ok">PR ready</span>'}.get(why, why)
            idh = f'<span class="id">{id_}</span>'
            cur = ' cursor' if (cursor_i == i and not focus) else ''
            h += f'<div class="lrow {cls}{cur}"><span class="g {g}"></span><div class="title">{idh}<span class="t">{t}</span></div><div class="why">{chip}</div></div>'
    return h
def oneoff_rows(title='claude in sindri', chip='Starting'):
    r = list(LROWS)
    r.insert(1, ('grp','Needs you',2)); r.pop(1)
    # a Starting one-off in Working, selected
    out = []
    for x in LROWS:
        if x == ('grp','Working',6): x = ('grp','Working',7)
        if x[0] == 'agent' and x[1] == 'GH-2959':
            out.append(('none','',title,f'<span class="chip att">{chip}</span>','sel'))
        out.append(('you','#3078','ideas: at 210k, recycle from a digest','recycle','') if x[1]=='#3078' else x)
    return out

# ---- terminal content ----
def cur(focus): return '' if focus is None else f'<span class="cur{"" if focus else " hollow"}"></span>'
def lane_screen(focus):
    return (
'<span class="a">●</span> <span class="hi">Read</span>(docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md)\n'
'  <span class="dim">⎿  Read 1033 lines</span>\n\n'
'<span class="a">●</span> Context is at 210k. I will write a digest of what is open, then ask meta to recycle me.\n\n'
'<span class="a">●</span> <span class="hi">Bash</span>(git -C ~/.agentic-workflow/aw-wt/ideas status --short)\n'
'  <span class="dim">⎿  ?? docs/superpowers/specs/2026-11-12-ideas-digest.md</span>\n\n'
'<span class="a">●</span> <span class="hi">Write</span>(docs/superpowers/specs/2026-11-12-ideas-digest.md)\n'
'  <span class="dim">⎿  Wrote 64 lines</span>\n\n\n\n\n\n\n\n\n\n\n\n'
f'<span class="box">&gt; {cur(focus)}</span>'
'<span class="dim sline"><span>  ? for shortcuts</span><span>context 210k</span></span>')
def trust_screen(focus, slug='claude-code', folder=None):
    return (
'<span class="a">✻ Welcome to Claude Code</span>\n\n\n'
'<span class="box"><span class="att">Do you trust the files in this folder?</span>\n\n'
f' {folder or "~/.agentic-workflow/aw-wt/oneoff-261112-1409-" + slug}\n\n'
' <span class="hi">❯ 1. Yes, proceed</span>\n'
'   2. No, exit\n </span>\n'
f'  <span class="dim">Enter to confirm · Esc to cancel</span> {cur(focus)}')

def term(inner, focus, dimmed=False, overlay=''):
    return f'<div class="pwrap"><div class="term{" focus" if focus else ""}{" dimmed" if dimmed else ""}"><div class="scr">{inner}</div>{overlay}</div></div>'

def phead(name, state, scls='', extra='', sock='tmux default · 1 other client', btns=None):
    b = btns if btns is not None else btn('Close') + btn('Stop…')
    return (f'<div class="phead"><span class="crumb">Agents ›</span><span class="name">{name}</span>{btn("Transcript")}'
            f'{f'<span class="state {scls}"><i></i>{state}</span>' if state else ''}{extra}<span class="sp"></span><span class="sock">{sock}</span>{b}</div>')

LIST_FOOT = lambda n: f'<div class="keys"><span class="hl">Enter again, or F6, to type in {n}</span><span class="keys-div"></span><span>{kbd("Esc")}close</span><span>{kbd("n")}new</span></div>'
PANE_FOOT = lambda n: f'<div class="keys"><span class="hl">Typing goes to {n}</span><span class="keys-div"></span><span>{kbd("F6")} or {kbd("Ctrl+Shift+L")} back to the list</span><span>{kbd("Shift+PgUp")}scroll back</span></div>'

def page(title, w, head, lstfocus, rows, tm, foot, narrow=False, cursor_i=None, extra='', off=False):
    css = f'body{{width:{w}px}}' + (".cmdk{width:28px;padding:0;justify-content:center;font-size:0;gap:0}.cmdk kbd{display:none}.cmdk::before{content:'\\2315';font-size:16px;color:var(--ink-subtle)}" if w < 1100 else '')
    return (f'<!doctype html>\n<html lang="en"><head><meta charset="utf-8"><title>{title}</title><link rel="stylesheet" href="{G}"><link rel="stylesheet" href="agents3340.css"><style>{css}</style></head>\n'
            f'<body><div class="app">{topbar(off)}{head}<div class="split{" narrow" if narrow else ""}"><div class="lst{" focus" if lstfocus else ""}">{lst(rows, lstfocus, narrow, cursor_i)}</div>{tm}</div>{foot}{extra}</div></body></html>\n')

OUT = []
def w(name, html): (D / f'{name}.html').write_text(html); OUT.append(name)
def sel(rows, i):
    out = []
    for k, r in enumerate(rows):
        if r[0] == 'grp': out.append(r)
        else: out.append(r[:4] + ('sel' if k == i else '',))
    return out
def card(h, body, btns=''):
    return f'<div class="ov"><div class="card"><h3>{h}</h3>{body}<div class="btns">{btns}</div></div></div>'

def variant(name, title, head, rows, screen_fn, pane_focus, overlay='', dimmed=False, target='ideas', cursor_i=None, widths=(1440, 1000), off=False):
    for width in widths:
        nar = width == 1000
        tm = term(screen_fn(pane_focus), pane_focus, dimmed, overlay)
        foot = PANE_FOOT(target) if pane_focus else LIST_FOOT(target)
        sfx = '-focus-pane' if pane_focus else '-focus-list'
        w(f'{name}{sfx}' + ('-1000' if nar else ''),
          page(f'{title} ({"focus in the pane" if pane_focus else "focus in the list"})', width, head, not pane_focus, rows, tm, foot, nar, cursor_i=None if pane_focus else cursor_i, off=off))

def both(name, title, head, rows, screen_fn, overlay='', dimmed=False, target='ideas', cursor_i=1, panes=(False, True), widths=(1440, 1000), off=False):
    for pf in panes:
        variant(name, title, head, rows, screen_fn, pf, overlay, dimmed, target, cursor_i, widths, off)

blank = lambda f: ''
ROWS_ENDED = sel(LROWS + [('grp','Recently ended',1),('draft','','Add a retry test for the queue worker','Ended','')], 13)
ROWS_DESIGN = sel(LROWS, 5)
def endbar(path='~/.agentic-workflow/aw-wt/oneoff-261112-1409-add-a-retry', discard=True, refused=False, ring=False):
    h = '<div class="ov" style="align-items:flex-end;padding-top:0"><div class="endbar"><div class="m">Ended 14:02</div>'
    if refused:
        h += ('<div class="refuse">Couldn\'t remove the worktree: it has changes. <span class="mono">SND-AGENT-018</span>'
              '<div class="gs"> M src/Sindri/Aw.Sindri.Tests/RetryQueueTests.cs<br>?? notes.md</div></div>')
    if path: h += f'<div class="m2">Worktree <span class="mono">{path}</span></div>'
    h += '<div class="btns">' + btn('Copy resume command', '', 'kfocus' if ring else '') + (btn('Remove worktree') if path else '') + (btn('Discard session') if discard else '') + '</div></div></div>'
    return h
ENDED_BAR = endbar()
TAIL = 'Read(docs/superpowers/specs/2026-11-12-sindri-agents-page-design.md)<br>Bash(dotnet build -warnaserror)<br>  Build succeeded, 0 warnings<br>Write(src/Sindri/Aw.Sindri.Ui/RowList.cs)'
def noatt(ring=False):
    return ('<div class="ov"><div class="card" style="width:560px"><h3>Warp tab: Sindri can\'t show or type into this terminal.</h3>'
  '<div class="tail">' + TAIL + '</div><div class="btns">' + btn('Jump to it','o','kfocus' if ring else '') + btn('Copy resume command') + '</div></div></div>')
NOATT = noatt()
GONE = card('tmux session gone', '', btn('Copy resume command'))

PROMPT = "Add a retry test for the queue worker"
SLUG = 'add-a-retry'
ROWS_DESIGN = sel(LROWS, 5); ROWS_DESIGN[5] = ('agent','#3101','design: building RowList',"Can't see this terminal",'sel')
WARP = 'warp tab'
def foot_card(label): return f'<div class="keys"><span class="hl">F6 to {label}</span><span class="keys-div"></span><span>{kbd("Esc")}close</span><span>{kbd("n")}new</span></div>'
def foot_cardfocus(label): return f'<div class="keys"><span class="hl">Enter to {label} · F6 back to the list</span></div>'
def one(name, title, head, rows, scr, overlay='', dimmed=False, foot=None, cursor_i=None, lstfocus=True, tfocus=False, widths=(1440, 1000)):
    for width in widths:
        nar = width == 1000
        w(name + ('-1000' if nar else ''), page(title, width, head, lstfocus, rows, term(scr, tfocus, dimmed, overlay), foot, nar, cursor_i=cursor_i if lstfocus is False else cursor_i))

both('agents3340-1-lane', 'Sindri · Agents · lane pane, attached', phead('ideas', 'Attached', 'ok'), LROWS, lane_screen)
both('agents3340-2-oneoff', 'Sindri · Agents · one-off pane, starting', phead('claude in sindri', 'Starting', 'att', sock='tmux sindri'), oneoff_rows(), trust_screen, target='claude in sindri', cursor_i=None)
both('agents3340-2b-oneoff-4s', 'Sindri · Agents · one-off, 4 s after a worktree start', phead(PROMPT, 'Starting', 'att', sock='tmux sindri'), oneoff_rows(PROMPT, 'Answer in the pane'), lambda f: trust_screen(f, SLUG), target=PROMPT, cursor_i=None, panes=(False,))
both('agents3340-3a-connecting', 'Sindri · Agents · pane, connecting', phead('ideas', 'Connecting', 'att'), LROWS, blank)
one('agents3340-3b-reconnecting-focus-list', 'Sindri · Agents · pane, reconnecting', phead('ideas', 'Reconnecting', 'att'), LROWS, lane_screen(False), '', True, f'<div class="keys"><span class="hl">Reconnecting</span><span class="keys-div"></span><span>{kbd("Esc")}close</span><span>{kbd("n")}new</span></div>', lstfocus=True)
def cardpair(name, title, head, rows, scr, ov, label, dimmed=True):
    one(name + '-focus-list', title + ', focus in the list', head, rows, scr, ov(False), dimmed, foot_card(label), lstfocus=True)
    one(name + '-focus-card', title + ', focus on the card', head, rows, scr, ov(True), dimmed, foot_cardfocus(label), lstfocus=False, tfocus=True)
def kf(r, cls=''): return (cls + (' kfocus' if r else '')).strip()
cardpair('agents3340-4-cant-reach', 'Sindri · Agents · pane, can\'t reach Sindri', phead('ideas', 'Can\'t reach Sindri', 'err', btns=btn('Close')), LROWS, lane_screen(False),
         lambda r: card('Can\'t reach Sindri', '<p>Esc closes the view.</p>', btn('Retry now', '', kf(r, 'primary'))), 'Retry now')
cardpair('agents3340-5-take-it-back', 'Sindri · Agents · pane, open in another window', phead('ideas', '', ''), LROWS, lane_screen(False),
         lambda r: card('This pane is open in another Sindri window', '', btn('Take it back', '', kf(r, 'primary'))).replace('class="btns"', 'class="btns" style="margin-top:12px"'), 'Take it back')
cardpair('agents3340-6-cant-attach', 'Sindri · Agents · can\'t attach', phead('design', 'Can\'t attach', 'att', sock=WARP, btns=btn('Close')), ROWS_DESIGN, '', noatt, 'Jump to it', dimmed=False)
cardpair('agents3340-7-gone', 'Sindri · Agents · gone', phead('ideas', 'Gone', 'err', sock='tmux default', btns=btn('Close')), LROWS, lane_screen(None),
         lambda r: card('tmux session gone', '', btn('Copy resume command', '', kf(r))), 'Copy resume command')
LANE_ENDED = sel(LROWS + [('grp','Recently ended',1),('draft','','ideas: at 210k, recycle from a digest','Ended','')], 13)
HE = phead(PROMPT, 'Ended', 'end', sock='tmux sindri', btns=btn('Close'))
cardpair('agents3340-8-ended', 'Sindri · Agents · ended one-off', HE, ROWS_ENDED, trust_screen(None, SLUG), lambda r: endbar(ring=r), 'Copy resume command')
cardpair('agents3340-8b-ended-remove-refused', 'Sindri · Agents · ended one-off, remove worktree refused', HE, ROWS_ENDED, trust_screen(None, SLUG), lambda r: endbar(refused=True, ring=r), 'Copy resume command')
TITLE_C = 'Explain how the retry queue backs off'
ROWS_ENDED_C = sel(LROWS + [('grp','Recently ended',1),('draft','',TITLE_C,'Ended','')], 13)
HE_C = phead(TITLE_C, 'Ended', 'end', sock='tmux sindri', btns=btn('Close'))
cardpair('agents3340-8c-ended-no-worktree', 'Sindri · Agents · ended one-off, no worktree', HE_C, ROWS_ENDED_C, trust_screen(None, folder='~/work/sindri'), lambda r: endbar(path=None, ring=r), 'Copy resume command')
cardpair('agents3340-8d-ended-lane', 'Sindri · Agents · ended lane', phead('ideas', 'Ended', 'end', sock='tmux default', btns=btn('Close')), LANE_ENDED, lane_screen(None), lambda r: endbar(path=None, discard=False, ring=r), 'Copy resume command')

# ---- New agent form (15.4) ----
ROWS_P = [('grp','Needs you',2),('you','#3078','ideas: at 210k, recycle from a digest','recycle',''),('pr','#3067','qa: PR ready in the merge queue','PR ready',''),
          ('grp','Working',6),('agent','GH-2959','context: mapping the 10M-line index','working',''),('agent','#3101','design: building RowList','working',''),
          ('agent','','engine: wiring the terminal route','working',''),('agent','','perf: timing the pane repaint','working',''),
          ('agent','','ui: drawing the cell grid','working',''),('agent','','ux: walking the new-agent form','working','')]
def page_list_html(starting=False):
    rows = ROWS_P
    if starting:
        rows = [(('grp','Working',7) if r==('grp','Working',6) else r) for r in ROWS_P]
        k = rows.index(('agent','GH-2959','context: mapping the 10M-line index','working',''))
        rows.insert(k, ('none','','claude in sindri','Starting',''))
    return '<div class="list">' + ''.join(
        (f'<div class="group">{r[1]} <span class="n">{r[2]}</span></div>' if r[0]=='grp' else
         f'<div class="lrow {r[4]}" style="grid-template-columns:16px minmax(0,1fr) 160px;padding:0 var(--inset)"><span class="g {r[0]}"></span><div class="title"><span class="id">{r[1]}</span><span class="t">{r[2]}</span></div><div class="why">{r[3]}</div></div>') for r in rows) + '</div>'
def field(label, inner, cls='', extra='', err=''):
    e = f'<div class="ferr">{err}</div>' if err else ''
    return f'<div class="fl"><label>{label}</label><div class="inp {cls}">{inner}</div>{e}{extra}</div>'
def form(prompt, repo='sindri', agent='Claude Code', wt=False, focus='prompt', locked=False, start='Start (Ctrl+Enter)', errs=None, foot_extra='', fine='', cnt=None, caret=True, repo_note=''):
    errs = errs or {}
    lk = ' locked' if locked else ''
    if prompt:
        ph = f'{prompt}' + ('<span class="caret"></span>' if focus=='prompt' and not locked and caret else '')
    else:
        ph = '<span class="ph">Empty starts the agent at its own prompt</span>' + ('<span class="caret"></span>' if focus=='prompt' and not locked else '')
    n = cnt if cnt is not None else f'{len(prompt)}'
    perr = 'err ' if 'prompt' in errs else ''
    pf = field('Prompt', ph, 'ta ' + perr + ('focus ' if focus=='prompt' and not locked and not perr else '') + lk, f'<div class="cnt{" err" if "prompt" in errs else ""}">{n} / 8,000</div>', errs.get('prompt',''))
    rf = field('Repo', f'{repo}<span class="chev">▾</span>', ('err ' if 'repo' in errs else '') + ('focus ' if focus=='repo' else '') + lk, repo_note, errs.get('repo',''))
    af = field('Agent', f'{agent}<span class="chev">▾</span>', ('err ' if 'agent' in errs else '') + lk, '', errs.get('agent',''))
    wf = f'<div class="fl"><div class="sw-row"><span class="sw{" on" if wt else ""}"></span>New worktree</div></div>'
    body = pf + rf + af + wf + fine
    foot = foot_extra + f'<div class="sfoot"><span class="sp"></span>{btn(start, "", "primary")}</div>'
    return f'<aside class="sheet"><div class="sheet-head"><h2>New agent</h2>{kbd("Esc")}</div><div class="sbody">{body}</div>{foot}</aside>'
def fpage(title, sheet, foot=None, starting=False):
    foot = foot or f'<div class="keys"><span>{kbd("Tab")}next field</span><span>{kbd("Ctrl+Enter")}start</span><span>{kbd("Ctrl+Shift+Enter")}start in the background</span><span>{kbd("Esc")}close, keeps your text</span></div>'
    return (f'<!doctype html>\n<html lang="en"><head><meta charset="utf-8"><title>{title}</title><link rel="stylesheet" href="{G}"><link rel="stylesheet" href="agents3340.css"></head>\n'
            f'<body><div class="app">{topbar()}<div class="header"><h1>Agents</h1><span class="head" style="color:var(--ink-subtle)">{9 if starting else 8} agents · 2 need you · 1 PR ready to merge</span></div>'
            f'<div class="body">{page_list_html(starting)}</div>{foot}<div class="scrim"></div>{sheet}</div></body></html>\n')
P = 'Add a retry test for the queue worker'
w('agents3340-f1-form-default', fpage('Sindri · New agent · default', form('')))
w('agents3340-f2-form-filled', fpage('Sindri · New agent · filled', form(P, wt=True)))
w('agents3340-f3-form-starting', fpage('Sindri · New agent · starting', form(P, wt=True, locked=True, start='Starting...', fine='<div class="fl"><div class="mk">Making the worktree</div></div>')))
errfoot = '<div style="padding:0 var(--sp4) var(--sp3)"><div class="ferr-foot"><span class="m">tmux is not installed. Install it with <code>sudo apt install tmux</code></span>' + btn('Retry') + '</div></div>'
w('agents3340-f4-form-error-tmux-missing', fpage('Sindri · New agent · tmux not installed', form(P, foot_extra=errfoot)))
gitfoot = '<div style="padding:0 var(--sp4) var(--sp3)"><div class="ferr-foot"><span class="m">Couldn\'t make the worktree. git said: <code>fatal: a branch named oneoff-261112-1409-add-a-retry already exists</code></span>' + btn('Retry') + '</div></div>'
w('agents3340-f5-form-error-014-worktree', fpage('Sindri · New agent · 014 worktree failed', form(P, wt=True, foot_extra=gitfoot)))
w('agents3340-f6-form-repo-not-onboarded', fpage('Sindri · New agent · repo not onboarded', form(P, repo='<span class="dot"></span>ledgerly-app', focus='repo', repo_note='<div class="note"><span class="mk">not onboarded</span> <span class="lnk">Onboard it</span></div>')))
w('agents3340-f7-form-error-012-folder-gone', fpage('Sindri · New agent · 012 folder gone', form(P, repo='ledgerly-app', focus='repo',
    errs={'repo': 'ledgerly-app: the folder <code>~/work/ledgerly-app</code> is gone'})))
w('agents3340-f8-form-error-013-not-signed-in', fpage('Sindri · New agent · 013 not signed in', form(P, agent='Codex', focus='agent',
    errs={'agent': '<span class="em">Codex isn\'t signed in. Run <code>codex login</code> in a terminal, then Start again.</span>'}).replace('class="ferr"><span class="em">', 'class="ferr cp"><span class="em">')))
w('agents3340-f9-form-error-017-prompt-too-long', fpage('Sindri · New agent · 017 prompt too long', form(('Add a retry test for the queue worker, covering the backoff and the dead-letter path. ' * 3).strip() + ' ...', cnt='8,214', caret=False,
    errs={'prompt': 'The prompt is 8,214 characters. The limit is 8,000.'})))
bg = '<div style="padding:0 var(--sp4) var(--sp3)"><div class="ferr-foot" style="background:var(--s2)"><span class="m">Started claude in sindri.</span><span class="lnk">Show it</span></div></div>'
w('agents3340-f10-form-background-start', fpage('Sindri · New agent · started in the background', form('', foot_extra=bg), starting=True))
FF = f'<div class="keys"><span>{kbd("Tab")}next field</span><span>{kbd("Ctrl+Enter")}start</span><span>{kbd("Esc")}close</span></div>'
def f1000(name, title, sheet):
    w(name, page(title, 1000, phead('ideas', 'Attached', 'ok'), False, LROWS, term(lane_screen(False), False, True), FF, True, extra='<div class="scrim"></div>' + sheet))
f1000('agents3340-f1-form-default-1000', 'Sindri · New agent · default, 1000 wide', form(''))
f1000('agents3340-f2-form-filled-1000', 'Sindri · New agent · filled, 1000 wide', form(P, wt=True))
f1000('agents3340-f8-form-error-013-not-signed-in-1000', 'Sindri · New agent · 013, 1000 wide', form(P, agent='Codex', focus='agent', errs={'agent': '<span class="em">Codex isn\'t signed in. Run <code>codex login</code> in a terminal, then Start again.</span>'}).replace('class="ferr"><span class="em">', 'class="ferr cp"><span class="em">'))
(D/'frames.txt').write_text('\n'.join(OUT)+'\n')
