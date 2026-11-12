#!/usr/bin/env python3
# Generator for the first-run v3 frames (#3236). Run: python3 gen.py && python3 shoot.py setup-connect setup-accounts setup-repos setup-steps stale-line states
# Needs: google-chrome, Pillow not required; states.html is patched from commit 0a816f94bf of pr-assets/first-run-3019 (git -C <repo> show). Writes into first-run-3019/. Pure HTML+CSS, grammar.css only.
import os, re, subprocess
# Output folder: this script's own folder (the asset branch root, or design/first-run-3019 locally); FR_OUT overrides.
OUT = os.environ.get('FR_OUT') or os.path.dirname(os.path.abspath(__file__))

SHEET_CSS = """
body{height:auto;overflow:visible}
.sheet-head{padding:16px var(--inset) 12px;border-bottom:1px solid var(--hair)}
.sheet-head h1{font:600 15px/22px var(--font);margin:0}
.sheet-head p{margin:2px 0 0;color:var(--ink-3);font-size:11px;line-height:16px}
.panel{width:1440px;border-bottom:1px solid var(--hair-strong);display:flex;flex-direction:column;overflow:hidden}
.plabel{height:28px;display:flex;align-items:center;gap:var(--sp3);padding:0 var(--inset);background:var(--s1);border-bottom:1px solid var(--hair);font-size:12px;color:var(--ink-muted)}
.plabel b{font-weight:600;color:var(--ink)} .plabel span{margin-left:auto;font-family:var(--mono);font-size:11px;color:var(--ink-3)}
.pbody{position:relative;overflow:hidden}
.list{padding:var(--sp2) 0}
.pbody .topbar{height:40px}
.pbody .keys{height:28px}
.pbody .header{height:48px}
/* button states: hover lifts the fill, pressed sinks it (darker + inset + 1px down), focus is a 2px ring with a gap */
.btn.hover{background:var(--s4);border-color:var(--hair-3);color:var(--ink)}
.btn.primary.hover{background:var(--primary-hi);border-color:var(--primary-hi);color:#fff}
.btn.pressed{background:var(--s1);border-color:var(--hair-strong);color:var(--ink-subtle);box-shadow:inset 0 2px 3px rgba(0,0,0,.55);transform:translateY(1px)}
.btn.primary.pressed{background:#454fb0;border-color:#454fb0;color:#dfe2ff;box-shadow:inset 0 2px 3px rgba(0,0,0,.55)}
.btn.focus{outline:2px solid var(--primary-hi);outline-offset:2px}
.btn.primary.focus{outline-color:#fff}
.field.focus{box-shadow:0 0 0 2px var(--primary-soft)}
.lnk{cursor:pointer;color:var(--primary-hi);text-decoration:underline;text-underline-offset:2px}
.code{font:600 15px/22px var(--mono);letter-spacing:.08em;color:var(--ink);background:var(--s3);border:1px solid var(--hair-strong);border-radius:var(--r2);padding:0 8px}
.err-t{color:var(--err)}
.guide .step .say b{color:var(--ink);font-weight:500}
.guide .btns2,.actionbar .btns2{display:flex;gap:8px;align-items:center}
.guide{margin-right:var(--inset)}
.det-open{margin:0 var(--inset) var(--sp3) calc(var(--inset) + var(--col-glyph) + var(--sp3));font:400 12px/18px var(--font);color:var(--ink-3)}
.det-open code{display:block;margin-top:4px;font:400 12px/18px var(--mono);color:var(--ink-subtle)}
.pbody .row.fix{--col-act:300px}
.toast{position:absolute;right:24px;bottom:16px;background:var(--s3);border:1px solid var(--hair-strong);border-radius:var(--r3);padding:8px 14px;color:var(--ink);box-shadow:0 8px 24px rgba(0,0,0,.45);display:flex;gap:8px;align-items:center}
.ctx{margin:0;padding:6px var(--inset);font-size:12px;color:var(--ink-3);background:var(--canvas);border-bottom:1px solid var(--hair)}
.ctx b{color:var(--ink-subtle);font-weight:500}
.dpanel{padding:8px 24px;background:var(--s1);border-bottom:1px solid var(--hair);font:400 12px/18px var(--font);color:var(--ink-3)}
.dpanel code{display:block;margin-top:4px;font:400 12px/18px var(--mono);color:var(--ink-subtle)}
.state-line .what .lnk{margin:0}
"""

