import pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'
FRAMES = []
def write(n, s): (D / f'messages-{n}.html').write_text(s); FRAMES.append(n)

NAV = ('<a>Home<span class="n">9</span></a><a>Reviews<span class="n">4</span></a><a class="on">Messages<span class="n">25</span></a><a>Items<span class="n">31</span></a><a>Workflows</a>'
       '<a>Agents<span class="n">3</span></a><a>Projects</a><a>Notes</a><a>Context</a>')
LIST_KEYS = [('j/k', 'move'), ('Space', 'peek'), ('Enter', 'open'), ('r', 'open in Slack'), ('e', 'done'), ('h', 'snooze'), ('/', 'filter'), ('?', 'all keys')]
MSG_KEYS = [('j/k', 'move'), ('Enter', 'open thread'), ('r', 'open in Slack'), ('e', 'done'), ('h', 'snooze'), ('n', 'next unread'), ('Home/End', 'top, bottom'), ('Esc', 'back to list'), ('?', 'all keys')]
def kk(items): return ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in items)
def keys(mode, items): return f'<span><span class="mode">{mode}</span></span>' + kk(items)

def shell(title, header, body, kbar, sr, extra='', w=1440, pill='Connected', off=False):
    st = f'body{{width:{w}px}}' + ('.cmdk{width:150px;white-space:nowrap;overflow:hidden}.cmdk kbd{display:none}' if w < 1200 else '')
    return f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>{title}</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="messages.css"><style>{st}</style></head>
