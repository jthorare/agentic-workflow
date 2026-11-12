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

# ---------------------------------------------------------------- v3 additions (meta's UX pass, #3236)
SHEET_CSS += """
.sheet{display:flex;flex-wrap:wrap;width:1440px}
.panel.w1440{width:1440px} .panel.w800{width:800px} .panel.w720{width:720px;border-right:1px solid var(--hair-strong)}
.panel .pbody{flex:1}
.fcol{width:640px;margin:0 auto;padding:36px 0 24px}
.fcol .eyebrow2{font:500 12px/16px var(--font);color:var(--ink-subtle);margin:0 0 6px}
.fcol h1{font:600 24px/32px var(--font);margin:0 0 6px;color:var(--ink)}
.fcol .lede{font:400 14px/22px var(--font);color:var(--ink-subtle);margin:0 0 20px}
.fcol .ghline{display:flex;align-items:center;gap:8px;font:400 13px/20px var(--font);color:var(--ink-subtle);margin:0 0 6px}
.fcol .ghline .lnk{margin:0}
.fcol .btn{height:32px;padding:0 14px;font-size:13px;line-height:30px}
.fcol .btn kbd{line-height:16px}
.crow{display:grid;grid-template-columns:20px 1fr auto;column-gap:12px;align-items:center;padding:10px 16px;background:var(--s1);border:1px solid var(--hair);border-radius:var(--r3);margin-bottom:8px;min-height:56px}
.crow.cursor{box-shadow:0 0 0 1px var(--primary-hi);border-color:var(--primary-hi)}
.crow.dim{opacity:.55}
.ct{font:500 14px/20px var(--font);color:var(--ink)}
.cs{font:400 13px/20px var(--font);color:var(--ink-subtle)}
.cs.mono{font-family:var(--mono);font-size:12px}
.cs .err-t{color:var(--err)}
.cbtn{display:flex;gap:8px;align-items:center}
.cchips{display:flex;gap:6px;align-items:center;color:var(--ink-subtle);font-size:12px}
.fcol .guide{margin:-4px 0 8px 32px;padding:8px 14px}
.fcol .guide .step{grid-template-columns:20px 1fr;row-gap:6px;padding:6px 0}
.fcol .guide .step .btns2{grid-column:2}
.fcol .state-line{height:40px;padding:0 14px;border:1px solid var(--hair);border-radius:var(--r3);margin-bottom:12px}
.fcol .state-line .sub{color:var(--ink-subtle)}
.cfoot{display:flex;align-items:center;gap:8px;margin-top:16px;color:var(--ink-subtle);font-size:13px}
.cfoot .spacer{flex:1}
.mask{letter-spacing:.12em}
.drop{outline:1px dashed var(--primary-hi);outline-offset:2px}
"""
def page(title, head, sub, panels):
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{title}</title><link rel="stylesheet" href="../grammar-2978/grammar.css">
<style>{SHEET_CSS}</style></head><body>
<div class="sheet-head"><h1>{head}</h1><p>{sub}</p></div>
<div class="sheet">{''.join(panels)}</div>
</body></html>
"""
def panel(label, tag, body, w=1440):
    return f'<section class="panel w{w}"><div class="plabel"><b>{label}</b><span>{tag}</span></div><div class="pbody">{body}</div></section>\n'

# ---- top bars: reduced before the first Add, full after it (decision 33)
def bar_reduced(pill='Sindri running', off=False):
    return f'<div class="topbar"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill{" off" if off else ""}">{pill}</div></div>'
def bar_full(tab='Setup', n='6/8', pill='Connected', off=False, search=True):
    items = ['Home<span class=n>9</span>','Reviews<span class=n>4</span>','Items<span class=n>31</span>','Workflows','Agents<span class=n>3</span>','Projects','Docs']
    out = ''.join(f'<a href="#">{i}</a>' for i in items)
    if tab == 'Setup': out += f'<a class=on href="#">Setup<span class=n>{n}</span></a>'
    elif tab == 'Settings': out += '<a class=on href="#">Settings</a>'
    elif tab == 'Home': out = out.replace('<a href="#">Home','<a class=on href="#">Home',1) + f'<a href="#">Setup<span class=n>{n}</span></a>'
    cmd = '<div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div>' if search else ''
    return f'<div class="topbar"><div class="wordmark">Sindri</div><nav class="nav">{out}</nav><div class="spacer"></div>{cmd}<div class="status-pill{" off" if off else ""}">{pill}</div></div>'

def keys(*pairs): return '<div class="keys">' + ''.join(f'<span><kbd>{k}</kbd>{v}</span>' for k, v in pairs) + '</div>'
K_Q1 = keys(('j/k','move'),('Enter','press'),('Ctrl+Enter','primary'),('Ctrl+V','paste in a token field'))
K_Q1_PLAIN = keys(('j/k','move'),('Enter','press'),('Ctrl+Enter','primary'))
K_Q2 = keys(('j/k','move'),('x','tick'),('a','tick all'),('Space','peek path'),('/','filter'),('Ctrl+Enter','add'),('Esc','back to question 1'))
K_REPOS = keys(('j/k','move'),('x','tick'),('a','tick all'),('Ctrl+Enter','add'),('Esc','Done'))

def crow(glyph, title, sub, btns='', cls='', mono=False):
    return (f'<div class="crow {cls}"><span class="g {glyph}"></span><div><div class="ct">{title}</div><div class="cs{" mono" if mono else ""}">{sub}</div></div>'
            f'<div class="cbtn">{btns}</div></div>')
def fcol(eyebrow, h1, lede, inner, foot='', extra_lede=''):
    gl = f'<div class="ghline"><span class="g linked"></span>{extra_lede}</div>' if extra_lede else ''
    return f'<div class="fcol"><div class="eyebrow2">{eyebrow}</div><h1>{h1}</h1>{gl}<p class="lede">{lede}</p>{inner}{foot}</div>'
def cfoot(text, *btns): return f'<div class="cfoot"><span>{text}</span><span class="spacer"></span>{"".join(btns)}</div>'

# ---------------------------------------------------------------- question 1
GH_SUB = 'pull requests, reviews and CI'
LEDE_Q1 = 'GitHub lets Sindri tick the repos you work on next. Linear and Slack are optional.'
GH_PERMS = 'Pull requests: read and write, Contents: read and write, Checks: read, Issues: read, Metadata: read'
SKIPNOW = btn('Skip for now','ghost')
def r_gh(btns, sub=GH_SUB, glyph='todo', cls='cursor'): return crow(glyph,'GitHub',sub,btns,cls)
def r_lin(btns=None, sub='issues and projects · opens your browser', glyph='todo', cls=''):
    return crow(glyph,'Linear',sub, btns if btns is not None else btn('Sign in to Linear'), cls)
def r_sl(btns=None, sub='messages and mentions · optional · needs a token, 3 steps', glyph='todo', cls=''):
    return crow(glyph,'Slack',sub, btns if btns is not None else btn('Paste a Slack token'), cls)
def q1(inner, foot=None, keys_=None, pill='Sindri running'):
    keys_ = keys_ or (K_Q1 if 'class="field' in inner else K_Q1_PLAIN)
    foot = foot or cfoot('You can connect these later from Connections.', SKIPNOW)
    return bar_reduced(pill) + fcol('Question 1 of 2','Connect your accounts',LEDE_Q1, inner, foot) + keys_
def cont(state=''): return cfoot('GitHub is connected. Linear and Slack are optional.', btn('Continue','primary','Enter',state))

def gh_paste(typed=False, refused='', who='GitHub'):
    mask = '<span class="mask">••••••••••••••••••••</span>' if typed else 'Paste the token'
    save = btn('Save token','primary' if typed else '', 'Enter' if typed else None)
    return guide(
        step(1, "Open GitHub's new fine-grained token page.", btn('Open in browser'), now=not typed and not refused),
        step(2, f'Pick your repos and give it <b>{GH_PERMS}</b>.'),
        step(3, (f'<span class="err-t">{refused}</span>' if refused else 'Paste the token here.'),
             field(mask, True, 260) + save, now=typed or bool(refused)))
def lin_paste(typed=False):
    mask = '<span class="mask">••••••••••••••••••••</span>' if typed else 'Paste the key'
    return guide(step(1,'Open Linear, Settings, API, Personal API keys.', btn('Open in browser'), now=not typed),
                 step(2,'Create a key named <b>Sindri</b>.'),
                 step(3,'Paste the key here.', field(mask, True, 260) + btn('Save key','primary' if typed else '', 'Enter' if typed else None), now=typed))
def sl_paste():
    return guide(step(1,"Open Slack's app page and create the app from Sindri's manifest.", btn('Open in browser'), now=True),
                 step(2,'Install the app to your workspace.'),
                 step(3,'Copy the user token Slack shows you, then paste it here.', field('Paste the token', True, 240) + btn('Save token')))

def q1_panels():
    P = []
    nogh = lambda: r_gh(btn('Sign in to GitHub','primary','Enter','focus'), sub=GH_SUB+' · opens github.com/login/device')
    P.append(panel('Question 1, no gh login found (1440 wide)', 'first screen · §4.2', q1(nogh()+r_lin()+r_sl()), 1440))
    P.append(panel('Question 1, no gh login found (800 wide)', 'first screen · §4.2', q1(nogh()+r_lin()+r_sl()), 800))
    # pick b: with a gh login question 1 never shows; this is the screen Change opens
    P.append(panel('After Change on question 2 (a gh-login box only)', 'decision 13',
        q1(r_gh(btn('Sign in to GitHub','primary','Enter','focus'), sub="gh login found: ada · uses the gh login's scopes")+r_lin()+r_sl()), 720))
    P.append(panel('GitHub: device code shown', 'device flow',
        q1(r_gh(btn('Waiting for GitHub…','','','busy'), sub='Waiting for GitHub', glyph='agent')
           + guide(step(1,'Enter <span class="code">ABCD-1234</span> at <b>github.com/login/device</b>.', btn('Copy code and open GitHub','primary','Enter','focus'), now=True),
                   step(2,'Waiting for GitHub… the code works for 14:32 more.', btn('Cancel')))
           + r_lin()+r_sl()), 720))
    P.append(panel('GitHub: device code expired', 'device flow',
        q1(r_gh('', sub='<span class="err-t">Code expired</span>', glyph='fail')
           + guide(step(1,'<span class="err-t">The code expired before GitHub saw it.</span>', btn('Get a new code','primary','Enter','focus')+btn('Cancel'), now=True))
           + r_lin()+r_sl()), 720))
    P.append(panel('GitHub: sign-in cancelled, row back to not connected', 'decision 31 · cancel',
        q1(r_gh(btn('Sign in to GitHub','primary','Enter','focus'), sub=GH_SUB+' · not connected')+r_lin()+r_sl()), 720))
    P.append(panel('GitHub: token paste open, field empty', 'decision 17',
        q1(r_gh(btn('Paste a GitHub token','primary','','pressed'), sub=GH_SUB+' · needs a token, 3 steps')+gh_paste()+r_lin()+r_sl()), 720))
    P.append(panel('GitHub: token typed, Save token is the primary', 'mismatch 7',
        q1(r_gh(btn('Paste a GitHub token','','','pressed'), sub=GH_SUB+' · needs a token, 3 steps')+gh_paste(typed=True)+r_lin()+r_sl()), 720))
    P.append(panel('GitHub: token refused, field cleared and focused', '§4.2',
        q1(r_gh(btn('Paste a GitHub token','primary','','pressed'), sub=GH_SUB+' · needs a token, 3 steps')+gh_paste(refused='GitHub refused this token: it has no access to Pull requests. Paste a new one.')+r_lin()+r_sl()), 720))
    P.append(panel('SND-SETUP-013: your org blocks the app', '§6',
        q1(r_gh(btn('Paste a GitHub token','primary','','pressed'), sub="Your org blocks Sindri's app. Paste a token instead.", glyph='fail')+gh_paste()+details('SND-SETUP-013 · sindri setup connect github --token-env NAME')+r_lin()+r_sl()), 720))
    P.append(panel('SND-SETUP-014: this build has no app id', '§6',
        q1(r_gh(btn('Paste a GitHub token','primary','','pressed'), sub='This build has no GitHub app id. Paste a token instead.', glyph='fail')+gh_paste()+details('SND-SETUP-014 · sindri setup connect github --token-env NAME')+r_lin()+r_sl()), 720))
    gh_ok = crow('linked','GitHub',GH_SUB+' · connected as ada')
    P.append(panel('Linear: key paste open, field empty (GitHub connected)', '§4.2',
        q1(gh_ok+r_lin(btn('Paste a Linear token','','','pressed'), sub='issues and projects · needs a key, 3 steps', cls='cursor')+lin_paste()+r_sl(), cont()), 720))
    P.append(panel('Linear: key typed, Save key is the primary', 'mismatch 7',
        q1(gh_ok+r_lin(btn('Paste a Linear token','','','pressed'), sub='issues and projects · needs a key, 3 steps', cls='cursor')+lin_paste(typed=True)+r_sl(), cfoot('GitHub is connected. Linear and Slack are optional.', btn('Continue','','Enter'))), 720))
    P.append(panel('Slack: token paste open (never the primary)', '§4.2',
        q1(gh_ok+r_lin()+r_sl(btn('Paste a Slack token','','','pressed'), cls='cursor')+sl_paste(), cont()), 720))
    P.append(panel('Linear: signing in', '§4.2',
        q1(gh_ok+r_lin(btn('Cancel'), sub='Waiting for your browser…', glyph='agent', cls='cursor')+r_sl(), cont()), 720))
    P.append(panel('Linear: waiting on the workspace admin', '§4.2 · decision 16',
        q1(gh_ok+r_lin(btn('Check again'), sub='Waiting on your workspace admin to approve Sindri.', glyph='parked', cls='cursor')+r_sl(), cont()), 720))
    P.append(panel('Linear: sign-in failed', '§4.2',
        q1(gh_ok+r_lin(btn('Retry','primary','Enter','focus'), sub='<span class="err-t">Linear refused the sign-in: access_denied.</span>', glyph='fail', cls='cursor')+r_sl(), cont()), 720))
    P.append(panel('GitHub: the token could not be stored', 'SND-REVIEW-025 rule',
        q1(r_gh(btn('Retry','primary','Enter','focus'), sub="<span class=\"err-t\">Couldn't store the token: the keychain is locked. Nothing was saved.</span>", glyph='fail')+r_lin()+r_sl()), 720))
    P.append(panel('Slack: token refused, field cleared and focused', '§4.2',
        q1(gh_ok+r_lin()+r_sl(btn('Paste a Slack token','','','pressed'), sub='<span class="err-t">Slack refused this token: invalid_auth. Paste a new one.</span>', glyph='fail', cls='cursor')+sl_paste(), cont()), 720))
    P.append(panel('GitHub connected: Continue is the primary and focused', 'decision 15',
        q1(gh_ok+r_lin()+r_sl(), cont('focus')), 720))
    P.append(panel('Button states', 'hover lifts · pressed sinks · focus draws a ring',
        '<div style="display:flex;padding:20px 40px 32px;gap:20px">' + ''.join(
          f'<div style="display:flex;flex-direction:column;gap:14px;align-items:flex-start;width:150px"><span style="font-size:11px;color:var(--ink-3)">{n}</span>{btn("Sign in","primary","Enter",s)}{btn("Skip for now","",None,s)}</div>'
          for n, s in [('rest',''),('hover','hover'),('pressed','pressed'),('focus (ring)','focus')]) + '</div>', 720))
    return P

# ---------------------------------------------------------------- question 2
GHLINE = 'Signed in as ada through gh · <a class="lnk" href="#">Change</a>'
def repo(path, slug, wt='', known=False, on=False, cls='', state=''):
    chips = ''.join(f'<span class="chip">{c}</span>' for c in ([f'worktrees: {wt}'] if wt else []) + (['known'] if known else []))
    sub = f'{slug}' if slug else 'no remote'
    return (f'<div class="crow {cls}"><span class="g box{" on" if on else ""}"></span><div><div class="ct">{path}</div><div class="cs mono">{sub}</div></div>'
            f'<div class="cchips">{chips}</div></div>')
def six(on=True, first_ring=True, cls=''):
    rows = [repo('~/repos/agentic-workflow','jthorare/agentic-workflow','104',True,on,cls+(' cursor' if first_ring else '')),
            repo('~/repos/exports','ledgerly-app/exports','3',True,on,cls),
            repo('~/work/ledgerly-core','ledgerly-app/core','',False,False,cls),
            repo('~/repos/dotfiles','ada/dotfiles','',False,False,cls),
            repo('~/repos/prism-mcp-fork','ada/prism-mcp','1',False,False,cls),
            repo('~/src/old-site','','',False,False,cls)]
    return ''.join(rows)
SKELS = '<div class="crow"><span class="g box"></span><div><div class="ct" style="width:200px;height:8px;background:var(--s2);border-radius:2px;margin:6px 0"></div></div><div></div></div><div class="crow"><span class="g box"></span><div><div class="ct" style="width:150px;height:8px;background:var(--s2);border-radius:2px;margin:6px 0"></div></div><div></div></div>'
def SL(g, what, sub='', b='', cls=''):
    return f'<div class="state-line {cls}"><span class="g {g}"></span><span class="what">{what}<span class="sub">{sub}</span></span><span class="btns">{b}</span></div>'
ADD2 = btn('Add 2 repos','primary','Ctrl+Enter')
LOOK = lambda c='': btn('Look in another folder…', c)
FOOT2 = '2 of 6 ticked'
def q2(status, rows, foot, gh=True, lede='Sindri looks under your home folder, 3 levels deep. Known repos are ticked; worktrees fold into their main checkout.', keys_=K_Q2):
    return bar_reduced() + fcol('Question 2 of 2 · the last question; Home opens next','Where are your repos?', lede, status + rows, foot, extra_lede=GHLINE if gh else '') + keys_

def repos_panels():
    P = []
    scan = SL('agent','Scanning ~ · 3 levels · 41 folders','6 repos so far',btn('Stop scan'))
    P.append(panel('Scanning (1440 wide)', '§4.3', q2(scan, six()+SKELS, cfoot(FOOT2, LOOK(), ADD2)), 1440))
    P.append(panel('Scanning (800 wide)', '§4.3', q2(scan, six()+SKELS, cfoot(FOOT2, LOOK(), ADD2)), 800))
    P.append(panel('Done, repos found', '§4.3', q2(SL('done','6 repos under ~, 3 levels deep'), six(), cfoot(FOOT2, LOOK(), ADD2)), 720))
    P.append(panel('Done, none found: Look in another folder is the primary, Skip for now beside it', '§4.3 · decision 31',
        q2(SL('todo','No git repos under ~, 3 levels deep.'), '', cfoot('0 of 0 ticked', SKIPNOW, LOOK('primary')), lede='Sindri looks under your home folder, 3 levels deep.'), 720))
    P.append(panel('Scan failed: Retry is the primary, Skip for now beside it', '§4.3 · SND-SETUP-008',
        q2(SL('fail',"Couldn't read ~: permission denied.",'',btn('Retry','primary','Enter','focus'),'err'), '', cfoot('0 of 0 ticked', SKIPNOW, LOOK())), 720))
    P.append(panel('Adding: rows read-only, no primary', '§4.3',
        q2(SL('agent','Adding 2 repos…'), six(first_ring=False, cls='dim'), cfoot(FOOT2, LOOK('busy'), btn('Adding…','','','busy'))), 720))
    failed_row = ('<div class="crow"><span class="g fail"></span><div><div class="ct">~/repos/exports</div><div class="cs"><span class="err-t">That folder isn\'t the top of a git repo.</span></div></div>'
                  '<div class="cbtn">' + btn('Retry','','Enter') + '</div></div>')
    P.append(panel('Add failed for one repo: Continue to Home is the primary, Retry on the row is secondary', '§4.3 · SND-SETUP-001',
        q2(SL('fail','1 of 2 repos added. ~/repos/exports could not be added.','','','err'),
           failed_row + repo('~/work/ledgerly-core','ledgerly-app/core')+repo('~/repos/dotfiles','ada/dotfiles')+repo('~/repos/prism-mcp-fork','ada/prism-mcp','1')+repo('~/src/old-site',''),
           cfoot('1 repo added', LOOK(), btn('Continue to Home','primary','Enter','focus'))), 720))
    f2 = lambda p, s, w: (f'<div class="crow"><span class="g fail"></span><div><div class="ct">{p}</div><div class="cs"><span class="err-t">{s}</span></div></div><div class="cbtn">{btn("Retry","primary","Enter","focus") if w else btn("Retry")}</div></div>')
    P.append(panel('Every add failed: Retry is the primary, Skip for now beside it', 'decision 31 · SND-SETUP-001',
        q2(SL('fail',"None of the 2 repos could be added.",'','','err'),
           f2('~/repos/agentic-workflow',"That folder isn't the top of a git repo.",True)+f2('~/repos/exports',"That folder isn't the top of a git repo.",False),
           cfoot('0 repos added', SKIPNOW, LOOK())), 720))
    P.append(panel('No account connected: nothing pre-ticked', '§4.3',
        q2(SL('done','6 repos under ~, 3 levels deep'), six(on=False), cfoot('0 of 6 ticked', LOOK(), btn('Add 0 repos','primary','Ctrl+Enter','busy')),
           gh=False, lede='Tick the repos you want Sindri to watch.'), 720))
    P.append(panel('Couldn\'t read your GitHub repos: Retry, the scan still works', 'gap 7',
        q2(SL('fail',"Couldn't read your GitHub repos.",'Known repos aren\'t marked.',btn('Retry','primary','Enter','focus'),'err') ,six(on=False,first_ring=False), cfoot('0 of 6 ticked', LOOK(), btn('Add 0 repos','','Ctrl+Enter','busy'))), 720))
    P.append(panel('Stop scan pressed: the rows found stay', '§4.3',
        q2(SL('todo','Scan stopped · 41 folders · 4 repos found','',btn('Scan again')),
           repo('~/repos/agentic-workflow','jthorare/agentic-workflow','104',True,True,'cursor')+repo('~/repos/exports','ledgerly-app/exports','3',True,True)+repo('~/work/ledgerly-core','ledgerly-app/core')+repo('~/repos/dotfiles','ada/dotfiles'),
           cfoot(FOOT2, LOOK(), ADD2)), 720))
    P.append(panel('A folder dragged onto the window: it joins the list ticked', '§4.3',
        q2(SL('done','6 repos under ~, 3 levels deep · 1 added by you'), six(first_ring=False) + repo('~/work/side-project','ada/side-project','',False,True,'drop'),
           cfoot('3 of 7 ticked', LOOK(), btn('Add 3 repos','primary','Ctrl+Enter'))), 720))
    return P

def repos_page_panels():
    P = []
    def rp(rows, foot):
        return bar_full('Setup','6/8') + fcol('Setup › Add repos','Repos','Tick the repos you want Sindri to watch. Repos Sindri already watches are hidden.', SL('done','4 repos under ~ that Sindri does not watch yet') + rows, foot) + K_REPOS
    rest = repo('~/work/ledgerly-core','ledgerly-app/core')+repo('~/repos/dotfiles','ada/dotfiles')+repo('~/repos/prism-mcp-fork','ada/prism-mcp','1')+repo('~/src/old-site','')
    P.append(panel('Repos page, nothing ticked: Done is the primary', 'decision 32', rp(rest.replace('class="crow ','class="crow ',1), cfoot('0 of 4 ticked', LOOK(), btn('Done','primary','Esc','focus'))), 720))
    rest1 = repo('~/work/ledgerly-core','ledgerly-app/core','',False,True,'cursor')+repo('~/repos/dotfiles','ada/dotfiles')+repo('~/repos/prism-mcp-fork','ada/prism-mcp','1')+repo('~/src/old-site','')
    P.append(panel('Repos page, one ticked: Add 1 repo is the primary, Done is secondary', 'decision 32', rp(rest1, cfoot('1 of 4 ticked', LOOK(), btn('Done','','Esc'), btn('Add 1 repo','primary','Ctrl+Enter'))), 720))
    return P

# ---------------------------------------------------------------- setup page
def sbody(title, count, rows, nav=None, extra=''):
    return (nav or bar_full()) + f'<div class="header"><h1>{title}</h1><span class="count">{count}</span></div>{extra}<div class="list" style="--facts:136px;--col-act:300px"><div class="group"><span class="chev">▾</span>Steps <span class="n">8</span><span class="right">the same steps as <b style="font-weight:500">sindri setup</b>, in the same order</span></div>{rows}</div>' + keys(('j/k','move'),('Enter','press the primary'),('.','actions'),('Tab','buttons'))
def sd(t, sub, fact='', b='', cls='read'): return row('done', t, sub, fact, b, cls)
CUR = sd('Sindri is current','running 7c81646, the installed one')
ACC = sd('Connect your accounts','GitHub is connected as ada · Linear and Slack are optional and not connected','',btn('Accounts'))
REP = sd('Add your repos','jthorare/agentic-workflow and ledgerly-app/exports','2 repos',btn('Add repos'))
PROF = sd('Profile saved','every setting read, none wrong')
AG = sd('Agent for workflows: claude-code','found installed; workflows launch their agent steps with it')
MCP = sd("Agents can use Sindri's tools",'registered in agentic-workflow and exports')
KEEP = ' You can keep working; it finishes in the background.'
MAP1 = row('agent','Build the map','Indexing files: 7,000 of 15,223 in agentic-workflow.'+KEEP,'<span class="bar"><i style="width:46%"></i></span>46%','','unread')
MAP2 = row('agent','Build the map','Building the map: 4,720 of 15,223 files in agentic-workflow.'+KEEP,'<span class="bar"><i style="width:31%"></i></span>31%','','unread')
MAPD = sd('Build the map','a current map of agentic-workflow and exports')
HELLO_OK = lambda ring=True: row('done','Run hello-sindri',"hello-sindri ran: made 'Hello from Sindri' in agentic-workflow.",'', btn('Open item','primary' if ring else '','Enter','focus' if ring else '')+btn('Run again'), 'read cursor' if ring else 'read')
HELLO_RUN = row('agent','Run hello-sindri','hello-sindri is running: step 2 of 4.','','','unread')
HELLO_FAIL = row('fail','Run hello-sindri','hello-sindri failed at Make a greeting: the step ran out of time after 60 s.','<span class="err-t">failed 1m ago</span>', btn('Open the run','primary','Enter','focus')+btn('Run again'),'unread cursor')
def ring(r): return r.replace('class="row fix ','class="row fix cursor ',1)
def steps_panels():
    P = []
    P.append(panel('Map phase 1: indexing files; hello-sindri ran by itself, Open item holds the primary and the ring', '§4.4 · decision 18',
        sbody('Setup','6 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+MAP1+HELLO_OK())))
    P.append(panel('Map phase 2: building the map', '§4.4 · decision 18',
        sbody('Setup','6 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+MAP2+HELLO_OK())))
    P.append(panel('hello-sindri running by itself: no button, no primary on the page, the ring on the first row not done', '§4.4 · decision 19',
        sbody('Setup','5 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+ring(MAP2)+HELLO_RUN)))
    P.append(panel('hello-sindri failed: Open the run is the primary, Run again beside it', '§4.4',
        sbody('Setup','7 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+MAPD+HELLO_FAIL)))
    noag = row('you','Agent for workflows','No coding agent found. Install Claude Code, Codex or Cursor, then check again.','', btn('Check again','primary','Enter','focus'),'unread cursor')
    hello_todo = row('todo','Run hello-sindri','a first workflow that needs no account and no network','', btn('Run hello-sindri')+btn('Dismiss','ghost'),'unread')
    P.append(panel('Agent: none installed; Dismiss stays on the hello-sindri row', '§4.4',
        sbody('Setup','5 of 8 steps done', CUR+ACC+REP+PROF+noag+MCP+MAP2+hello_todo, bar_full('Setup','5/8'))))
    mapfail = row('fail','Build the map','The map of ledgerly-app/exports failed: the disk is full (No space left on device). agentic-workflow is done.','<span class="err-t">Failed</span>', btn('Retry','primary','Enter','focus'),'unread cursor')
    P.append(panel('Map failed for one repo: the step is Failed, the other repo is done', '§4.4 · must 4',
        sbody('Setup','6 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+mapfail+HELLO_OK(False))))
    mapstop = row('fail','Build the map','The map build stopped at 4,720 of 15,223 files 10 minutes ago.','<span class="err-t">Stopped</span>', btn('Retry','primary','Enter','focus'),'unread cursor')
    P.append(panel('Map build stopped with no progress', '§4.4 · must 4',
        sbody('Setup','6 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+mapstop+HELLO_OK(False))))
    stale = row('parked','Sindri is current','A newer Sindri is installed (7c81646, built today 10:31).','<span class="err-t">stale</span>', btn('Restart Sindri','primary','Ctrl+Shift+R','focus'),'unread cursor')
    P.append(panel('The serve row is stale', '§4.4 · §4.5',
        sbody('Setup','5 of 8 steps done', stale+ACC+REP+PROF+AG+MCP+MAP2+HELLO_OK(False), bar_full('Setup','5/8'))))
    exp = row('parked','Connect your accounts','Your Linear sign-in expired.','<span class="err-t">expired</span>', btn('Reconnect Linear','primary','Enter','focus'),'unread cursor')
    P.append(panel('sources: an account expired', '§4.4',
        sbody('Setup','5 of 8 steps done', CUR+exp+REP+PROF+AG+MCP+MAP2+HELLO_OK(False), bar_full('Setup','5/8'))))
    rd = row('fail','Connect your accounts','The last read of jthorare/agentic-workflow failed 2m ago.','<span class="err-t">failed 2m ago</span>', btn('Retry','primary','Enter','focus'),'unread cursor')
    P.append(panel('sources: the first read failed (the primary because it is the first step not done)', '§4.4',
        sbody('Setup','5 of 8 steps done', CUR+rd+REP+PROF+AG+MCP+MAP2+HELLO_OK(False), bar_full('Setup','5/8'))))
    P.append(panel('Loading: the last read from the local home paints at once, one line says it is updating', '§4.4 · F9',
        sbody('Setup','6 of 8 steps done', CUR+ACC+REP+PROF+AG+MCP+MAP2+HELLO_OK(False), extra=SL('agent','Updating…'))))
    P.append(panel("Steps read failed: Setup can't be read", '§4.4',
        bar_full('Setup','6/8') + '<div class="header"><h1>Setup</h1></div>' + SL('fail',"Setup can't be read right now.",'',btn('Retry','primary','Enter','focus'),'err')
        + keys(('Enter','press the primary'))))
    down = (row('fail',"Sindri isn't running",'Nothing answered on 127.0.0.1:7410.','<span class="err-t">not running</span>', btn('Start Sindri','primary','Enter','focus'),'unread cursor')
            + row('todo','Connect your accounts','Starts once Sindri is running','','','read') + REP.replace(btn('Add repos'),'') + PROF + AG
            + row('todo',"Agents can use Sindri's tools",'Starts once Sindri is running','','','read') + row('todo','Build the map','Starts once Sindri is running','','','read') + row('todo','Run hello-sindri','Starts once Sindri is running','','','read'))
    P.append(panel('Serve down: steps that need Sindri wait', '§4.4',
        sbody('Setup','3 of 8 steps done', down, bar_full('Setup','3/8', pill='Not running', off=True))))
    alld = CUR+ACC+REP+PROF+AG+MCP+MAPD+sd('Run hello-sindri','hello-sindri ran: made \'Hello from Sindri\' in agentic-workflow.')
    alld += '<div class="dpanel" style="margin-top:var(--sp3);background:transparent;border:0">Not imported: agent session history and other tools\' settings.</div>'
    P.append(panel('All done: the Setup tab leaves the nav, Settings holds the list', '§4.4',
        sbody('Settings','Setup · 8 of 8 steps done', alld, bar_full('Settings'))))
    return P

# ---------------------------------------------------------------- not running
def nr_half(pill, off, glyph, h2, p, acts, det, cap):
    return (f'<div class="half">{bar_reduced(pill, off)}<div class="nrbody"><div class="empty"><div class="eg"><span class="g {glyph}"></span></div><h2>{h2}</h2><p>{p}</p>'
            f'<div class="acts">{acts}</div>{("<details class=details open><summary>Details</summary><code>"+det+"</code></details>") if det else ""}</div></div>'
            f'<div class="keys"><span class="cap" style="margin-left:0">{cap}</span></div></div>')
def not_running_page():
    css = """.frames{display:grid;grid-template-columns:720px 720px;width:1440px}