def page(title, head, sub, panels):
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{title}</title><link rel="stylesheet" href="../grammar-2978/grammar.css">
<style>{SHEET_CSS}</style></head><body>
<div class="sheet-head"><h1>{head}</h1><p>{sub}</p></div>
{''.join(panels)}
</body></html>
"""

def panel(label, tag, body):
    return f'<section class="panel"><div class="plabel"><b>{label}</b><span>{tag}</span></div><div class="pbody">{body}</div></section>\n'

BAR_FIRST = '<div class="topbar"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill">Connected</div></div>'
KEYS = '<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Enter</kbd>press</span><span><kbd>Ctrl+Enter</kbd>primary</span><span><kbd>Ctrl+V</kbd>paste</span><span><kbd>Esc</kbd>{esc}</span></div>'

def btn(label, cls='', key=None, state=''):
    k = f' <kbd>{key}</kbd>' if key else ''
    c = ' '.join(x for x in ['btn', cls, state] if x)
    return f'<span class="{c}">{label}{k}</span>'

def row(glyph, title, sub, fact='', btns='', cls=''):
    return (f'<div class="row fix {cls}"><span class="g {glyph}"></span><div class="title"><span class="t">{title}</span>'
            f'<span class="sub">{sub}</span></div><div class="fact">{fact}</div><div class="fixbtn">{btns}</div></div>')

def step(n, say, right='', now=False):
    return f'<div class="step{" now" if now else ""}"><span class="no">{n}</span><span class="say">{say}</span><span class="btns2">{right}</span></div>'

def guide(*steps): return '<div class="guide">' + ''.join(steps) + '</div>'
def field(text='Paste the token', focus=True, w=280):
    return f'<span class="field{" focus" if focus else ""}" style="width:{w}px">{text} <kbd style="margin-left:auto">Ctrl+V</kbd></span>'
def details(code): return f'<div class="det-open">Details ▾<code>{code}</code></div>'

def firstrun(h1, count, hint, rows, foot_text, foot_btns, esc='nothing here'):
    return (BAR_FIRST + f'<div class="header"><h1>{h1}</h1><span class="count">{count}</span></div>'
            + f'<div class="list" style="--facts:200px"><div class="group"><span class="chev">▾</span>Accounts <span class="n">3</span><span class="right">{hint}</span></div>{rows}</div>'
            + f'<div class="actionbar"><span>{foot_text}</span><span class="spacer"></span>{foot_btns}</div>' + KEYS.replace('{esc}', esc))

GH_SUB = 'pull requests, reviews and CI'
HINT_Q = 'GitHub lets Sindri tick the repos you work on next'
LINEAR = lambda b='Sign in to Linear', cls='': row('todo','Linear','issues and projects','opens your browser', btn(b, cls))
SLACK = lambda b='Paste a Slack token', cls='': row('todo','Slack','messages and mentions · optional','needs a token, 3 steps', btn(b, cls))
SKIP = (btn('Skip','ghost'), 'You can connect these later from Connections.')
CONT = lambda s='': ('<span class="btns2">' + btn('Continue','primary','Enter',s) + '</span>', 'GitHub is connected. Linear and Slack are optional.')
GH_PERMS = 'Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read'

def gh_paste_guide(refused=''):
    return guide(
        step(1, "Open GitHub's new fine-grained token page.", btn('Open in browser'), now=not refused),
        step(2, f'Pick your repos and give it <b>{GH_PERMS}</b>.'),
        step(3, (f'<span class="err-t">{refused}</span>' if refused else 'Paste the token here.'),
             field('Paste the token', True, 260) + btn('Save token'), now=bool(refused)))

def q1_panels():
    P = []
    f1 = lambda rows, foot=SKIP: firstrun('Connect your accounts', 'Question 1 of 2', HINT_Q, rows, foot[1], foot[0])
    P.append(panel('GitHub: gh login found', '§4.2', f1(
        row('todo','GitHub', f'{GH_SUB} · gh login found: ada · uses the gh login\'s scopes', 'gh login found: ada', btn('Use the gh login (ada)','primary','Enter','focus'), 'cursor')
        + LINEAR() + SLACK())))
    P.append(panel('GitHub: no gh login found', '§4.2', f1(
        row('todo','GitHub', GH_SUB, 'opens github.com/login/device', btn('Sign in to GitHub','primary','Enter','focus'), 'cursor')
        + LINEAR() + SLACK())))
    P.append(panel('GitHub: device code shown', '§4.2 · device flow', f1(
        row('agent','GitHub', GH_SUB, 'waiting for GitHub', btn('Waiting for GitHub…','','','busy'), 'cursor')
        + guide(step(1, 'Enter <span class="code">ABCD-1234</span> at <b>github.com/login/device</b>.', btn('Copy code and open GitHub','primary','Enter','focus'), now=True),
                step(2, 'Waiting for GitHub… the code works for 14:32 more.', btn('Cancel')))
        + LINEAR() + SLACK())))
    P.append(panel('GitHub: device code expired', '§4.2 · device flow', f1(
        row('fail','GitHub', GH_SUB, '<span class="err-t">code expired</span>', '', 'cursor')
        + guide(step(1, '<span class="err-t">The code expired before GitHub saw it.</span>', btn('Get a new code','primary','Enter','focus') + btn('Cancel'), now=True))
        + LINEAR() + SLACK())))
    P.append(panel('GitHub: token paste open', '§4.2 · decision 17', f1(
        row('todo','GitHub', GH_SUB, 'needs a token, 3 steps', btn('Paste a GitHub token','primary','','pressed'), 'cursor')
        + gh_paste_guide() + LINEAR() + SLACK())))
    P.append(panel('GitHub: token refused', '§4.2', f1(
        row('todo','GitHub', GH_SUB, 'needs a token, 3 steps', btn('Paste a GitHub token','primary'), 'cursor')
        + gh_paste_guide('GitHub refused this token: it has no access to Pull requests. Paste a new one.')
        + LINEAR() + SLACK())))
    P.append(panel('SND-SETUP-013: your org blocks the app', '§6', f1(
        row('fail','GitHub', "Your org blocks Sindri's app. Paste a token instead.", '<span class="err-t">sign-in refused</span>', btn('Paste a GitHub token','primary','','pressed'), 'cursor')
        + gh_paste_guide() + details('SND-SETUP-013 · sindri setup connect github --token-env NAME')
        + LINEAR() + SLACK())))
    P.append(panel('SND-SETUP-014: this build has no app id', '§6', f1(
        row('fail','GitHub', 'This build has no GitHub app id. Paste a token instead.', '<span class="err-t">no app id</span>', btn('Paste a GitHub token','primary','','pressed'), 'cursor')
        + gh_paste_guide() + details('SND-SETUP-014 · sindri setup connect github --token-env NAME')
        + LINEAR() + SLACK())))
    P.append(panel('Linear: token paste open (GitHub connected, so Continue is the primary)', '§4.2', f1(
        row('linked','GitHub', GH_SUB, 'connected as ada', '')
        + row('todo','Linear','issues and projects','needs a key, 3 steps', btn('Paste a Linear token','','','pressed'),'cursor')
        + guide(step(1, 'Open Linear, Settings, API, Personal API keys.', btn('Open in browser'), now=True),
                step(2, 'Create a key named <b>Sindri</b>.'),
                step(3, 'Paste the key here.', field('Paste the key', True, 260) + btn('Save key')))
        + SLACK(), CONT())))
    P.append(panel('Slack: token paste open (never the primary)', '§4.2', f1(
        row('linked','GitHub', GH_SUB, 'connected as ada', '')
        + LINEAR()
        + row('todo','Slack','messages and mentions · optional','needs a token, 3 steps', btn('Paste a Slack token','','','pressed'),'cursor')
        + guide(step(1, "Open Slack's app page and create the app from Sindri's manifest.", btn('Open in browser'), now=True),
                step(2, 'Install the app to your workspace.'),
                step(3, 'Copy the user token Slack shows you, then paste it here.', field('Paste the token', True, 240) + btn('Save token'))), CONT())))
    P.append(panel('Other row states, each its own state: signing in, waiting on admin, sign-in failed, store failed, Slack token refused', '§4.2', f1(
        row('agent','Linear','Waiting for your browser…','signing in', btn('Cancel'))
        + row('parked','Linear','Waiting on your workspace admin to approve Sindri.','waiting on admin', btn('Check again'))
        + row('fail','GitHub','GitHub refused the sign-in: access_denied.','<span class="err-t">sign-in failed</span>', btn('Retry'), 'cursor')
        + row('fail','Slack',"Couldn't store the token: the keychain is locked. Nothing was saved.",'<span class="err-t">not saved</span>', btn('Retry'))
        + row('fail','Slack','Slack refused this token: invalid_auth. Paste a new one.','<span class="err-t">token refused</span>', btn('Paste a Slack token')))))
    P.append(panel('GitHub connected: Continue is the primary and focused', '§4.2 · decision 15', f1(
        row('linked','GitHub', GH_SUB, 'connected as ada', '')
        + LINEAR() + SLACK(), CONT('focus'))))
    cells = ''
    for name, s in [('rest',''),('hover','hover'),('pressed','pressed'),('focus (ring)','focus')]:
        cells += (f'<div style="display:flex;flex-direction:column;gap:14px;align-items:flex-start;width:260px"><span style="font-size:11px;color:var(--ink-3)">{name}</span>'
                  f'{btn("Sign in to GitHub","primary","Enter",s)}{btn("Sign in to Linear","","",s)}</div>')
    P.append(panel('Button states', 'hover lifts · pressed sinks · focus draws a ring', f'<div style="display:flex;padding:20px 40px 32px;gap:20px">{cells}</div>'))
    return P

NAV_SETUP = '<nav class="nav"><a href="#">Home</a><a href="#">Reviews</a><a href="#">Items</a><a href="#">Workflows</a><a href="#">Agents</a><a href="#">Projects</a><a href="#">Docs</a><a class="on" href="#">Setup</a></nav>'
def accounts_page(rows, foot_btn, last):
    return ('<div class="topbar"><div class="wordmark">Sindri</div>' + NAV_SETUP + '<div class="spacer"></div><div class="status-pill">Connected</div></div>'
            + '<div class="header"><h1>Accounts</h1><span class="count">Connect or reconnect the accounts Sindri reads</span></div>'
            + f'<div class="list" style="--facts:200px"><div class="group"><span class="chev">▾</span>Accounts <span class="n">3</span></div>{rows}</div>'
            + f'<div class="actionbar"><span>{last}</span><span class="spacer"></span>{foot_btn}</div>' + KEYS.replace('{esc}', 'Done'))

def accounts_panels():
    P = []
    GH_OK = row('linked','GitHub', GH_SUB, 'connected as ada', '')
    LIN_OK = row('linked','Linear','issues and projects','connected as ada','')
    P.append(panel('Way in 1: from a Needs you row "Your Linear sign-in expired"', 'decision 28',
        '<div class="ctx"><b>Came from</b> Home › Needs you › Your Linear sign-in expired · <a class="lnk" href="#">Reconnect Linear</a></div>'
        + accounts_page(GH_OK + row('parked','Linear','Your Linear sign-in expired.','expired 3h ago', btn('Reconnect Linear','primary','Enter','focus'),'cursor') + SLACK(),
                        btn('Done','','Esc'), 'Done goes back to Home.')))
    P.append(panel('Way in 1, after Linear connects: Done is the primary and focused', 'decision 28',
        accounts_page(GH_OK + LIN_OK + SLACK(), btn('Done','primary','Esc','focus'), 'Done goes back to Home.')))
    P.append(panel('Way in 2: from the Setup page (Accounts button)', 'decision 28',
        '<div class="ctx"><b>Came from</b> Setup › Connect your accounts › <a class="lnk" href="#">Accounts</a></div>'
        + accounts_page(GH_OK + row('todo','Linear','issues and projects','opens your browser', btn('Sign in to Linear','primary','Enter','focus'),'cursor') + SLACK(),
                        btn('Done','','Esc'), 'Done goes back to Setup.')))
    P.append(panel('Done pressed', 'decision 28',
        accounts_page(GH_OK + LIN_OK + SLACK(), btn('Done','primary','Esc','pressed'), 'Done goes back to Setup.')))
    nav = '<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav"><a href="#">Home<span class=n>9</span></a><a href="#">Reviews<span class=n>4</span></a><a href="#">Items<span class=n>31</span></a><a href="#">Workflows</a><a href="#">Agents<span class=n>3</span></a><a href="#">Projects</a><a href="#">Docs</a><a class=on href="#">Setup<span class=n>6/8</span></a></nav><div class="spacer"></div><div class="status-pill">Connected</div></div>'
    body = (nav + '<div class="header"><h1>Setup</h1><span class="count">6 of 8 steps done</span></div>'
        + '<div class="list" style="--facts:136px;--col-act:200px"><div class="group"><span class="chev">▾</span>Steps <span class="n">8</span></div>'
        + row('done','Connect your accounts','GitHub is connected as ada · Linear is connected as ada · Slack is optional and not connected','', btn('Accounts'), 'read cursor')
        + row('done','Add your repos','jthorare/agentic-workflow and ledgerly-app/exports','2 repos','', 'read')
        + row('done','Profile saved','every setting read, none wrong','','', 'read')
        + '</div>' + '<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Enter</kbd>press the primary</span><span><kbd>.</kbd>actions</span></div>')
    P.append(panel('Back on the Setup page: the accounts row is done', 'decision 28', body))
    return P

def steps_page():
    nav = '<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav"><a href="#">Home<span class=n>9</span></a><a href="#">Reviews<span class=n>4</span></a><a href="#">Items<span class=n>31</span></a><a href="#">Workflows</a><a href="#">Agents<span class=n>3</span></a><a href="#">Projects</a><a href="#">Docs</a><a class=on href="#">Setup<span class=n>6/8</span></a></nav><div class="spacer"></div><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><div class="status-pill">Connected</div></div>'
    R = [
     row('done','Sindri is current','running 7c81646, the installed one','','','read'),
     row('done','Connect your accounts','GitHub is connected as ada · Linear and Slack are optional and not connected','', btn('Accounts'),'read'),
     row('done','Add your repos','jthorare/agentic-workflow and ledgerly-app/exports','2 repos','','read'),
     row('done','Profile saved','every setting read, none wrong','','','read'),
     row('done','Agent for workflows: claude-code','found installed; workflows launch their agent steps with it','','','read'),
     row('done',"Agents can use Sindri's tools",'registered in agentic-workflow and exports','','','read'),
     row('agent','Build the map','Building the map: 4,720 of 15,223 files in agentic-workflow','<span class="bar"><i style="width:31%"></i></span>31%','','unread cursor'),
     row('todo','Run hello-sindri','a first workflow that needs no account and no network', '', btn('Run hello-sindri','primary','Enter') + btn('Dismiss','ghost'),'unread'),
    ]
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Sindri · Setup</title><link rel="stylesheet" href="../grammar-2978/grammar.css">
<style>.list .row .fixbtn .btn{{height:24px}}</style></head><body>
<div class="app">{nav}
<div class="header"><h1>Setup</h1><span class="count">6 of 8 steps done</span><div class="filter">Filter<kbd style="margin-left:auto">/</kbd></div></div>
<div class="body"><div class="list" style="--facts:136px;--col-act:260px">
<div class="group"><span class="chev">▾</span>Steps <span class="n">8</span><span class="right">the same steps as <b style="font-weight:500">sindri setup</b>, in the same order</span></div>
{''.join(R)}
</div></div>
<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Enter</kbd>press the primary</span><span><kbd>.</kbd>actions</span><span><kbd>Space</kbd>peek</span><span><kbd>/</kbd>filter</span><span><kbd>Ctrl+K</kbd>palette</span></div>
</div></body></html>
"""