<body><div class="app">
<div class="topbar"><span class="wordmark">Sindri</span><nav class="nav">{NAV}</nav><span class="spacer"></span><div class="cmdk">Search or run a command<kbd>Ctrl+K</kbd></div><span class="status-pill{' off' if off else ''}">{pill}</span></div>
{header}
{body}
<div class="foot"><div class="keys">{kbar}</div><div class="sr"><b>Screen reader</b>{sr}</div></div>
{extra}</div></body></html>
'''
STATES = {
 'live': ('', 'Live (polling every 30 s) · joi', ''),
 'behind': ('att', 'Behind: history is limited to 15 messages per minute · joi', ''),
 'offline': ('off', 'Offline: last read 09:41', ''),
 'expired': ('err', 'Slack token expired', '<span class="btn primary">Reconnect</span>'),
 'scope': ('att', 'Needs scope search:read', '<span class="btn primary">Reconnect</span>'),
}
def hdr(state='live', title='Messages', count='25 need reply', hint=True):
    c, t, b = STATES[state]
    h = '<span class="hint">Ctrl+K search or jump</span>' if hint else ''
    return f'<div class="header"><h1>{title}</h1><span class="count">{count}</span>{h}<span class="stat {c}">{t}{b}</span></div>'

# ---------------- rail
def rail(on='Needs reply', slim=False, starred=True, counts=(25, 14), later=False, focus=None):
    def it(name, n, ic):
        cls = 'on' if on == name else ''
        if focus == name: cls += ' focus'
        return f'<div class="it {cls}"><span class="ic">{ic}</span><span class="lbl">{name}</span><span class="n">{n}</span></div>'
    o = f'<aside class="rail{" slim" if slim else ""}">' + it('Needs reply', counts[0], '<b class="l">N</b>') + it('Unread', counts[1], '<b class="l">U</b>')
    if later: o += it('Later', 2, 'L') + it('Drafts', 1, 'D')
    o += '<div class="sec">Starred</div>'
    if starred:
        for nm, n, h in [('harbor', 2, '#'), ('ferry-ops', '', '#'), ('Sam', '', 'S'), ('Kim, Lee', '', 'K')]:
            c = 'on' if on == nm else ''
            o += f'<div class="it {c}"><span class="ic hsh">{h}</span><span class="lbl">{nm}</span><span class="n">{n}</span></div>'
    else:
        o += '<div class="emp">No starred channels yet. Star it from the <kbd>.</kbd> panel, or find it with <kbd>Ctrl+K</kbd>.</div>'
    o += '<div class="foot2"><kbd>Ctrl+K</kbd><span class="lbl">all channels</span></div></aside>'
    return o

# ---------------- needs reply rows
CH = lambda t: f'<span class="lc">{t}</span>'
ROWS = [  # cls, who, place, title, chips, reason, age
 (3, 'Sam', 'DM', 'can you ship the fare table today?', [], 'DM', '8m'),
 (2, 'Ada', '#harbor', '@joi review #3120 when you can', ['#3120', '#3055'], 'review ask', '2h'),
 (3, 'Mira', 'DM', 'did the export retry change land? asking before I tell the customer', ['#3104'], 'DM', '2h'),
 (3, 'Priya', 'DM', 'can you look at the SSO rollout split before standup?', [], 'DM', '3h'),
 (2, 'Tomas', '#billing', 'review please: proration preview rounds half cents the wrong way', ['#3098'], 'review ask', '3h'),
 (3, 'Dani', '#harbor', '@joi what is the hold time for group bookings?', [], 'mention', '4h'),
 (3, 'Kim', 'DM', 'could you confirm the 90 s holds before I post the notice?', [], 'DM', '5h'),
 (2, 'Jo', '#ferry-ops', 'please look at the dock schedule PR, CI is green now', ['#3112'], 'review ask', '5h'),
 (3, 'Lee', '#general', '@joi when can we get the demo env back?', [], 'mention', '6h'),
 (3, 'Sana', 'DM', 'need you on the invoice rounding thread when you have a minute', [], 'DM', '6h'),
 (2, 'Ravi', '#harbor', 'review #3091 when you can, the Home spec is ready for meta', ['#3091'], 'review ask', '7h'),
 (3, 'Pat', 'DM', 'can you send the fare table diff? I will review tonight', [], 'DM', '7h'),
 (3, 'Noor', '#billing', '@joi could you re-run the export for ACME?', [], 'mention', '8h'),
 (2, 'Ada', '#harbor', 'also #3125 is stacked on 3120, can you look at both?', ['#3125'], 'review ask', '8h'),
 (3, 'Eli', 'DM', 'when can you review the retry cap proposal?', [], 'DM', '9h'),
 (3, 'Zoe', '#ferry-ops', '@joi please confirm the Friday sailing is on', [], 'mention', '9h'),
 (2, 'Omar', '#harbor', 'seat hold PR needs your look, it blocks the release', ['#3120'], 'review ask', '1d'),
 (3, 'Hana', 'DM', 'can you check the staging queue? it looks stuck again', [], 'DM', '1d'),
 (3, 'Ben', 'DM', 'need you to decide on the backfill before Thursday', [], 'DM', '1d'),
 (2, 'Iris', '#billing', 'review ask: ledger v3 migration, one file', ['#3101'], 'review ask', '1d'),
 (3, 'Cal', '#general', '@joi could you share the Notes spec link?', [], 'mention', '2d'),
 (3, 'Uma', 'DM', 'please look at the draft reply to the customer', [], 'DM', '2d'),
 (3, 'Fay', 'DM', 'can you take the on-call swap for Tuesday?', [], 'DM', '2d'),
 (2, 'Gus', '#harbor', 'review #3109 when free, the Agents page states and glyphs', ['#3109'], 'review ask', '3d'),
]
NEW_ROW = (3, 'Lee', 'DM', 'got a sec?', [], 'DM', '2m')
def mrow(r, mode='full', cursor=False, sel=False, hv=False, unread=False, cls='', actions=False):
    c, who, pl, ti, chips, rs, age = r
    chips_h = ''.join(CH(x) for x in chips) if mode == 'full' else ''
    k = f'mrow {"hasact" if actions else ""} {"narrow" if mode != "full" else ""} {"n2" if mode == "n2" else ""} {"cursor" if cursor else ""} {"sel" if sel else ""} {"hv" if hv else ""} {"unread" if unread else ""} {cls}'
    acts = ('<div class="actions"><span class="btn">Open in Slack <kbd>r</kbd></span><span class="btn">Snooze <kbd>h</kbd></span><span class="btn">Done <kbd>e</kbd></span></div>') if actions else ''
    return f'<div class="{k}"><span class="g c{c}"></span><span class="who">{who}</span><span class="pl">{pl}</span><span class="ti"><span class="t">{ti}</span>{chips_h}</span><span class="rs">{rs}</span><span class="age">{age}</span>{acts}</div>'
def needs_list(mode='full', n=22, cur=1, newband=True, sel=1):
    o = ''
    if newband:
        o += '<div class="mgroup new">New<span class="n">1</span></div><div class="newband">' + mrow(NEW_ROW, mode) + '</div>'
    o += '<div class="mgroup">Needs reply<span class="n">24</span></div>'
    for i, r in enumerate(ROWS[:n]):
        o += mrow(r, mode, cursor=(i == cur), sel=(i == sel), actions=(i == cur))
    return f'<div class="mlist">{o}</div>'

# ---------------- peek
def peek_thread(release=1):
    lr = '<div class="lrow"><span class="g pr"></span><span class="id">#3120</span><span class="t">Seat holds: 90 s</span><span class="r"><b>CI green</b> · asked of you · ada</span></div>'
    return f'''<aside class="peek"><div class="peek-head"><h2>Thread · #harbor</h2><span class="id">slack:thread:harbor</span><span class="icon">⤢</span><span class="icon">✕</span></div>