.half{height:360px;display:grid;grid-template-rows:var(--bar-h) 1fr var(--keys-h);border-bottom:1px solid var(--hair-strong)}
.half:nth-child(odd){border-right:1px solid var(--hair-strong)}
.half .empty{padding-top:var(--sp6)}
.nrbody{overflow:hidden}"""
    H = [
     nr_half('Starting', True, 'agent', 'Starting Sindri…', 'Sindri is starting on this machine. Home loads by itself when it answers.', btn('Starting…','primary','','busy'), 'Waiting for 127.0.0.1:7410 · 4.2 s', '1 · The app opened: Sindri starts by itself, no click'),
     nr_half('Not running', True, 'fail', "Sindri didn't start", "Sindri didn't start: port 7410 is already in use.", btn('Retry','primary','Enter','focus'), 'log: ~/.agentic-workflow/sindri-app/logs/serve.log<br>To start it yourself, run: sindri serve', "2 · Didn't start in 30 s"),
     nr_half('Not running', True, 'parked', "Sindri isn't running", 'Sindri stopped after a failed start. Start it and Home loads your reviews, runs and messages. This takes about 10 seconds.', btn('Start Sindri','primary','Enter','focus'), 'Nothing answered on 127.0.0.1:7410. To start it yourself, run: sindri', '3 · Not running, only after a failed start'),
     nr_half('Starting', True, 'agent', 'Sindri is already starting on this machine.', 'Home loads by itself when it answers. This page keeps checking.', '', 'Waiting for 127.0.0.1:7410 · 11.8 s', '4 · A second serve was refused'),
     nr_half('Stopping', True, 'parked', 'Sindri is still stopping.', 'The old Sindri has not exited yet.', btn('Start Sindri','primary','Enter','focus'), 'The old Sindri (pid 48213) has not exited. Start Sindri starts nothing while that pid lives.<br>log: ~/.agentic-workflow/sindri-app/logs/serve.log', '5 · Still stopping (pid alive after 30 s)'),
     nr_half('Not running', True, 'fail', "The Sindri program isn't installed next to this app.", 'This is a broken install, not something to click through.', '', 'scripts/install-cs.sh --link@@', '6 · No installed sindri beside sindri-ui: no button, focus on Details'),
    ]
    H[5] = H[5].replace('<summary>Details</summary>','<summary style="outline:2px solid var(--primary-hi);outline-offset:2px;border-radius:4px;display:inline-block;padding:0 4px">Details</summary>').replace('@@','')
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Sindri · Not running</title><link rel="stylesheet" href="../grammar-2978/grammar.css">
<style>{SHEET_CSS}{css}</style></head><body><div class="frames">{''.join(H)}</div></body></html>
"""