def navbar(pill='Connected'):
    return ('<div class="topbar" style="height:40px"><div class="wordmark">Sindri</div><nav class="nav"><a class=on href="#">Home</a><a href="#">Reviews</a><a href="#">Items</a><a href="#">Workflows</a><a href="#">Agents</a><a href="#">Projects</a><a href="#">Docs</a></nav><div class="spacer"></div>'
            f'<div class="status-pill{"" if pill=="Connected" else " off"}">{pill}</div></div>')
HOME = ('<div class="header" style="height:40px"><h1>Home</h1><span class=count>9</span></div><div class="list" style="--facts:176px;padding:0">'
        '<div class="row unread"><span class="g pr"></span><div class="title"><span class="id">#4182</span><span class="t">Paginate workspace audit export</span></div><div class="fact mono">ledgerly…/exports</div><div class="age">38m</div><div class="wait">review</div></div>'
        '<div class="row unread"><span class="g fail"></span><div class="title"><span class="id">r-0412</span><span class="t">Nightly usage rollup failed</span></div><div class="fact mono">wf/usage-rollup</div><div class="age">2h</div><div class="wait">retry</div></div></div>')
def sline(glyph, what, btns, cls='att', det=True, open_=False):
    d = ('<span class="det">Details ' + ('▾' if open_ else '▸') + '</span>') if det else ''
    return f'<div class="state-line {cls}"><span class="g {glyph}"></span><span class="what">{what}</span><span class="btns">{d}{btns}</span></div>'