<dl class="peek-fields"><dt>Class</dt><dd><span class="g c2"></span>Review ask</dd><dt>Waits on</dt><dd>you</dd><dt>Linked</dt><dd><span class="g merged"></span>#3120 · #3055 · #3125</dd></dl>
<div class="mp"><div class="eyebrow">Thread, 3 messages</div>
<div class="cl first"><span class="av20" style="background:#3a3f58">AD</span><span class="tx"><span class="nm">Ada</span><span class="tm">07:30</span><br>seat holds are still 120 s in staging</span></div>
<div class="cl"><span class="av20" style="background:#3b4a40">KI</span><span class="tx"><span class="nm">Kim</span><span class="tm">08:20</span><br>ok, 90 s holds then</span></div>
<div class="cl mention first"><span class="av20" style="background:#3a3f58">AD</span><span class="tx"><span class="nm">Ada</span><span class="tm">09:02</span><br><span class="you">@joi</span> review #3120 when you can{lr}</span></div>
<div class="ob"><b>Open in Slack</b> <kbd>r</kbd><br>Reply there. Release 2 adds the composer here.<br><span class="btn primary">Open in Slack <kbd>r</kbd></span></div></div></aside>'''

def needs_page(name, state='live', w=1440, rail_mode='full', mode='full', cur=1, sel=1, peek=True, extra='', title='Messages - Needs reply', sr=None, banner='', n=22, newband=True, counts=(25, 14)):
    slim = rail_mode == 'slim'
    cls = 'mbody' + (' rail48' if slim else '') + ('' if peek else ' nopeek')
    body = f'<div class="{cls}">{rail(slim=slim, counts=counts)}<div style="min-width:0;display:flex;flex-direction:column;overflow:hidden">{banner}{needs_list(mode, n, cur, newband, sel)}</div>{peek_thread() if peek else ""}</div>'
    sr = sr or 'Needs reply, list, 25 rows, 3 classes shown in release 1: direct question, review ask. Row: Review ask from Ada in harbor, 2 hours ago. Waits on you.'
    write(name, shell(title, hdr(state), body, keys('List keys', LIST_KEYS), sr, extra, w=w, off=(state == 'offline')))
needs_page('needs', sr='Needs reply, list, 25 rows. New, 1: Direct question from Lee in a direct message, 2 minutes ago. Cursor row: Review ask from Ada in harbor, 2 hours ago. Peek open. Open in Slack, Snooze, Done.')

# ---------------- widths, rail collapsed, header states (same page, other state)
needs_page('needs-1200', w=1200, title='Messages - Needs reply at 1200', n=21,
           sr='1,200 px: rail 220, list 540, peek 440. Row facts keep glyph, who, place, title, reason and age.')
needs_page('needs-1000', w=1000, rail_mode='slim', mode='n2', title='Messages - Needs reply at 1000', n=21,
           sr='1,000 px: rail collapsed to 48, list 512, peek 440. The linked-object chips and the reason chip drop first.')
needs_page('needs-rail-collapsed', rail_mode='slim', title='Messages - rail collapsed',
           sr='The rail is collapsed to 48 px from its header button or the palette entry messages.rail. Items keep their names for a screen reader.')
for st, sr in [('behind', 'Header: Behind, history is limited to 15 messages per minute. Rows stay. Conversations show a gap marker.'),
               ('offline', 'Header: Offline, last read 09:41. Every list stays readable from the store. Writes are refused, nothing is queued.')]:
    needs_page('header-' + st, state=st, sr=sr, title='Messages - header ' + st)
def banner(cls, text, btn, det):
    return f'<div class="state-line {cls}" style="height:auto;padding:8px var(--inset)"><span class="g {"fail" if cls == "err" else "parked"}"></span><span class="what" style="white-space:normal">{text}<br><span class="mono" style="font-size:11px;color:var(--ink-subtle)">Details ▸ {det}</span></span><span class="btns">{btn}</span></div>'
needs_page('header-expired', state='expired', title='Messages - token expired',
           banner=banner('err', 'Slack token expired. Reconnect to read new messages. Everything already here stays readable.', '<span class="btn primary">Reconnect <kbd>Enter</kbd></span>', 'SND-MSG-002'),
           sr='Banner: Slack token expired. Button: Reconnect. Details: SND-MSG-002. Lists stay readable and read-only.')
needs_page('header-scope', state='scope', title='Messages - scope missing',
           banner=banner('att', 'Search needs the scope search:read. Reconnect after adding it in the Slack app. Reading and Done still work.', '<span class="btn primary">Reconnect <kbd>Enter</kbd></span>', 'SND-MSG-003'),
           sr='Banner: search needs the scope search:read. The feature is off, not hidden. Button: Reconnect. Details: SND-MSG-003.')

# ---------------- unread (class 1 lives here in Release 1)
UN = [
 (1, 'Pat', 'DM', 'thanks!', [], '', '3h', 'unread'), (1, 'Kim', '#harbor', 'ok, 90 s holds then', [], '', '3h', 'unread men'),
 (1, 'Lee', '#general', '@here lunch Friday?', [], '', '4h', 'unread'), (1, 'Ada', '#harbor', 'staging is on 120 s for now', [], '', '4h', 'unread'),
 (1, 'Mira', '#billing', 'customer says the invoice looks right now', [], '', '5h', ''), (1, 'Jo', '#ferry-ops', 'dock cam is back online', [], '', '6h', ''),
 (1, 'Dani', 'Kim, Lee', 'moving standup to 10:15', [], '', '6h', 'unread'), (1, 'Sana', '#billing', 'rounding thread is resolved, thanks all', [], '', '7h', ''),
 (1, 'Noor', '#harbor', 'export for ACME finished', [], '', '8h', ''), (1, 'Eli', 'DM', 'fyi the retry cap doc is in Notes', [], '', '9h', 'unread'),
]
def unread_list(n=10, cursor=0):
    o = '<div class="mgroup">Unread<span class="n">14</span></div>'
    for i, r in enumerate(UN[:n]):
        row = r[:7]; cl = r[7]
        o += mrow(row, 'full', cursor=(i == cursor), unread=('unread' in cl), cls=('men' if 'men' in cl else '')).replace('<span class="rs">', '<span class="rs">').replace(f'<span class="t">{row[3]}</span>', f'<span class="t">{row[3]}</span>' + ('<span class="dot"></span>' if 'unread' in cl else '') + ('<span class="you">you</span>' if 'men' in cl else ''))
    return f'<div class="mlist">{o}</div>'
write('unread', shell('Messages - Unread', hdr(count='14 unread'), f'<div class="mbody nopeek">{rail(on="Unread")}{unread_list()}</div>', keys('List keys', LIST_KEYS + [('n', 'next unread')][:0]),
      'Unread, list, 14 rows. Release 1 shows class 1 threads here: circle glyph. Bold name plus a dot means unread; the word "you" and a bar mean a mention.'))

# ---------------- conversation
def ln(name, tm, body, first=True, av='#3a3f58', ini='AD', cur=False, mention=False, extra='', hov=''):
    cls = 'cl' + (' first' if first else '') + (' cur' if cur else '') + (' mention' if mention else '')
    if first:
        return f'<div class="{cls}"><span class="av20" style="background:{av}">{ini}</span><span class="tx"><span class="nm">{name}</span><span class="tm">{tm}</span><br>{body}{extra}</span></div>'
    return f'<div class="{cls}"><span class="tm h" style="position:static;text-align:right;align-self:center;opacity:{1 if hov else 0}">{tm if hov else ""}</span><span class="tx">{body}{extra}</span></div>'
def blk(lines, cp=True): return '<div class="blk">' + ('<span class="cp">copy <kbd>c</kbd></span>' if cp else '') + ''.join(f'<div class="ln {c}">{t}</div>' for c, t in lines) + '</div>'
def lrow(gl, i, t, r): return f'<div class="lrow"><span class="g {gl}"></span><span class="id">{i}</span><span class="t">{t}</span><span class="r">{r}</span></div>'
RX = lambda *a: '<div class="rx">' + ''.join(f'<span class="{c}">{e} {n}</span>' for e, n, c in a) + '</div>'
def chan_lines(cur=None, hov=False):
    o = '<div class="gapm"><span>12 older messages not loaded.</span><span class="btn">Load older</span><span class="cd">next in 41 s</span><span class="sp"></span><span class="cd">Behind: history is limited</span></div>'
    o += '<div class="dday"><span>Today</span></div>'
    o += ln('Ada', '08:41', 'morning. seat holds are still <code>120</code> s in staging', cur=(cur == 'a'))
    o += ln('', '08:42', 'I will move it to 90 s once <span class="lk">#3120</span> lands', first=False, hov=hov)
    o += ln('Kim', '08:55', 'ok, 90 s holds then', av='#3b4a40', ini='KI', cur=(cur == 'k'), extra=RX(('✓', 3, 'me'), ('♥', 2, '')))
    o += ln('Sam', '09:01', 'config for staging:', av='#4a3b3b', ini='SA', extra=blk([('', 'seat_hold_seconds: 90'), ('', 'hold_release: on_timeout')]))
    o += ln('Pat', '09:01', 'fare table change, one line each way:', av='#3b3f4a', ini='PA', extra=blk([('h', 'diff --git a/fares.yaml b/fares.yaml'), ('d', '- base: 14.50'), ('a', '+ base: 14.80')]))
    o += ln('Ada', '09:02', '<span class="you">@joi</span> review #3120 when you can', cur=(cur == 'm'), mention=True, extra=lrow('pr', '#3120', 'Seat holds: 90 s', '<b>CI green</b> · asked of you · ada'))
    o += '<div class="thr"><span class="avs"><i></i><i></i><i></i></span><span>Ada, Sam and 2 more · 6 replies · last 09:02</span></div>'
    o += '<div class="ndiv">New</div>'
    o += ln('Lee', '09:12', 'anyone around for a quick look at the fare table?', av='#3b4a40', ini='LE')
    return o
def conv_page(name, cur=None, hov=False, mode='Message keys', keys_=MSG_KEYS, title='Messages - #harbor', sr='', rail_on='harbor', focus=None, extra=''):
    ch = '<div class="chead"><b># harbor</b><span class="sub">412 people · starred</span></div>'
    body = f'<div class="mbody">{rail(on=rail_on, focus=focus)}<section class="conv">{ch}<div class="clines">{chan_lines(cur, hov)}</div><span class="newchip">3 new ↓</span></section>{peek_thread()}</div>'
    write(name, shell(title, hdr(count='25 need reply'), body, keys(mode, keys_), sr, extra))
conv_page('conversation', hov=True, sr='Conversation, harbor, a log. Ada, 09:02: review 3120 when you can. New messages: 1 below the divider. Gap: 12 older messages not loaded, Load older. Message keys.')
conv_page('conversation-focus', cur='m', rail_on='harbor', focus='harbor', title='Messages - focus rings',
          sr='Focus ring on the message line: Ada, 09:02, mention. Message keys: reading harbor. The rail item keeps its own ring when the rail has focus.')
# DM, with link rows and links that stay plain text
def dm_lines():
    o = '<div class="dday"><span>Today</span></div>'
    o += ln('Sam', '08:48', 'the Linear board for the fare table is here: https://linear.app/private-team/issue/PRV-12', av='#4a3b3b', ini='SA')
    rows = lrow('pr', '#3120', 'Seat holds: 90 s', '<b>CI green</b> · asked of you · ada') + lrow('doing', '#3055', 'Seat hold release', 'In Progress · you') + lrow('merged', '#3104', 'fix: retry export upload', 'merged · 2h') + '<div class="lmore">+3 links</div>'
    o += ln('Sam', '09:00', 'and the PR and two follow-ups: https://github.com/jthorare/agentic-workflow/pull/3120 and more', first=False, hov=True, extra=rows)
    o += '<div class="ndiv">New</div>'
    o += ln('Sam', '09:02', 'can you ship the fare table today?', av='#4a3b3b', ini='SA')
    return o
dm_peek = peek_thread().replace('Thread · #harbor', 'Sam · DM').replace('slack:thread:harbor', 'slack:dm:sam').replace('Review ask', 'Direct question').replace('g c2', 'g c3')
write('conversation-dm', shell('Messages - DM with Sam', hdr(), f'<div class="mbody">{rail(on="Sam")}<section class="conv"><div class="chead"><b>Sam</b><span class="sub">direct message</span></div><div class="clines">{dm_lines()}</div></section>{dm_peek}</div>',
      keys('Message keys', MSG_KEYS), 'Conversation, direct message with Sam, a log. Three live rows: pull request 3120, CI green; issue 3055, In Progress; pull request 3104, merged. A Linear link stays plain text.'))

# ---------------- snooze (Home's picker as it is in Release 1; the checkbox is Release 2)
SV = lambda p: f'<svg viewBox="0 0 16 16">{p}</svg>'
CLOCK = '<circle cx="8" cy="8" r="5.5"/><path d="M8 5v3.2l2 1.3"/>'
CAL = '<rect x="2.5" y="3.5" width="11" height="10" rx="1.5"/><path d="M2.5 6.5h11M5.5 2v3M10.5 2v3"/>'
def snooze_menu(top, follow=False):
    o = f'<div class="menu" style="left:520px;top:{top}px;width:300px"><div class="mhead">Snooze until</div>'
    o += '<div class="mfield"><span class="ph">Type a date: fri 14:00, nov 20, in 2h</span></div>'
    for lab, w, k, on in [('Later today', '17:00', '1', 1), ('Tomorrow', 'Fri 9:00', '2', 0), ('Monday', 'Mon 9:00', '3', 0), ('Next week', 'Thu 9:00', '4', 0)]:
        o += f'<div class="ment{" on" if on else ""}">{SV(CLOCK)}<span class="lab">{lab}</span><span class="when">{w}</span><kbd>{k}</kbd></div>'
    o += f'<div class="mdiv"></div><div class="ment">{SV(CAL)}<span class="lab">Pick a date or time</span><kbd>5</kbd></div>'
    if follow:
        o += '<div class="mdiv"></div><div class="ment chk"><span class="g box on"></span><span class="lab">Follow up if no reply</span><kbd>f</kbd></div><div class="mnote">I spoke last in this thread. If nobody answers before it is due it returns, with "no reply since Tue". An answer drops the snooze.</div>'
    return o + '</div>'
SNKEYS = [('1-5', 'pick'), ('↑↓', 'move'), ('Enter', 'snooze'), ('Esc', 'close')]
def snooze_page(name, follow, sr, title):
    body = f'<div class="mbody">{rail()}<div style="min-width:0;overflow:hidden">{needs_list("full", 8, 2, False, 2).replace("actions", "actions")}</div>{peek_thread()}</div>'
    write(name, shell(title, hdr(), body, keys('Picker', SNKEYS + ([('f', 'follow up if no reply')] if follow else [])), sr, snooze_menu(222, follow)))
snooze_page('snooze-picker', False, 'Snooze picker: later today, tomorrow, Monday, next week, a typed time. Release 1 uses Home’s picker as it is: no follow-up checkbox.', 'Messages - snooze picker')
snooze_page('snooze-picker-followup', True, 'Snooze picker with the checkbox Follow up if no reply, checked, key f. Release 2: it needs the thread to have my last message.', 'Messages - snooze picker with follow up')
# snoozed rows, "back Mon 9:00"
SNZ = [(3, 'Mira', 'DM', 'did the export retry change land? asking before I tell the customer', [], 'back Mon 9:00', '2h'), (2, 'Tomas', '#billing', 'review please: proration preview rounds half cents the wrong way', ['#3098'], 'back early: new reply', '3h')]
def snz_rows():
    o = '<div class="mgroup">Needs reply<span class="n">22</span></div>'
    for i, r in enumerate(ROWS[:6]): o += mrow(r, 'full', cursor=(i == 0), sel=(i == 0), actions=(i == 0))
    o += '<div class="mgroup">Snoozed<span class="n">2</span></div>'
    for r in SNZ:
        o += mrow(r, 'full', cls='dimr').replace(f'<span class="rs">{r[5]}</span>', f'<span class="rs" style="color:var(--ink-muted)">{r[5]}</span>')
    return f'<div class="mlist">{o}</div>'
write('snoozed-rows', shell('Messages - snoozed', hdr(count='22 need reply'), f'<div class="mbody nopeek">{rail(counts=(22, 14))}{snz_rows()}</div>', keys('List keys', LIST_KEYS),
      'Snoozed, 2 rows. Mira, direct message: back Monday 9:00. Tomas, harbor: back early, new reply.'))

# ---------------- first run (6.8)
def stp(n, title, status, body='', cur=False, done=False, todo=False):
    num = '<span class="g done"></span>' if done else f'<span class="num{" cur" if cur else ""}">{n}</span>'
    return f'<div class="stp {"todo" if todo else ""}"><div class="sh">{num}<span>{title}</span><span class="sp"></span><span class="st">{status}</span></div>{("<div class=sb>" + body + "</div>") if body else ""}</div>'
MANIFEST = 'display_information:\n  name: Sindri Messages\noauth_config:\n  scopes:\n    user:\n      - channels:history\n      - groups:history\n      - im:history\n      - mpim:history\n      - channels:read\n      - groups:read\n      - im:read\n      - mpim:read\n      - users:read\n      - emoji:read\n      - search:read\nsettings:\n  token_rotation_enabled: false'
def fail(text, det, cls=''):
    return f'<div class="fbox {cls}"><span class="g {"parked" if cls else "fail"}"></span><div class="w">{text}<div class="dt">Details ▸ {det}</div></div></div>'
def card(name, steps, sr, lead="Messages reads your Slack. It needs a token that only you can make.", title='Messages - connect Slack', off=False, kb=None):
    body = f'<div class="mbody norail"><div class="card"><h1>Connect Slack</h1><p class="lead">{lead}</p>{steps}</div></div>'
    write(name, shell(title, hdr(count='', hint=False).replace('<span class="stat ">Live (polling every 30 s) · joi</span>', '<span class="stat off">Not connected</span>'), body, kb or keys('Setup', [('Tab', 'next field'), ('Enter', 'continue'), ('Esc', 'back')]), sr, off=off))
S1_BODY = '<p>Open Slack’s create-app page with the manifest filled in. Name it anything, then press Create.</p><div class="row2"><span class="btn primary">Open Slack’s create-app page <kbd>Enter</kbd></span><span class="btn">Copy manifest</span></div>'
card('firstrun-1', stp(1, 'Create the app', 'Step 1 of 3', S1_BODY, cur=True) + stp(2, 'Install it', '', todo=True) + stp(3, 'Paste the token', '', todo=True),
     'Connect Slack, step 1 of 3: Create the app. Button: Open Slack’s create-app page. Steps 2 and 3 are not open yet.')
card('firstrun-1-fail', stp(1, 'Create the app', 'Step 1 of 3', fail('The link didn’t open Slack’s page. Make the app by hand: open api.slack.com/apps, choose Create New App, From a manifest, and paste this text.', 'SND-MSG-029') + f'<div class="mf">{MANIFEST}</div><div class="row2"><span class="btn primary">Copy manifest <kbd>Enter</kbd></span><span class="btn">Try the link again</span></div>', cur=True) + stp(2, 'Install it', '', todo=True) + stp(3, 'Paste the token', '', todo=True),
     'Step 1 failed: the link did not open Slack. The manifest and the steps by hand are shown. Button: Copy manifest.')
S2_BODY = '<p>In the app’s page choose Install to Workspace and allow the read scopes. Then come back here.</p><div class="row2"><span class="btn primary">I installed it <kbd>Enter</kbd></span><span class="btn">Back</span></div>'
card('firstrun-2', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Step 2 of 3', S2_BODY, cur=True) + stp(3, 'Paste the token', '', todo=True),
     'Connect Slack, step 2 of 3: Install it. Step 1 is done. Button: I installed it.')
REQ = 'Hi, I would like to add a read-only Slack app (Sindri Messages) to our workspace for my own use. It asks for user scopes only: read channels, DMs and group DMs, read people and emoji, and search. It cannot post or change anything. Could you approve it? Thanks.'
card('firstrun-2-blocked', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Blocked', fail('Your workspace needs an admin to approve this app. No Messages page can exist until they do.', 'SND-MSG-019 · Slack says: app_approval_required', 'att') + f'<p>Send this to your Slack admin:</p><div class="mf" style="white-space:normal;width:560px;line-height:18px">{REQ}</div><div class="row2"><span class="btn primary">Copy request <kbd>Enter</kbd></span><span class="btn">Check again</span></div>', cur=True) + stp(3, 'Paste the token', '', todo=True),
     'Step 2 is blocked: the workspace needs an admin to approve the app. Copyable request text for the admin. Details: SND-MSG-019. Buttons: Copy request, Check again.')
FIELD = lambda t, f=True: f'<div class="field {"focus" if f else ""}">{t}<span class="caret"></span></div>'
S3_BODY = '<p>Open OAuth &amp; Permissions in the app’s page, copy the User OAuth Token (starts with xoxp-), and paste it here. It is never shown again and Sindri clears the clipboard after the paste, if it can.</p>'
card('firstrun-3', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div class="okline"><span class="g done"></span>User token. All 11 read scopes are present.</div><div class="row2" style="margin-top:10px"><span class="btn primary">Connect <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 of 3: Paste the token. The field is write-only and shows dots. Detected: user token, all 11 read scopes present. Button: Connect.')
card('firstrun-3-wrong-token', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('That is a bot token (xoxb-). Messages needs the User OAuth Token (xoxp-) from OAuth &amp; Permissions, User Token section.', 'SND-MSG-030') + '<div class="row2"><span class="btn primary">Paste another token <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 failed: the pasted token is a bot token. Messages needs the user token that starts with xoxp-. Details: SND-MSG-030. The token was not sent anywhere.')
card('firstrun-3-scope', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('The token lacks the scope search:read. Add it in the Slack app, reinstall, then paste the new token.', 'SND-MSG-003', 'att') + '<div class="row2"><span class="btn primary">Paste another token <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 failed: the token lacks the scope search:read. Details: SND-MSG-003. Button: Paste another token.')
card('firstrun-3-offline', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('Offline: connect when you’re back. Nothing is stored. The token stays in this box for this session.', 'SND-MSG-028', 'att') + '<div class="row2"><span class="btn primary off">Connect <kbd>Enter</kbd></span><span style="font-size:12px;color:var(--ink-subtle)">Connect turns on when the network is back</span></div>', cur=True),
     'Step 3, offline: Offline, connect when you are back. Nothing is stored. The token field keeps its text for this session. Connect is off. Details: SND-MSG-028.', off=True)

# ---------------- first sync and listening
def syncpage(name, lines, sr, state='live', title=''):
    body = f'<div class="mbody">{rail(counts=(0, 0), starred=False)}<div class="mlist"><div class="mid" style="padding-top:48px">{lines}</div></div></div>'
    write(name, shell(title or 'Messages - ' + name, hdr(state, count='0 need reply'), body, keys('List keys', [('Ctrl+K', 'search or jump'), ('?', 'all keys')]), sr))
syncpage('firstrun-sync', '<div class="g agent" style="margin-bottom:10px"></div><h2>Connected. Reading your Slack.</h2><p>40 conversations, 0 messages so far.</p><p style="color:var(--ink-subtle);font-size:12px">Your DMs, mentions and starred channels load first. Needs reply fills in as messages arrive.</p>',
         'First sync: 40 conversations, 0 messages so far. Reading your Slack.', title='Messages - first sync')
syncpage('firstrun-listening', '<span class="g done" style="margin-bottom:10px;display:block"></span><h2>Listening.</h2><p>New messages appear here. Open a conversation to load its history (1 per minute).</p><p style="color:var(--ink-subtle);font-size:12px">The first day has no history for Needs reply yet. Sindri is polling your hot conversations every 30 s.</p>',
         'Listening. New messages appear here. Open a conversation to load its history, one per minute.', title='Messages - listening')

# ---------------- states
def emptypage(name, on, h, p, sr, rail_kw=None, count='0 need reply', counts=(0, 14)):
    body = f'<div class="mbody nopeek">{rail(on=on, counts=counts, **(rail_kw or {}))}<div class="mlist"><div class="mid"><h2>{h}</h2><p>{p}</p></div></div></div>'
    write(name, shell('Messages - ' + name, hdr(count=count), body, keys('List keys', LIST_KEYS), sr))
emptypage('empty-needs', 'Needs reply', 'You’re caught up.', 'The next snoozed thread is back Mon 9:00.', 'Needs reply is empty: You are caught up. The next snoozed thread is back Monday 9:00. The rail shows no starred channels yet.', {'starred': False})
emptypage('empty-unread', 'Unread', 'Nothing unread.', 'New messages in your DMs, mentions and starred channels show here.', 'Unread is empty: Nothing unread.', count='0 unread', counts=(3, 0))
sk = ''.join(f'<div class="sk"><span class="g c3" style="opacity:.3"></span><i style="width:{a}px"></i><i style="width:{b}px"></i><i style="width:{c}px;background:var(--s2)"></i><i style="width:24px"></i></div>' for a, b, c in [(60, 40, 320), (50, 60, 280), (70, 50, 360), (56, 44, 240), (62, 58, 300), (48, 40, 340)])
write('loading', shell('Messages - loading', hdr(count='…'), f'<div class="mbody nopeek">{rail()}<div class="mlist"><div class="mgroup">Needs reply<span class="n"></span></div>{sk}</div></div>', keys('List keys', LIST_KEYS),
      'Loading: 6 skeleton rows painted from the cache. Rows fill in from the store.'))
# toast, sender and place only
TOAST = '<div class="toast"><span class="g c3"></span><div><b style="font-weight:600">Sam, DM</b><div style="font-size:12px;color:var(--ink-subtle)">Needs reply</div></div><span class="btn" style="margin-left:auto">Open <kbd>Enter</kbd></span></div>'
OSN = '<div class="os"><div class="h">Sindri</div><div><b style="font-weight:600">Sam, DM</b></div><div style="font-size:12px;color:var(--ink-subtle)">1 thread waits on you</div></div>'
needs_page('toast', title='Messages - toast', extra=OSN + TOAST, sr='Notification: Sam, DM. The text of the message is not read out or shown. Button: Open.')
# footer modes
def fm(mode, items, cap): return f'<p class="cap2">{cap}</p><div class="fm"><div class="keys">{keys(mode, items)}</div></div>'
write('footer-modes', shell('Messages - footer modes', hdr(), f'<div class="mbody norail"><div class="footmodes">{fm("List keys", LIST_KEYS, "Cursor on a list row: the footer chip reads List keys.")}{fm("Message keys", MSG_KEYS, "Cursor on a message line: the footer chip reads Message keys. A screen reader announces the change: “Message keys: reading harbor”.")}<p class="cap2">In Release 1 the footer shows only keys that work: r reads “open in Slack” and m (make issue) is absent.</p></div></div>', keys('List keys', LIST_KEYS),
      'The footer chip changes between List keys and Message keys. A screen reader announces the mode: Message keys, reading harbor.'))
# hover and pressed
def sheet_html():
    rr = lambda cls: mrow(ROWS[0], 'full', cls=cls)
    h = '<div class="sheet"><h2>Row</h2>'
    for cap, cls in [('Rest', ''), ('Hover', 'hv'), ('Pressed', 'press'), ('Cursor', 'cursor')]:
        h += f'<div class="cell"><span class="cap">{cap}</span><div style="flex:1;margin:0 -24px">{rr(cls)}</div></div>'
    h += '<h2>Chip</h2>'
    for cap, cls in [('Rest', ''), ('Hover', 'hov'), ('Pressed', 'prs')]:
        h += f'<div class="cell"><span class="cap">{cap}</span><span class="rx" style="margin:0"><span class="{cls}">✓ 3</span></span><span class="lc {"hov" if cls=="hov" else ""}" style="font:400 11px/16px var(--mono);border:1px solid var(--hair-strong);border-radius:4px;padding:0 5px;color:var(--ink-subtle)">#3120</span></div>'
    h += '<h2>Button</h2>'
    for cap, cls in [('Rest', ''), ('Hover', 'hov'), ('Pressed', 'prs'), ('Primary', 'primary')]:
        h += f'<div class="cell"><span class="cap">{cap}</span><span class="btn {cls}">Open in Slack <kbd>r</kbd></span></div>'
    return h + '</div>'
write('states-hover-pressed', shell('Messages - hover and pressed', hdr(), f'<div class="mbody norail">{sheet_html()}</div>', keys('List keys', LIST_KEYS), 'Hover and pressed states for a row, a chip and a button. State is never colour alone.'))