SLACK = lambda b='Paste a Slack token', cls='': row('todo','Slack','messages and mentions · optional','needs a token, 3 steps', btn(b, cls))
def accounts_page(rows, foot_btn, last, tab='Setup'):
    return (bar_full(tab)
            + '<div class="header"><h1>Accounts</h1><span class="count">Connect or reconnect the accounts Sindri reads</span></div>'
            + f'<div class="list" style="--facts:200px"><div class="group"><span class="chev">▾</span>Accounts <span class="n">3</span></div>{rows}</div>'
            + f'<div class="actionbar"><span>{last}</span><span class="spacer"></span>{foot_btn}</div>' + keys(('j/k','move'),('Enter','press'),('Ctrl+Enter','primary'),('Esc','Done')))
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
    nav = bar_full('Setup','6/8')
    body = (nav + '<div class="header"><h1>Setup</h1><span class="count">6 of 8 steps done</span></div>'
        + '<div class="list" style="--facts:136px;--col-act:200px"><div class="group"><span class="chev">▾</span>Steps <span class="n">8</span></div>'
        + row('done','Connect your accounts','GitHub is connected as ada · Linear is connected as ada · Slack is optional and not connected','', btn('Accounts'), 'read cursor')
        + row('done','Add your repos','jthorare/agentic-workflow and ledgerly-app/exports','2 repos','', 'read')
        + row('done','Profile saved','every setting read, none wrong','','', 'read')
        + '</div>' + '<div class="keys"><span><kbd>j/k</kbd>move</span><span><kbd>Enter</kbd>press the primary</span><span><kbd>.</kbd>actions</span></div>')
    P.append(panel('Back on the Setup page: the accounts row is done', 'decision 28', body))
    return P