L1 = '<a class="lnk" href="#">Nightly gate</a> (run 14, an agent step)'
L2 = '<a class="lnk" href="#">Review loop on #3120</a> (run 3, waiting on your approval)'
L3 = '<a class="lnk" href="#">Code index of agentic-workflow</a> (a job)'
STALE = 'A newer Sindri is installed (7c81646, built today 10:31).'

def stale_panels():
    P = []
    rs = btn('Restart Sindri','primary','Ctrl+Shift+R')
    P.append(panel('Stale', '§4.5', navbar()+sline('parked', STALE, rs)+HOME))
    P.append(panel('Stale, Details open', '§4.5', navbar()+sline('parked', STALE, rs, open_=True)
        + '<div class="dpanel">running 80f69df, built yesterday 22:10 · installed 7c81646, built today 10:31<code>sindri stop &amp;&amp; sindri</code></div>'+HOME))
    P.append(panel('Runs open', '§4.5 · decision 11', navbar()+sline('parked', f'Sindri restarts when 3 finish: {L1}, {L2}, {L3}.', btn('Restart when they finish','primary'), det=False)+HOME))
    P.append(panel('Waiting for runs', '§4.5', navbar()+sline('agent', f'Restarts when 3 finish. Keep this window open. {L1}, {L2}, {L3}.', btn('Cancel'), cls='', det=False)+HOME))
    P.append(panel('A new run started during the wait', '§4.5', navbar()+sline('agent', f'A new run started: <a class="lnk" href="#">Weekday check</a> (run 7). Sindri restarts when it finishes too.', btn('Cancel'), cls='', det=False)+HOME))
    P.append(panel('Cancelled: back to Stale', '§4.5', navbar()+sline('parked', STALE, rs)+HOME))
    P.append(panel("Still stopping (the old serve's pid is alive after 30 s)", '§4.5',
        navbar('Stopping')+sline('parked','Sindri is still stopping.', btn('Start Sindri','primary','Enter'), open_=True)
        + '<div class="dpanel">The old Sindri (pid 48213) has not exited. Start Sindri starts nothing while that pid lives.<code>log: ~/.agentic-workflow/sindri-app/logs/serve.log</code></div>'+HOME))
    P.append(panel('Restarting: pages keep their last read, writes are off', '§4.5', navbar('Restarting')+sline('agent','Restarting Sindri…', '', cls='', det=False)+HOME))
    P.append(panel('Restarted: the line goes, one toast', '§4.5', navbar()+HOME+'<div style="height:64px"></div><div class="toast"><span class="g done"></span>Sindri restarted on 7c81646.</div>'))
    P.append(panel("Didn't come back in 30 s", '§4.5 · SND-SETUP-012', navbar('Not running')+sline('fail', "Sindri stopped but didn't start again: port 7410 is already in use.", btn('Start Sindri','primary','Enter'), cls='err')+HOME))
    return P


# ---------------- question 2: repos ----------------
def rrow(path, slug, wt='', known=False, on=False, cls='', extra=''):
    return (f'<div class="row {cls}"><span class="g box{" on" if on else ""}"></span><div class="title"><span class="t">{path}</span>{extra}</div>'
            f'<div class="fact mono">{slug}</div><div class="fact">{wt}</div><div class="fact">{"known" if known else ""}</div></div>')
R_AW  = lambda **k: rrow('~/repos/agentic-workflow','jthorare/agentic-workflow','worktrees: 104',**k)
R_EX  = lambda **k: rrow('~/repos/exports','ledgerly-app/exports','worktrees: 3',**k)
R_CORE= lambda **k: rrow('~/work/ledgerly-core','ledgerly-app/core',**k)
R_DOT = lambda **k: rrow('~/repos/dotfiles','ada/dotfiles',**k)
R_PM  = lambda **k: rrow('~/repos/prism-mcp-fork','ada/prism-mcp','worktrees: 1',**k)
R_OLD = lambda **k: rrow('~/src/old-site','<span style="color:var(--ink-3)">no remote</span>',**k)
SK = '<div class="row skel"><span class="g box"></span><div class="title"><span class="t"></span></div></div><div class="row skel"><span class="g box"></span><div class="title"><span class="t" style="width:150px"></span></div></div>'
KEYS_R = '<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>x</kbd>tick</span><span><kbd>a</kbd>tick all</span><span><kbd>Space</kbd>peek path</span><span><kbd>/</kbd>filter</span><span><kbd>Ctrl+Enter</kbd>add</span><span><kbd>Esc</kbd>back to question 1</span></div>'
def rframe(status, group, rows, foot, btns, sub='Question 2 of 2 · this is the last question; Home opens next'):
    return (BAR_FIRST + f'<div class="header"><h1>Where are your repos?</h1><span class="count">{sub}</span></div>'
            + f'<div class="list" style="--facts:248px 120px 72px">{status}{group}{rows}</div>'
            + f'<div class="actionbar"><span>{foot}</span><span class="spacer"></span>{btns}</div>' + KEYS_R)