def navbar(pill='Connected'):
    return bar_full('Home', pill=pill, off=pill != 'Connected')
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
    P.append(panel('Runs open', '§4.5 · decision 11', navbar()+sline('parked', f'Sindri restarts when 3 finish: {L1}, {L2}, {L3}.', btn('Restart when they finish','primary','Ctrl+Shift+R'), det=False)+HOME))
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


# ---------------------------------------------------------------- states (patched from the base sheet)
def patch_states():
    p = os.path.join(OUT, 'states.html')
    s = open(p).read()
    s = s.replace('(build 1112 → 1118)', '(7c81646, built today 10:31)').replace('1,204 / 3,880 files', '4,720 / 15,223 files')
    red = lambda pill='Sindri running': (f'<div class="topbar" style="height:40px"><div class="wordmark">Sindri</div><div class="spacer"></div><div class="status-pill">{pill}</div></div>')
    sec = lambda lab, tag, inner: f'<section class="panel"><div class="plabel"><b>{lab}</b><span>{tag}</span></div><div class="pbody">{inner}</div></section>\n'
    s = re.sub(r'<section class="panel"><div class="plabel"><b>No profile</b>.*?</section>\n?',
        sec('Home: no profile, only after a restart error (first run never reaches it)', '#3054 · #3113', red('Not set up').replace('status-pill"','status-pill off"') + '<div class="header" style="height:40px"><h1>Home</h1></div>'
            '<div class="state-line att"><span class="g you"></span><span class="what">Finish setting up Sindri: connect your accounts.</span><span class="btns"><span class="btn primary">Connect your accounts <kbd>Enter</kbd></span></span></div>'), s, flags=re.S)
    s = re.sub(r'<section class="panel"><div class="plabel"><b>Profile with no repo</b>.*?</section>\n?',
        sec('Home: after Skip for now on question 2 (no repo yet)', '#3054 · decision 31', red() + '<div class="header" style="height:40px"><h1>Home</h1></div>'
            '<div class="state-line att"><span class="g you"></span><span class="what">Finish setting up Sindri: add your repos.</span><span class="btns"><span class="btn primary">Find my repos <kbd>Enter</kbd></span></span></div>'), s, flags=re.S)
    full = bar_full('Home').replace('class="topbar"', 'class="topbar" style="height:40px"')
    hn = lambda inner: full + '<div class="header" style="height:40px"><h1>Home</h1></div>' + inner
    emp = lambda g, h, pp, acts: f'<div class="empty" style="padding-top:var(--sp4)"><div class="eg"><span class="g {g}"></span></div><h2>{h}</h2><p>{pp}</p><div class="acts">{acts}</div></div>'
    wide = lambda lab, tag, inner: f'<section class="panel" style="grid-column:1/-1;width:1440px"><div class="plabel"><b>{lab}</b><span>{tag}</span></div><div class="pbody">{inner}</div></section>\n'
    new = ''
    new += wide('Home: hello-sindri being made', '#3019 · decision 19', hn(emp('agent','Getting a first workflow ready…','Sindri is making hello-sindri in agentic-workflow and runs it by itself. This takes a few seconds.','')))
    new += wide('Home: hello-sindri running by itself', '#3019 · decision 19', hn(emp('agent','hello-sindri is running: step 2 of 4.','It makes an item in agentic-workflow when it finishes.','')))
    new += wide('Home: hello-sindri succeeded, the map is still building', '#3019 · must 3 · §4.6', hn(
        '<div class="list" style="--facts:176px;--col-act:168px;padding:0">'
        '<div class="row fix cursor"><span class="g done"></span><div class="title"><span class="t">hello-sindri ran: made \'Hello from Sindri\' in agentic-workflow</span></div><div class="fact mono">wf/hello-sindri</div><div class="fixbtn"><span class="btn primary">Open item <kbd>Enter</kbd></span></div></div></div>'
        '<div class="state-line" style="height:auto;min-height:32px;padding:6px 24px"><span class="g agent"></span><span class="what" style="white-space:normal;overflow:visible">Building the map of agentic-workflow: 31%. The map is still building, you can keep working.</span><span class="btns"><a class="btn" href="#">Open Setup</a></span></div>'))
    new += wide('Home: hello-sindri failed', '#3019 · pick c', hn(emp('fail','hello-sindri failed at Make a greeting: the step ran out of time after 60 s.','Open the run to see its steps, or run it again.','<span class="btn primary">Open the run <kbd>Enter</kbd></span><span class="btn">Run again</span>')))
    new += wide('Home: a map build failed', '#3019 · must 4 · §4.6', hn(
        '<div class="state-line err"><span class="g fail"></span><span class="what">The map of ledgerly-app/exports failed. agentic-workflow is done.</span><span class="btns"><span class="det">Details ▸</span><span class="btn primary">Retry <kbd>Enter</kbd></span></span></div>'))
    new += wide('Home: own rows present, hello-sindri as a secondary line', '#3019 · decision 22', hn(
        '<div class="list" style="--facts:176px;padding:0">'
        '<div class="row unread cursor"><span class="g pr"></span><div class="title"><span class="id">#4182</span><span class="t">Paginate workspace audit export</span></div><div class="fact mono">ledgerly…/exports</div><div class="age">38m</div><div class="wait">review</div></div>'
        '<div class="row unread"><span class="g fail"></span><div class="title"><span class="id">r-0412</span><span class="t">Nightly usage rollup failed</span></div><div class="fact mono">wf/usage-rollup</div><div class="age">2h</div><div class="wait">retry</div></div>'
        '<div class="row fix read" style="--col-act:236px"><span class="g done"></span><div class="title"><span class="t">hello-sindri ran</span></div><div class="fact mono">wf/hello-sindri</div><div class="fixbtn"><span class="btn ghost">Run again</span><span class="btn ghost">Dismiss</span></div></div></div>'))
    s = s.replace('</div></body></html>', new + '</div></body></html>')
    s = s.replace('--facts:104px', '--facts:176px')
    # the base sheet's own panels that carry a top bar get the full bar with counts after the first Add: the stale panel
    open(p, 'w').write(s)

def write(name, txt):
    open(os.path.join(OUT, name), 'w').write(txt)

if __name__ == '__main__':
    data = subprocess.run(['git','-C','/home/thor/repos/agentic-workflow','show','0a816f94bf:states.html'], capture_output=True, text=True, check=True).stdout
    write('states.html', data)
    write('setup-connect.html', page('Sindri · Question 1: connect your accounts', 'Question 1 of 2: connect your accounts',
        'A centred 640 px column on the reduced top bar. With a gh login question 1 never shows; the second frame is what Change opens. Every state at the fixture numbers, one primary each.', q1_panels()))
    write('setup-repos.html', page('Sindri · Question 2: where are your repos', 'Question 2 of 2: where are your repos',
        'A centred 640 px column. Scan from ~, depth 3. Every dead end has a way on: Continue to Home, or Skip for now.', repos_panels()))
    write('setup-repos-page.html', page('Sindri · Repos page', 'The Repos page: adding repos after first run',
        'Opened from the Add repos button on the Setup page. No "Question 2 of 2"; repos already added are hidden; Done goes back to Setup.', repos_page_panels()))
    write('setup-accounts.html', page('Sindri · Accounts page', 'Accounts: question 1 opened from elsewhere',
        'No "Question 1 of 2", the footer holds Done, and Esc presses Done. Two ways in, Done pressed, and the page it returns to. The full top bar, as after the first Add.', accounts_panels()))
    write('setup-steps.html', page('Sindri · Setup', 'The Setup page', 'One RowList of the eight steps, in the order of sindri setup. The ring and the one primary sit on the same row.', steps_panels()))
    write('stale-line.html', page('Sindri · Stale serve line', 'The stale serve line', 'One 32 px line under the full top bar. A build is written as its commit and its built time.', stale_panels()))
    write('not-running.html', not_running_page())
    patch_states()