GRP = lambda n, r='repos you worked on in GitHub in the last 30 days are ticked': f'<div class="group" style="margin-top:var(--sp2)"><span class="chev">▾</span>Git repos found <span class="n">{n}</span><span class="right">{r}</span></div>'
SL = lambda g, what, sub='', b='', cls='': f'<div class="state-line {cls}"><span class="g {g}"></span><span class="what">{what}<span class="sub">{sub}</span></span><span class="btns">{b}</span></div>'
ADD2 = btn('Add 2 repos','primary','Ctrl+Enter')
LOOK = lambda c='ghost': btn('Look in another folder…', c)
FOOT2 = '2 of 6 ticked · worktrees fold into their main checkout'
def repos_panels():
    P=[]
    six = lambda **k: R_AW(known=True,on=True,**k)+R_EX(known=True,on=True)+R_CORE(cls='cursor')+R_DOT()+R_PM()+R_OLD()
    P.append(panel('Scanning', '§4.3', rframe(SL('agent','Scanning ~ · 3 levels · 41 folders','6 repos so far',btn('Stop scan','ghost')), GRP('6 · 2 ticked'), six()+SK, FOOT2, LOOK()+ADD2)))
    P.append(panel('Done, repos found', '§4.3', rframe(SL('done','6 repos under ~, 3 levels deep'), GRP('6 · 2 ticked'), six(), FOOT2, LOOK()+ADD2)))
    P.append(panel('Done, none found', '§4.3', rframe(SL('todo','No git repos under ~, 3 levels deep.'), '', '', '0 of 0 ticked', LOOK('primary'))))
    P.append(panel('Scan failed', '§4.3 · SND-SETUP-008', rframe(SL('fail',"Couldn't read ~: permission denied.",'',btn('Retry','primary','Enter'),'err'), '', '', '0 of 0 ticked', LOOK())))
    ro = lambda **k: R_AW(known=True,on=True,cls='read',**k)+R_EX(known=True,on=True,cls='read')+R_CORE(cls='read')+R_DOT(cls='read')+R_PM(cls='read')+R_OLD(cls='read')
    P.append(panel('Adding: rows read-only, no primary', '§4.3', rframe(SL('agent','Adding 2 repos…'), GRP('6 · 2 ticked'), ro(), FOOT2, LOOK('ghost busy')+btn('Adding…','','','busy'))))
    P.append(panel('Add failed for one repo: the others are added and leave the list', '§4.3 · SND-SETUP-001',
        rframe(SL('fail','1 of 2 repos added. ~/repos/exports could not be added.','',' ','err').replace('<span class="btns"> </span>','<span class="btns"></span>'), GRP('5 left'),
               '<div class="row fix" style="--facts:248px 120px 72px;--col-act:168px"><span class="g fail"></span><div class="title"><span class="t">~/repos/exports</span><span class="sub err-t">That folder isn\'t the top of a git repo.</span></div><div class="fact mono">ledgerly-app/exports</div><div class="fact">worktrees: 3</div><div class="fact"></div><div class="fixbtn">'+btn('Retry','primary','Enter')+'</div></div>'
               + R_CORE()+R_DOT()+R_PM()+R_OLD(), '0 of 5 ticked', LOOK())))
    nk = lambda **k: rrow('~/repos/agentic-workflow','jthorare/agentic-workflow','worktrees: 104',**k)+rrow('~/repos/exports','ledgerly-app/exports','worktrees: 3',**k)
    P.append(panel('No account connected: nothing pre-ticked', '§4.3', rframe(SL('done','6 repos under ~, 3 levels deep'), GRP('6 · 0 ticked','Tick the repos you want Sindri to watch'),
        nk(cls='cursor' if False else '')+R_CORE()+R_DOT()+R_PM()+R_OLD(), '0 of 6 ticked · worktrees fold into their main checkout', LOOK()+btn('Add 0 repos','primary','Ctrl+Enter','busy'), sub='Question 2 of 2 · Tick the repos you want Sindri to watch')))
    return P

# ---------------- setup page ----------------
def snav(tab='Setup', n='6/8'):
    items = ['Home<span class=n>9</span>','Reviews<span class=n>4</span>','Items<span class=n>31</span>','Workflows','Agents<span class=n>3</span>','Projects','Docs']
    out = ''.join(f'<a href="#">{i}</a>' for i in items)
    if tab == 'Setup': out += f'<a class=on href="#">Setup<span class=n>{n}</span></a>'
    else: out += '<a class=on href="#">Settings</a>'
    return f'<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav">{out}</nav><div class="spacer"></div><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><div class="status-pill">Connected</div></div>'
def sbody(title, count, rows, nav=None):
    return (nav or snav()) + f'<div class="header"><h1>{title}</h1><span class="count">{count}</span></div><div class="list" style="--facts:136px;--col-act:260px"><div class="group"><span class="chev">▾</span>Steps <span class="n">8</span><span class="right">the same steps as <b style="font-weight:500">sindri setup</b>, in the same order</span></div>{rows}</div>' + '<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Enter</kbd>press the primary</span><span><kbd>.</kbd>actions</span></div>'
def sd(t, sub, fact='', b='', cls='read'): return row('done', t, sub, fact, b, cls)
def steps_panels():
    P=[]
    cur = sd('Sindri is current','running 7c81646, the installed one')
    acc = sd('Connect your accounts','GitHub is connected as ada · Linear and Slack are optional and not connected','',btn('Accounts'))
    rep = sd('Add your repos','jthorare/agentic-workflow and ledgerly-app/exports','2 repos')
    prof= sd('Profile saved','every setting read, none wrong')
    ag  = sd('Agent for workflows: claude-code','found installed; workflows launch their agent steps with it')
    mcp = sd("Agents can use Sindri's tools",'registered in agentic-workflow and exports')
    mapd= sd('Build the map','a current map of agentic-workflow and exports')
    maprun = row('agent','Build the map','Indexing files: 7,000 of 15,223 in agentic-workflow','<span class="bar"><i style="width:46%"></i></span>46%','','unread')
    maprun2 = row('agent','Build the map','Building the map: 4,720 of 15,223 files in agentic-workflow','<span class="bar"><i style="width:31%"></i></span>31%','','unread')
    hello = row('todo','Run hello-sindri','a first workflow that needs no account and no network','', btn('Run hello-sindri','primary','Enter')+btn('Dismiss','ghost'),'unread')
    P.append(panel('Map, phase 1: indexing files', '§4.4 · decision 18', sbody('Setup','6 of 8 steps done', cur+acc+rep+prof+ag+mcp+maprun.replace('unread','unread cursor')+hello, snav())))
    P.append(panel('Map, phase 2: building the map', '§4.4 · decision 18', sbody('Setup','6 of 8 steps done', cur+acc+rep+prof+ag+mcp+maprun2.replace('unread','unread cursor')+hello)))
    noag = row('you','Agent for workflows','No coding agent found. Install Claude Code, Codex or Cursor, then check again.','', btn('Check again','primary','Enter'),'unread cursor')
    hello2 = row('todo','Run hello-sindri','a first workflow that needs no account and no network','', btn('Run hello-sindri','ghost'),'unread')
    P.append(panel('Agent: no agent installed', '§4.4', sbody('Setup','5 of 8 steps done', cur+acc+rep+prof+noag+mcp+maprun2+hello2, snav('Setup','5/8'))))
    run = row('agent','Run hello-sindri','hello-sindri is running: step 2 of 4.','','','unread cursor')
    P.append(panel('First workflow: running', '§4.4', sbody('Setup','7 of 8 steps done', cur+acc+rep+prof+ag+mcp+mapd+run, snav('Setup','7/8'))))
    fail = row('fail','Run hello-sindri','hello-sindri failed at Make a greeting: the step ran out of time after 60 s.','<span class="err-t">failed 1m ago</span>', btn('Open the run','primary','Enter'),'unread cursor')
    P.append(panel('First workflow: the run failed', '§4.4', sbody('Setup','7 of 8 steps done', cur+acc+rep+prof+ag+mcp+mapd+fail, snav('Setup','7/8'))))
    wait = lambda t,sub: row('todo', t, 'Waits for Sindri to run', '', '', 'read')
    down = (row('fail',"Sindri isn't running",'Nothing answered on 127.0.0.1:7410.','<span class="err-t">not running</span>', btn('Start Sindri','primary','Enter'),'unread cursor')
            + wait('Connect your accounts','') + rep + prof + ag + wait("Agents can use Sindri's tools",'') + wait('Build the map','') + wait('Run hello-sindri',''))
    dn = snav('Setup','3/8').replace('status-pill">Connected','status-pill off">Not running')
    P.append(panel('Serve down: steps that need Sindri wait', '§4.4', sbody('Setup','3 of 8 steps done', down, dn)))
    alld = cur+acc+rep+prof+ag+mcp+mapd+sd('Run hello-sindri','hello-sindri ran once and succeeded')
    P.append(panel('All done: the Setup tab leaves the nav, Settings holds the list', '§4.4', sbody('Settings','Setup · 8 of 8 steps done', alld, snav('Settings'))))
    return P

def nr_half(pill, glyph, h2, p, acts, det, cap, off=True):
    nav='<nav class="nav"><a class=on href="#">Home</a><a href="#">Reviews</a><a href="#">Items</a><a href="#">Workflows</a><a href="#">Agents</a><a href="#">Projects</a><a href="#">Docs</a></nav>'
    return (f'<div class="half"><div class="topbar"><div class="wordmark">Sindri</div>{nav}<div class="spacer"></div><div class="status-pill off">{pill}</div></div>'
            f'<div class="header"><h1>Home</h1></div><div class="body"><div class="list"><div class="empty"><div class="eg"><span class="g {glyph}"></span></div><h2>{h2}</h2><p>{p}</p>'
            f'<div class="acts">{acts}</div><details class="details" open><summary>Details</summary><code>{det}</code></details></div></div></div>'
            f'<div class="keys"><span class="cap" style="margin-left:0">{cap}</span></div></div>')
def not_running_page():
    css = """.frames{display:grid;grid-template-columns:720px 720px;width:1440px}
.half{height:420px;display:grid;grid-template-rows:var(--bar-h) var(--header-h) 1fr var(--keys-h);border-bottom:1px solid var(--hair-strong)}
.half:nth-child(odd){border-right:1px solid var(--hair-strong)}
.half .nav a{color:var(--ink-3)} .half .nav a.on{color:var(--ink-subtle)}
.half .list{overflow:hidden}"""
    H = [
     nr_half('Not running','parked',"Sindri isn't running","Start it and Home loads your reviews, runs and messages. This takes about 10 seconds.", btn('Start Sindri','primary','Enter','focus'),'Nothing answered on 127.0.0.1:7410. To start it yourself, run: sindri','1 · Not running'),
     nr_half('Starting','agent','Starting Sindri…','Sindri is starting on this machine. Home loads by itself when it answers.', btn('Starting…','primary','','busy'),'Waiting for 127.0.0.1:7410 · 4.2 s','2 · Starting'),
     nr_half('Not running','fail',"Sindri didn't start","Sindri didn't start: port 7410 is already in use.", btn('Retry','primary','Enter','focus'),'log: ~/.agentic-workflow/sindri-app/logs/serve.log<br>To start it yourself, run: sindri serve',"3 · Didn't start in 30 s"),
     nr_half('Starting','agent','Sindri is already starting on this machine.','Home loads by itself when it answers. This page keeps checking.','','Waiting for 127.0.0.1:7410 · 11.8 s','4 · A second serve was refused'),
    ]
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Sindri · Not running</title><link rel="stylesheet" href="../grammar-2978/grammar.css">
<style>{SHEET_CSS}{css}</style></head><body><div class="frames">{''.join(H)}</div></body></html>
"""

def patch_states():
    p = os.path.join(OUT, 'states.html')
    s = open(p).read()
    s = s.replace('(build 1112 → 1118)', '(7c81646, built today 10:31)').replace('1,204 / 3,880 files','4,720 / 15,223 files')
    s = re.sub(r'<section class="panel"><div class="plabel"><b>No profile</b>.*?</section>',
        '<section class="panel"><div class="plabel"><b>Home: no profile</b><span>#3054 · #3113</span></div><div class="pbody"><div class="topbar" style="height:40px"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill off">Not set up</div></div><div class="header" style="height:40px"><h1>Home</h1></div>'
        '<div class="state-line att"><span class="g you"></span><span class="what">Finish setting up Sindri: connect your accounts.</span><span class="btns"><span class="btn primary">Connect your accounts <kbd>Enter</kbd></span></span></div></div></section>', s, flags=re.S)
    s = re.sub(r'<section class="panel"><div class="plabel"><b>Profile with no repo</b>.*?</section>',
        '<section class="panel"><div class="plabel"><b>Home: a profile with no repo</b><span>#3054</span></div><div class="pbody"><div class="topbar" style="height:40px"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill">Connected</div></div><div class="header" style="height:40px"><h1>Home</h1></div>'
        '<div class="state-line att"><span class="g you"></span><span class="what">Finish setting up Sindri: add your repos.</span><span class="btns"><span class="btn primary">Find my repos <kbd>Enter</kbd></span></span></div></div></section>', s, flags=re.S)
    hn = lambda inner: ('<div class="pbody"><div class="topbar" style="height:40px"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill">Connected</div></div><div class="header" style="height:40px"><h1>Home</h1></div>' + inner + '</div>')
    emp = lambda g, h, pp, acts: f'<div class="empty" style="padding-top:var(--sp4)"><div class="eg"><span class="g {g}"></span></div><h2>{h}</h2><p>{pp}</p><div class="acts">{acts}</div></div>'
    hs = lambda lab, tag, inner: f'<section class="panel"><div class="plabel"><b>{lab}</b><span>{tag}</span></div>{hn(inner)}</section>\n'
    new = ''
    new += hs('Home: hello-sindri being made', '#3019 · decision 19', emp('agent','Getting a first workflow ready…','Sindri is making hello-sindri in agentic-workflow. This takes a few seconds.',''))
    new += hs('Home: hello-sindri not run (no own rows yet)', '#3019 · decision 19', emp('todo','Run a first workflow','hello-sindri needs no account and runs in a few seconds.','<span class="btn primary">Run hello-sindri <kbd>Enter</kbd></span>'))
    new += hs('Home: hello-sindri running', '#3019', emp('agent','hello-sindri is running: step 2 of 4.','It makes an item in agentic-workflow when it finishes.',''))
    new += hs('Home: hello-sindri failed', '#3019', emp('fail','hello-sindri failed at Make a greeting: the step ran out of time after 60 s.','Open the run to see its steps.','<span class="btn primary">Open the run <kbd>Enter</kbd></span>'))
    new += hs('Home: own rows present, hello-sindri as a secondary line', '#3019 · decision 22',
        '<div class="list" style="--facts:176px;padding:0">'
        '<div class="row unread cursor"><span class="g pr"></span><div class="title"><span class="id">#4182</span><span class="t">Paginate workspace audit export</span></div><div class="fact mono">ledgerly…/exports</div><div class="age">38m</div><div class="wait">review</div></div>'
        '<div class="row unread"><span class="g fail"></span><div class="title"><span class="id">r-0412</span><span class="t">Nightly usage rollup failed</span></div><div class="fact mono">wf/usage-rollup</div><div class="age">2h</div><div class="wait">retry</div></div>'
        '<div class="row fix read" style="--col-act:236px"><span class="g todo"></span><div class="title"><span class="t">Run hello-sindri</span></div><div class="fact mono">wf/hello-sindri</div><div class="fixbtn"><span class="btn ghost">Run hello-sindri</span><span class="btn ghost">Dismiss</span></div></div></div>')
    s = s.replace('</div></body></html>', new + '</div></body></html>')
    s = s.replace('--facts:104px','--facts:176px')
    open(p, 'w').write(s)

def patch_repos():
    p = os.path.join(OUT, 'setup-repos.html'); s = open(p).read()
    s = s.replace('Question 1 of 2', 'Question 2 of 2 · this is the last question; Home opens next')
    s = s.replace('jthorare/dotfiles','ada/dotfiles').replace('jthorare/prism-mcp','ada/prism-mcp')
    s = s.replace('<kbd>Esc</kbd>back','<kbd>Esc</kbd>back to question 1')
    open(p, 'w').write(s)

def write(name, txt):
    open(os.path.join(OUT, name), 'w').write(txt)

if __name__ == '__main__':
    for f in ('states.html',):
        data = subprocess.run(['git','-C','/home/thor/repos/agentic-workflow','show',f'0a816f94bf:{f}'], capture_output=True, text=True, check=True).stdout
        write(f, data)
    write('setup-connect.html', page('Sindri · Question 1: connect your accounts', 'Question 1 of 2: connect your accounts',
        "Every state of the accounts question at the fixture numbers. One primary per state. Hover lifts a button, pressed sinks it, focus draws a ring.", q1_panels()))
    write('setup-accounts.html', page('Sindri · Accounts page', 'Accounts: question 1 opened from elsewhere',
        'No "Question 1 of 2", the footer holds Done, and Esc presses Done. Two ways in, Done pressed, and the page it returns to.', accounts_panels()))
    write('stale-line.html', page('Sindri · Stale serve line', 'The stale serve line',
        'One 32 px line under the top bar. A build is written as its commit and its built time.', stale_panels()))
    write('setup-steps.html', page('Sindri · Setup', 'The Setup page', 'One RowList of the eight steps, in the order of sindri setup. The first row not done holds the one primary.', steps_panels()))
    write('setup-repos.html', page('Sindri · Question 2: where are your repos', 'Question 2 of 2: where are your repos', 'Every state of the repos question at the fixture numbers. Scan from ~, depth 3. One primary per state.', repos_panels()))
    write('not-running.html', not_running_page())
    patch_states()
