import pathlib
D = pathlib.Path(__file__).parent
G = '../grammar-2978/grammar.css'
FRAMES = []
def write(n, s): (D / f'messages-{n}.html').write_text(s); FRAMES.append(n)

def nav(n=23):
    m = f'<span class="n">{n}</span>' if n is not None else ''
    return ('<a>Home<span class="n">9</span></a><a>Reviews<span class="n">4</span></a><a class="on">Messages' + m + '</a><a>Items<span class="n">31</span></a><a>Workflows</a>'
            '<a>Agents<span class="n">3</span></a><a>Projects</a><a>Notes</a><a>Context</a>')
LIST_KEYS = [('j/k', 'move'), ('Space', 'peek'), ('Enter', 'open'), ('r', 'open in Slack'), ('e', 'done'), ('h', 'snooze'), ('/', 'filter'), ('?', 'all keys')]
MSG_KEYS = [('j/k', 'move'), ('Enter', 'open thread'), ('r', 'open in Slack'), ('e', 'done'), ('h', 'snooze'), ('n', 'next unread'), ('Home/End', 'top, bottom'), ('Esc', 'back to list'), ('?', 'all keys')]
def kk(items): return ''.join(f'<span><kbd>{k}</kbd>{l}</span>' for k, l in items)
MODES = ('List keys', 'Message keys', 'Composer')
def keys(mode, items): return (f'<span><span class="mode">{mode}</span></span>' if mode in MODES else '') + kk(items)

def shell(title, header, body, kbar, sr, extra='', w=1440, pill='Connected', off=False, ncount=23):
    st = f'body{{width:{w}px}}' + ('.cmdk{width:150px;white-space:nowrap;overflow:hidden}.cmdk kbd{display:none}' if w < 1200 else '')
    return f'''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>{title}</title>
<link rel="stylesheet" href="{G}"><link rel="stylesheet" href="messages.css"><style>{st}</style></head>
<body><div class="app">
<div class="topbar"><span class="wordmark">Sindri</span><nav class="nav">{nav(ncount)}</nav><span class="spacer"></span><div class="cmdk">{'Search' if w < 1100 else 'Search or run a command'}<kbd>Ctrl+K</kbd></div><span class="status-pill{' off' if off else ''}">{pill}</span></div>
{header}
{body}
<div class="foot"><div class="keys">{kbar}</div><div class="sr"><b>Screen reader</b>{sr}</div></div>
{extra}</div></body></html>
'''
STATES = {
 'live': ('', 'Live (polling every 30 s) · joi', ''),
 'behind': ('att', 'Behind: 12 min · history is limited to 15 messages per minute · joi', ''),
 'offline': ('off', 'Offline: last read 09:10', ''),
 'expired': ('err', 'Slack token expired', ''),
 'scope': ('att', 'Needs scope search:read', ''),
}
def hdr(state='live', title='Messages', count='23 need reply', hint=True):
    c, t, b = STATES[state]
    h = '<span class="hint">Ctrl+K search or jump</span>' if hint else ''
    return f'<div class="header"><h1>{title}</h1><span class="count">{count}</span>{h}<span class="stat {c}">{t}{b}</span></div>'

# ---------------- rail
def rail(on='Needs reply', slim=False, starred=True, counts=(23, 14), later=None, drafts=None, focus=None, harbor=3):
    def it(name, n, ic):
        cls = 'on' if on == name else ''
        if focus == name: cls += ' focus'
        return f'<div class="it {cls}"><span class="ic">{ic}</span><span class="lbl">{name}</span><span class="n">{n}</span></div>'
    o = f'<aside class="rail{" slim" if slim else ""}">' + it('Needs reply', counts[0], '<b class="l">N</b>') + it('Unread', counts[1], '<b class="l">U</b>')
    if later is not None: o += it('Later', later, '<b class="l">L</b>')
    if drafts is not None: o += it('Drafts', drafts, '<b class="l">D</b>')
    o += '<div class="sec">Starred</div>'
    if starred:
        for nm, n, h in [('harbor', harbor, '#'), ('ferry-ops', '', '#'), ('Sam', '', 'S'), ('Kim, Lee', '', 'K')]:
            c = 'on' if on == nm else ''
            o += f'<div class="it {c}"><span class="ic hsh">{h}</span><span class="lbl">{nm}</span><span class="n">{n}</span></div>'
    else:
        o += '<div class="emp">No starred channels yet. Star it from the <kbd>.</kbd> panel, or find it with <kbd>Ctrl+K</kbd>.</div>'
    o += '<div class="foot2"><kbd>Ctrl+K</kbd><span class="lbl">all channels</span></div></aside>'
    return o

# ---------------- needs reply rows
CH = lambda t: f'<span class="lc">{t}</span>'
ROWS0 = [  # cls, who, place, title, chips, reason, age
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
 (3, 'Noor', '#billing', '@joi could you re-run the export for ACME?', [], 'mention', '8h'),
 (2, 'Ada', '#harbor', 'also #3125 is stacked on 3120, can you look at both?', ['#3125'], 'review ask', '8h'),
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
# spec 7.2: class high first, then blocks (Home 4.1), then the newest message, oldest first. Blocks here: Ada's #3120 thread 2, Ada's stacked #3125 and Tomas 1, the rest 0.
BLOCKS = {('Ada', '2h'): 2, ('Ada', '8h'): 1, ('Tomas', '3h'): 1}
def _mins(a): return int(a[:-1]) * {'m': 1, 'h': 60, 'd': 1440}[a[-1]]
ROWS = sorted(ROWS0, key=lambda r: (-r[0], -BLOCKS.get((r[1], r[6]), 0), -_mins(r[6])))
ADA = next(i for i, r in enumerate(ROWS) if r[1] == 'Ada' and r[6] == '2h')
NEW_ROW = (3, 'Lee', 'DM', 'got a sec?', [], 'DM', '2m')
KIND = {3: 'Direct question', 2: 'Review ask', 1: 'FYI'}
def _ago(a): n, u = int(a[:-1]), {'m': 'minute', 'h': 'hour', 'd': 'day'}[a[-1]]; return f'{n} {u}{"s" if n != 1 else ""} ago'
def rowsr(r, extra=''):
    c, who, pl, ti, chips, rs, age = r
    place = 'a direct message' if pl == 'DM' else 'harbor' if pl == '#harbor' else pl.lstrip('#')
    return f'{KIND[c]} from {who} in {place}, {_ago(age)}: {ti.replace("#", "")}{extra}'
A1 = ('Open in Slack', 'r')
def mrow(r, mode='full', cursor=False, sel=False, hv=False, unread=False, cls='', actions=False, compact=False):
    c, who, pl, ti, chips, rs, age = r
    chips_h = ''.join(CH(x) for x in chips) if (mode == 'full' and not compact) else ''
    k = f'mrow {"hasact" if actions else ""} {"narrow" if mode != "full" else ""} {"n2" if mode == "n2" else ""} {"cmp" if compact else ""} {"cursor" if cursor else ""} {"sel" if sel else ""} {"hv" if hv else ""} {"unread" if unread else ""} {cls}'
    cmp_ = ' compact' if (mode != 'full' or compact) else ''
    acts = (f'<div class="actions{cmp_}"><span class="btn" title="{A1[0]}"><span class="lbl">{A1[0]} </span><kbd>{A1[1]}</kbd></span><span class="btn" title="Snooze"><span class="lbl">Snooze </span><kbd>h</kbd></span><span class="btn" title="Done"><span class="lbl">Done </span><kbd>e</kbd></span></div>') if actions else ''
    return f'<div class="{k}"><span class="g c{c}"></span><span class="who">{who}</span><span class="pl">{pl}</span><span class="ti"><span class="t">{ti}</span>{chips_h}</span><span class="rs">{rs}</span><span class="age">{age}</span>{acts}</div>'
def needs_list(mode='full', n=22, cur=None, newband=True, sel=None, rows=None, total=22, ncount=1, compact=False, cm=False, dim=()):
    cur = ADA if cur is None else cur
    sel = ADA if sel is None else sel
    rows = ROWS if rows is None else rows
    o = ''
    if newband:
        o += f'<div class="mgroup new">New<span class="n">{ncount}</span></div><div class="newband">' + mrow(NEW_ROW, mode) + '</div>'
    o += f'<div class="mgroup">Needs reply<span class="n">{total}</span></div>'
    for i, r in enumerate(rows[:n]):
        o += mrow(r, mode, cursor=(i == cur), sel=(i == sel), actions=(i == cur and not cm), compact=compact, cls=('cm ' if (cm and i == cur) else '') + ('dimr' if i in dim else ''))
    return f'<div class="mlist">{o}</div>'

# ---------------- peek
def peek_thread(release=1):
    lr = '<div class="lrow"><span class="g pr"></span><span class="id">#3120</span><span class="t">Seat holds: 90 s</span><span class="r"><b>CI green</b> · asked of you · ada</span></div>'
    return f'''<aside class="peek"><div class="peek-head"><h2>Thread · #harbor</h2><span class="id">slack:thread:harbor</span><span class="icon">⤢</span><span class="icon">✕</span></div>
<dl class="peek-fields"><dt>Class</dt><dd><span class="g c2"></span>Review ask</dd><dt>Waits on</dt><dd>you</dd><dt>Linked</dt><dd><span class="g merged"></span>#3120 · #3055 · #3125</dd></dl>
<div class="mp"><div class="eyebrow">Thread, root only</div>
<div class="cl mention first"><span class="av20" style="background:#3a3f58">AD</span><span class="tx"><span class="nm">Ada</span><span class="tm">07:30</span><br><span class="you">@joi</span> review #3120 when you can{lr}</span></div>
<div class="ob"><b>Open in Slack</b> <kbd>r</kbd><br>Reply there. Release 2 adds the composer here.<br><span class="btn primary">Open in Slack <kbd>r</kbd></span></div></div></aside>'''
def peek_dm():
    lr = '<div class="lrow"><span class="g pr"></span><span class="id">#3120</span><span class="t">Seat holds: 90 s</span><span class="r"><b>CI green</b> · asked of you · ada</span></div>'
    return f'''<aside class="peek"><div class="peek-head"><h2>Sam · DM</h2><span class="id">slack:dm:sam</span><span class="icon">⤢</span><span class="icon">✕</span></div>
<dl class="peek-fields"><dt>Class</dt><dd><span class="g c3"></span>Direct question</dd><dt>Waits on</dt><dd>you</dd><dt>Linked</dt><dd><span class="g merged"></span>#3120 · #3055 · #3104</dd></dl>
<div class="mp"><div class="eyebrow">Direct message, 3 messages today</div>
<div class="cl first"><span class="av20" style="background:#4a3b3b">SA</span><span class="tx"><span class="nm">Sam</span><span class="tm">08:48</span><br>the Linear board for the fare table is here: https://linear.app/private-team/issue/PRV-12</span></div>
<div class="cl first"><span class="av20" style="background:#4a3b3b">SA</span><span class="tx"><span class="nm">Sam</span><span class="tm">09:00</span><br>and the PR and two follow-ups{lr}</span></div>
<div class="cl first"><span class="av20" style="background:#4a3b3b">SA</span><span class="tx"><span class="nm">Sam</span><span class="tm">09:02</span><br>can you ship the fare table today?</span></div>
<div class="ob"><b>Open in Slack</b> <kbd>r</kbd><br>Reply there. Release 2 adds the composer here.<br><span class="btn primary">Open in Slack <kbd>r</kbd></span></div></div></aside>'''

def needs_page(name, state='live', w=1440, rail_mode='full', mode='full', cur=None, sel=None, peek=True, extra='', title='Messages - Needs reply', sr=None, banner='', n=22, newband=True, counts=(23, 14), count='23 need reply', ncount=23):
    slim = rail_mode == 'slim'
    cls = 'mbody' + (' rail48' if slim else '') + ('' if peek else ' nopeek')
    body = f'<div class="{cls}">{rail(slim=slim, counts=counts)}<div style="min-width:0;display:flex;flex-direction:column;overflow:hidden">{banner}{needs_list(mode, n, cur, newband, sel, compact=(w < 1440))}</div>{peek_thread() if peek else ""}</div>'
    sr = sr or ('Needs reply, list, 23 rows. Cursor row: ' + rowsr(ROWS[ADA]) + '. Waits on you.')
    write(name, shell(title, hdr(state, count=count), body, keys('List keys', LIST_KEYS), sr, extra, w=w, off=(state == 'offline'), ncount=ncount))
needs_page('needs', sr='Needs reply, list, 23 rows. New, 1: ' + rowsr(NEW_ROW) + '. Cursor row: ' + rowsr(ROWS[ADA]) + '. Peek open. Open in Slack, Snooze, Done.')

# ---------------- widths, rail collapsed, header states (same page, other state)
needs_page('needs-1200', w=1200, title='Messages - Needs reply at 1200', n=21,
           sr='1,200 px: rail 220, list 540, peek 440. The cursor row drops its chips and reason, its buttons shrink to key chips and never cover the age. Cursor row: ' + rowsr(ROWS[ADA]) + '.')
needs_page('needs-1000', w=1000, rail_mode='slim', mode='n2', title='Messages - Needs reply at 1000', n=21,
           sr='1,000 px: rail collapsed to 48, list 512, peek 440. The linked-object chips and the reason chip drop first; the cursor row buttons shrink to key chips and the title keeps over 120 px. Cursor row: ' + rowsr(ROWS[ADA]) + '.')
needs_page('needs-rail-collapsed', rail_mode='slim', title='Messages - rail collapsed',
           sr='The rail is collapsed to 48 px from its header button or the palette entry messages.rail. Items keep their names for a screen reader. Cursor row: ' + rowsr(ROWS[ADA]) + '.')
for st, sr in [('behind', 'Header: Behind 12 minutes, history is limited to 15 messages per minute. Rows stay. Conversations show a gap marker.'),
               ('offline', 'Header: Offline, last read 09:10. Every list stays readable from the store. Writes are refused, nothing is queued.')]:
    needs_page('header-' + st, state=st, sr=sr, title='Messages - header ' + st)
def banner(cls, text, btn, det):
    return f'<div class="state-line {cls}" style="height:auto;padding:8px var(--inset)"><span class="g {"fail" if cls == "err" else "parked"}"></span><span class="what" style="white-space:normal">{text}<br><span class="mono" style="font-size:11px;color:var(--ink-subtle)">Details ▸ {det}</span></span><span class="btns">{btn}</span></div>'
needs_page('header-expired', state='expired', title='Messages - token expired',
           banner=banner('err', 'Slack token expired. Reconnect to read new messages. Everything already here stays readable.', '<span class="btn primary">Reconnect</span>', 'SND-MSG-002'),
           sr='Banner: Slack token expired. Button: Reconnect. Details: SND-MSG-002. Lists stay readable and read-only.')
needs_page('header-scope', state='scope', title='Messages - scope missing',
           banner=banner('att', 'Search needs the scope search:read. Reconnect after adding it in the Slack app. Reading and Done still work.', '<span class="btn primary">Reconnect</span>', 'SND-MSG-003'),
           sr='Banner: search needs the scope search:read. The feature is off, not hidden. Button: Reconnect. Details: SND-MSG-003.')

# ---------------- unread (class 1 lives here in Release 1)
UN = [  # cls (1 = a thread of mine, 0 = not mine: no glyph), who, place, title, mention, age (newest first; clock about 09:10)
 (0, 'Lee', '#harbor', 'thanks, taking a look now', 0, 'now'), (1, 'Kim', '#harbor', 'ok, 90 s holds then', 1, '50m'),
 (0, 'Mira', '#billing', 'customer says the invoice looks right now', 0, '1h'), (0, 'Jo', '#ferry-ops', 'dock cam is back online', 0, '2h'),
 (1, 'Pat', 'DM', 'thanks!', 0, '3h'), (0, 'Lee', '#general', '@here lunch Friday?', 0, '4h'),
 (0, 'Lee', 'Kim, Lee', 'moving standup to 10:15', 0, '5h'), (0, 'Sana', '#billing', 'rounding thread is resolved, thanks all', 0, '6h'),
 (0, 'Rui', '#ops', 'export for ACME finished', 0, '7h'), (0, 'Eli', 'DM', 'fyi the retry cap doc is in Notes', 0, '8h'),
 (0, 'Hana', '#general', 'standup notes are in the doc', 0, '9h'), (0, 'Ben', '#ferry-ops', 'dock inspection moved to Thursday', 0, '10h'),
 (0, 'Uma', '#billing', 'invoice template v2 is merged', 0, '11h'), (0, 'Fay', '#eng', 'on-call swap sheet is updated', 0, '12h'),
]
def unread_list(n=14, cursor=0):
    o = '<div class="mgroup">Unread<span class="n">14</span></div>'
    for i, (c, who, pl, ti, men, age) in enumerate(UN[:n]):
        row = (c, who, pl, ti, [], '', age)
        h = mrow(row, 'full', cursor=(i == cursor), unread=True, cls=('men' if men else ''))
        h = h.replace(f'<span class="t">{ti}</span>', f'<span class="t">{ti}</span><span class="dot"></span>' + ('<span class="you">you</span>' if men else ''))
        o += h
    return f'<div class="mlist">{o}</div>'
write('unread', shell('Messages - Unread', hdr(count='23 need reply'), f'<div class="mbody nopeek">{rail(on="Unread")}{unread_list()}</div>', keys('List keys', LIST_KEYS),
      'Unread, list, 14 rows, all unread. Lee in harbor, now: thanks, taking a look now. Kim in harbor, 50 minutes ago: ok, 90 s holds then, mentions you (circle: a thread of mine). Pat, direct message, 3 hours ago: thanks (circle). Rows without a circle are not mine.'))

# ---------------- conversation
def ln(name, tm, body, first=True, av='#3a3f58', ini='AD', cur=False, mention=False, extra='', hov=''):
    cls = 'cl' + (' first' if first else '') + (' cur' if cur else '') + (' mention' if mention else '')
    if first:
        return f'<div class="{cls}"><span class="av20" style="background:{av}">{ini}</span><span class="tx"><span class="nm">{name}</span><span class="tm">{tm}</span><br>{body}{extra}</span></div>'
    return f'<div class="{cls}"><span class="tm h" style="position:static;text-align:right;align-self:center;opacity:{1 if hov else 0}">{tm if hov else ""}</span><span class="tx">{body}{extra}</span></div>'
def blk(lines, cp=True): return '<div class="blk">' + ('<span class="cp">copy</span>' if cp else '') + ''.join(f'<div class="ln {c}">{t}</div>' for c, t in lines) + '</div>'
def lrow(gl, i, t, r): return f'<div class="lrow"><span class="g {gl}"></span><span class="id">{i}</span><span class="t">{t}</span><span class="r">{r}</span></div>'
RX = lambda *a: '<div class="rx">' + ''.join(f'<span class="{c}">{e} {n}</span>' for e, n, c in a) + '</div>'
def chan_lines(cur=None, hov=False):
    o = '<div class="gapm"><span>12 older messages not loaded.</span><span class="btn">Load older</span><span class="cd">next in 41 s</span><span class="sp"></span><span class="cd">Behind: 12 min</span></div>'
    o += '<div class="dday"><span>Today</span></div>'
    o += ln('Ada', '07:30', '<span class="you">@joi</span> review #3120 when you can', cur=(cur == 'm'), mention=True, extra=lrow('pr', '#3120', 'Seat holds: 90 s', '<b>CI green</b> · asked of you · ada'))
    o += ln('You', '08:05', 'what hold time do we want in staging?', av='#4a3b3b', ini='YO')
    o += '<div class="thr"><span class="avs"><i></i></span><span>Kim · 1 reply · last 08:20</span></div>'
    o += ln('Sam', '08:50', 'config for staging:', av='#4a3b3b', ini='SA', extra=blk([('', 'seat_hold_seconds: 90'), ('', 'hold_release: on_timeout')]))
    o += ln('', '08:51', 'that is what staging runs now', first=False, hov=hov)
    o += ln('Pat', '09:01', 'fare table change, one line each way:', av='#3b3f4a', ini='PA', extra=blk([('h', 'diff --git a/fares.yaml b/fares.yaml'), ('d', '- base: 14.50'), ('a', '+ base: 14.80')]))
    o += '<div class="ndiv">New</div>'
    o += ln('Lee', '09:08', 'anyone around for a quick look at the fare table?', av='#3b4a40', ini='LE')
    o += ln('', '09:09', 'it is the one-line change above', first=False)
    o += ln('', '09:10', 'thanks, taking a look now', first=False)
    return o
def conv_page(name, cur=None, hov=False, mode='Message keys', keys_=MSG_KEYS, title='Messages - #harbor', sr='', rail_on='harbor', focus=None, extra=''):
    ch = '<div class="chead"><b># harbor</b><span class="sub">412 people · starred</span></div>'
    body = f'<div class="mbody">{rail(on=rail_on, focus=focus)}<section class="conv">{ch}<div class="clines">{chan_lines(cur, hov)}</div><span class="newchip">3 new ↓</span></section>{peek_thread()}</div>'
    write(name, shell(title, hdr(count='23 need reply'), body, keys(mode, keys_), sr, extra))
conv_page('conversation', hov=True, sr='Conversation, harbor, a log. Ada, 07:30, mentions you: review 3120 when you can. Your thread with Kim: 1 reply. New messages: 1 below the divider. Gap: 12 older messages not loaded, Load older. Message keys.')
conv_page('conversation-focus', cur='m', rail_on='harbor', focus='harbor', title='Messages - focus rings',
          sr='Focus ring on the message line: Ada, 07:30, mention: review 3120 when you can. Message keys: reading harbor. The rail item keeps its own ring when the rail has focus.')
# DM, with link rows and links that stay plain text
def dm_lines():
    o = '<div class="dday"><span>Today</span></div>'
    o += ln('Sam', '08:48', 'the Linear board for the fare table is here: https://linear.app/private-team/issue/PRV-12', av='#4a3b3b', ini='SA')
    rows = lrow('pr', '#3120', 'Seat holds: 90 s', '<b>CI green</b> · asked of you · ada') + lrow('doing', '#3055', 'Seat hold release', 'In Progress · you') + lrow('merged', '#3104', 'fix: retry export upload', 'merged · 2h') + '<div class="lmore">+3 links</div>'
    o += ln('Sam', '09:00', 'and the PR and two follow-ups: https://github.com/jthorare/agentic-workflow/pull/3120 and more', first=False, hov=True, extra=rows)
    o += '<div class="ndiv">New</div>'
    o += ln('Sam', '09:02', 'can you ship the fare table today?', av='#4a3b3b', ini='SA')
    return o
dm_peek = peek_dm()
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
def snooze_page(name, follow, sr, title, peek=None, rail_html=None, rows=None, counts=(23, 14), count='23 need reply', ncount=23, total=22, dim=False):
    rr = ROWS if rows is None else rows
    body = f'<div class="mbody">{rail_html or rail(counts=counts)}<div style="min-width:0;overflow:hidden">{needs_list("full", len(rr[ADA - 9:]), 9, True, 9, rows=rr[ADA - 9:], total=total, dim=((9,) if dim else ()))}</div>{peek or peek_thread()}</div>'
    write(name, shell(title, hdr(count=count), body, keys('Picker', SNKEYS + ([('f', 'follow up if no reply')] if follow else [])), sr, snooze_menu(96 if follow else 150, follow), ncount=ncount))
snooze_page('snooze-picker', False, 'Snooze picker on the cursor row (' + rowsr(ROWS[ADA]) + '): later today, tomorrow, Monday, next week, a typed time. Release 1 uses Home’s picker as it is: no follow-up checkbox.', 'Messages - snooze picker')
# snoozed rows, "back Mon 9:00"
SNZ = [(3, 'Mira', 'DM', 'did the export retry change land? asking before I tell the customer', ['#3104'], 'back Mon 9:00', '2h')]
TOMAS = ('Tomas', '3h')
def snz_rows():
    # Mira is snoozed, so she leaves Needs reply (spec 9.4). Tomas came back early on a new reply, so he is in Needs reply again.
    rows = [dict(zip(('c', 'w', 'p', 't', 'ch', 'rs', 'a'), r)) for r in ROWS if r[1] != 'Mira']
    cur = next(i for i, r in enumerate(rows) if r['w'] == 'Ada' and r['a'] == '2h')
    o = '<div class="mgroup new">New<span class="n">1</span></div><div class="newband">' + mrow(NEW_ROW, 'full') + '</div>'
    o += '<div class="mgroup">Needs reply<span class="n">21</span></div>'
    # 21 rows in the band; the list is scrolled to the end, so 6 are above the fold (a cue says so) and 15 show
    o += '<div class="mgroup" style="margin-top:0;color:var(--ink-subtle);font-weight:400;height:24px">6 more above, scroll up</div>'
    for i, d in enumerate(rows):
        if i < 6: continue
        row = (d['c'], d['w'], d['p'], d['t'], d['ch'], 'back early: new reply' if (d['w'], d['a']) == TOMAS else d['rs'], d['a'])
        o += mrow(row, 'full', cursor=(i == cur), sel=(i == cur), actions=(i == cur))
    o += '<div class="mgroup">Snoozed<span class="n">1</span></div>'
    for r in SNZ:
        o += mrow(r, 'full', cls='dimr').replace(f'<span class="rs">{r[5]}</span>', f'<span class="rs" style="color:var(--ink-muted)">{r[5]}</span>')
    return f'<div class="mlist">{o}</div>'
write('snoozed-rows', shell('Messages - snoozed', hdr(count='22 need reply'), f'<div class="mbody">{rail(counts=(22, 14))}{snz_rows()}{peek_thread()}</div>', keys('List keys', LIST_KEYS),
      'Needs reply, 22 rows. Tomas in billing, 3 hours ago, back early: new reply: ' + 'review please, proration preview rounds half cents the wrong way. Snoozed, 1 row: Mira, direct message, back Monday 9:00. The cursor and the peek are on Ada.', ncount=22))

# ---------------- first run (6.8)
def stp(n, title, status, body='', cur=False, done=False, todo=False):
    num = '<span class="g done"></span>' if done else f'<span class="num{" cur" if cur else ""}">{n}</span>'
    return f'<div class="stp {"todo" if todo else ""}"><div class="sh">{num}<span>{title}</span><span class="sp"></span><span class="st">{status}</span></div>{("<div class=sb>" + body + "</div>") if body else ""}</div>'
MANIFEST = 'display_information:\n  name: Sindri Messages\noauth_config:\n  scopes:\n    user:\n      - channels:history\n      - groups:history\n      - im:history\n      - mpim:history\n      - channels:read\n      - groups:read\n      - im:read\n      - mpim:read\n      - users:read\n      - emoji:read\n      - search:read\nsettings:\n  token_rotation_enabled: false'
def fail(text, det, cls=''):
    d = f'<div class="dt">Details ▸ {det}</div>' if det else ''
    return f'<div class="fbox {cls}"><span class="g {"parked" if cls else "fail"}"></span><div class="w">{text}{d}</div></div>'
def card(name, steps, sr, lead="Messages reads your Slack. It needs a token that only you can make.", title='Messages - connect Slack', off=False, kb=None):
    body = f'<div class="mbody norail"><div class="card"><h1>Connect Slack</h1><p class="lead">{lead}</p>{steps}</div></div>'
    write(name, shell(title, hdr(count='', hint=False).replace('<span class="stat ">Live (polling every 30 s) · joi</span>', '<span class="stat off">Not connected</span>'), body, kb or keys('Setup', [('Tab', 'next field'), ('Enter', 'continue'), ('Esc', 'back')]), sr, off=off, ncount=None))
S1_BODY = '<p>Open Slack’s create-app page with the manifest filled in. Name it anything, then press Create.</p><div style="display:flex;gap:20px;align-items:flex-start"><div><div class="row2" style="flex-direction:column;align-items:flex-start"><span class="btn primary">Open Slack’s create-app page <kbd>Enter</kbd></span><span class="btn">Copy manifest</span></div></div><div style="flex:1;min-width:0"><div style="font-size:11px;color:var(--ink-subtle);margin-bottom:4px">Manifest, the same text the link pre-fills</div><div class="mf">' + MANIFEST + '</div></div></div>'
card('firstrun-1', stp(1, 'Create the app', 'Step 1 of 3', S1_BODY, cur=True) + stp(2, 'Install it', '', todo=True) + stp(3, 'Paste the token', '', todo=True),
     'Connect Slack, step 1 of 3: Create the app. Button: Open Slack’s create-app page. Steps 2 and 3 are not open yet.')
card('firstrun-1-fail', stp(1, 'Create the app', 'Step 1 of 3', fail('The link didn’t open Slack’s page. Make the app by hand: open api.slack.com/apps, choose Create New App, From a manifest, and paste this text.', '') + f'<div class="mf">{MANIFEST}</div><div class="row2"><span class="btn primary">Copy manifest <kbd>Enter</kbd></span><span class="btn">Try the link again</span></div>', cur=True) + stp(2, 'Install it', '', todo=True) + stp(3, 'Paste the token', '', todo=True),
     'Step 1 failed: the link did not open Slack. The manifest and the steps by hand are shown. Button: Copy manifest.')
S2_BODY = '<p>In the app’s page choose Install to Workspace and allow the read scopes. Then come back here.</p><div class="row2"><span class="btn primary">I installed it <kbd>Enter</kbd></span><span class="btn">Back</span></div>'
card('firstrun-2', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Step 2 of 3', S2_BODY, cur=True) + stp(3, 'Paste the token', '', todo=True),
     'Connect Slack, step 2 of 3: Install it. Step 1 is done. Button: I installed it.')
REQ = 'Hi, I would like to add a read-only Slack app (Sindri Messages) to our workspace for my own use. It asks for user scopes only: read channels, DMs and group DMs, read people and emoji, and search. It cannot post or change anything. Could you approve it? Thanks.'
card('firstrun-2-blocked', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Blocked', fail('Your workspace needs an admin to approve this app. No Messages page can exist until they do.', 'SND-MSG-019 · Slack says: app_approval_required', 'att') + f'<p>Send this to your Slack admin:</p><div class="mf" style="white-space:normal;width:560px;line-height:18px">{REQ}</div><div class="row2"><span class="btn primary">Copy request <kbd>Enter</kbd></span><span class="btn">Check again</span></div>', cur=True) + stp(3, 'Paste the token', '', todo=True),
     'Step 2 is blocked: the workspace needs an admin to approve the app. Copyable request text for the admin. Details: SND-MSG-019. Buttons: Copy request, Check again.')
FIELD = lambda t, f=True: f'<div class="field {"focus" if f else ""}">{t}<span class="caret"></span></div>'
S3_BODY = '<p>Open OAuth &amp; Permissions in the app’s page, copy the User OAuth Token (starts with xoxp-), and paste it here. It is never shown again and Sindri clears the clipboard after the paste, if it can.</p>'
card('firstrun-3', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div class="okline"><span class="g done"></span>User token (xoxp-). Sindri checks the 11 read scopes when you press Connect.</div><div class="row2" style="margin-top:10px"><span class="btn primary">Connect <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 of 3: Paste the token. The field is write-only and shows dots. Detected: user token. The 11 read scopes are checked on Connect. Button: Connect.')
card('firstrun-3-wrong-token', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('That is a bot token (xoxb-). Messages needs the User OAuth Token (xoxp-) from OAuth &amp; Permissions, User Token section.', 'SND-MSG-030') + '<div class="row2"><span class="btn primary">Paste another token <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 failed: the pasted token is a bot token. Messages needs the user token that starts with xoxp-. Details: SND-MSG-030. The token was not sent anywhere.')
card('firstrun-3-scope', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('The token lacks the scope search:read. Add it in the Slack app, reinstall, then paste the new token.', 'SND-MSG-003', 'att') + '<div class="row2"><span class="btn primary">Paste another token <kbd>Enter</kbd></span></div>', cur=True),
     'Step 3 failed: the token lacks the scope search:read. Details: SND-MSG-003. Button: Paste another token.')
card('firstrun-3-offline', stp(1, 'Create the app', 'Done', done=True) + stp(2, 'Install it', 'Done', done=True) + stp(3, 'Paste the token', 'Step 3 of 3', S3_BODY + FIELD('••••••••••••••••••••••••') + '<div style="height:8px"></div>' + fail('Offline: connect when you’re back. Nothing is stored. The token stays in this box for this session.', 'SND-MSG-028', 'att') + '<div class="row2"><span class="btn primary off">Connect <kbd>Enter</kbd></span><span style="font-size:12px;color:var(--ink-subtle)">Connect turns on when the network is back</span></div>', cur=True),
     'Step 3, offline: Offline, connect when you are back. Nothing is stored. The token field keeps its text for this session. Connect is off. Details: SND-MSG-028.', off=True)

# ---------------- first sync and listening
def syncpage(name, lines, sr, state='live', title=''):
    body = f'<div class="mbody">{rail(counts=(0, 0), starred=False)}<div class="mlist"><div class="mid" style="padding-top:48px">{lines}</div></div></div>'
    write(name, shell(title or 'Messages - ' + name, hdr(state, count='0 need reply'), body, keys('List keys', [('Ctrl+K', 'search or jump'), ('?', 'all keys')]), sr, ncount=0))
syncpage('firstrun-sync', '<div class="g agent" style="margin-bottom:10px"></div><h2>Connected. Reading your Slack.</h2><p>40 conversations, 0 messages so far.</p><p style="color:var(--ink-subtle);font-size:12px">Your DMs, mentions and starred channels load first. Needs reply fills in as messages arrive.</p>',
         'First sync: 40 conversations, 0 messages so far. Reading your Slack.', title='Messages - first sync')
syncpage('firstrun-listening', '<span class="g done" style="margin-bottom:10px;display:block"></span><h2>Listening.</h2><p>New messages appear here. Open a conversation to load its history (1 per minute).</p><p style="color:var(--ink-subtle);font-size:12px">Day-one rows come from Slack search. Sindri is polling your hot conversations every 30 s.</p>',
         'Listening. New messages appear here. Open a conversation to load its history, one per minute.', title='Messages - listening')

# ---------------- states
def emptypage(name, on, h, p, sr, rail_kw=None, count='0 need reply', counts=(0, 14), ncount=0):
    body = f'<div class="mbody nopeek">{rail(on=on, counts=counts, **(rail_kw or {}))}<div class="mlist"><div class="mid"><h2>{h}</h2><p>{p}</p></div></div></div>'
    write(name, shell('Messages - ' + name, hdr(count=count), body, keys('List keys', [('Ctrl+K', 'search or jump'), ('?', 'all keys')]), sr, ncount=ncount))
emptypage('empty-needs', 'Needs reply', 'You’re caught up.', 'The next snoozed thread is back Mon 9:00.', 'Needs reply is empty: You are caught up. The next snoozed thread is back Monday 9:00. The rail shows no starred channels yet.', {'starred': False})
emptypage('empty-unread', 'Unread', 'Nothing unread.', 'New messages in your DMs, mentions and starred channels show here.', 'Unread is empty: Nothing unread.', count='3 need reply', counts=(3, 0), rail_kw={'harbor': ''}, ncount=3)
sk = ''.join(f'<div class="sk"><span class="g c3" style="opacity:.3"></span><i style="width:{a}px"></i><i style="width:{b}px"></i><i style="width:{c}px;background:var(--s2)"></i><i style="width:24px"></i></div>' for a, b, c in [(60, 40, 320), (50, 60, 280), (70, 50, 360), (56, 44, 240), (62, 58, 300), (48, 40, 340)])
write('loading', shell('Messages - loading', hdr(count='23 need reply'), f'<div class="mbody nopeek">{rail()}<div class="mlist"><div class="mgroup">Needs reply<span class="n"></span></div>{sk}</div></div>', keys('List keys', [('Ctrl+K', 'search or jump'), ('?', 'all keys')]),
      'Loading: 6 skeleton rows painted from the cache. Rows fill in from the store. The header, the tab and the rail all read 23.', ncount=23))
# toast, sender and place only
TOAST = '<div class="toast"><span class="g c3"></span><div><b style="font-weight:600">Sam, DM</b><div style="font-size:12px;color:var(--ink-subtle)">Needs reply</div></div><span class="btn" style="margin-left:auto">Open</span></div>'
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

# =================== Release 2 (composer, drafts, react, Later, follow up) ===================
A1 = ('Reply', 'r')   # in Release 2, r opens the composer; the cursor-row buttons are Reply, Snooze, Done (spec 6.1, 6.5)
LIST2 = [('j/k', 'move'), ('Space', 'peek'), ('Enter', 'open'), ('r', 'reply'), ('e', 'done'), ('h', 'snooze'), ('/', 'filter'), ('?', 'all keys')]
CK = [('Ctrl+Enter', 'send'), ('Enter', 'new line'), ('Esc', 'leave the composer, text kept'), ('Ctrl+B / I', 'format')]
def agentav(): return '<span class="av agent" style="width:16px;height:16px;line-height:16px">A</span>'
def tb(): return '<div class="tb" aria-label="Formatting"><i>B</i><i style="font-style:italic">I</i><i style="text-decoration:line-through">S</i><i class="mono">&lt;/&gt;</i><i>❝</i><i>•</i><span class="sp"></span><span class="btn">Ask an agent to draft</span></div>'
def comp(text=None, target='#harbor, 412 people', focus=False, send='Send', send_off=None, line='', chips='', ph='Reply to #harbor', box_extra=''):
    off = (text is None) if send_off is None else send_off
    t = f'{text}<span class="caret"></span>' if text is not None else f'<span class="ph">{ph}</span>' + ('<span class="caret"></span>' if focus else '')
    s = f'<span class="btn primary {"off" if off else ""}">{send} <kbd>Ctrl+Enter</kbd></span>'
    return f'<div class="comp"><div class="to">To <b>{target}</b></div>{line}{tb()}<div class="box {"focus" if focus else ""}">{t}{box_extra}</div>{("<div style=margin-top:6px;display:flex;gap:6px>" + chips + "</div>") if chips else ""}<div class="acts">{s}<span class="sp"></span><span class="hint">Enter is a new line</span></div></div>'
# one #harbor thread everywhere: Ada's root at 07:30 alone; You 09:14 is the first reply where a reply was sent
def thread_lines(extra='', youline=''):
    lr = lrow('pr', '#3120', 'Seat holds: 90 s', '<b>CI green</b> · asked of you · ada')
    return ln('Ada', '07:30', '<span class="you">@joi</span> review #3120 when you can', mention=True, extra=lr) + youline
YOU = lambda extra='', cls='': ln('You', '09:14', 'On it, reviewing #3120 now.', av='#4a3b3b', ini='YO', extra=extra)
def peek_r2(cmp, lines=None, head='Thread · #harbor', idt='slack:thread:harbor', replies=0, waits='you', ob=''):
    body = lines if lines is not None else thread_lines()
    return f'''<aside class="peek"><div class="peek-head"><h2>{head}</h2><span class="id">{idt}</span><span class="icon">⤢</span><span class="icon">✕</span></div>
<dl class="peek-fields"><dt>Class</dt><dd><span class="g c2"></span>Review ask</dd><dt>Waits on</dt><dd>{waits}</dd></dl>
<div class="mp" style="padding-top:4px"><div class="eyebrow" style="margin-top:6px">{"Thread, root only" if replies == 0 else f"Thread, root and {replies} reply"}</div>{body}</div>{ob}{cmp}</aside>'''
def r2list(rows=None, cm=True, total=22, ncount=1, dim=False, n=None):
    rows = ROWS if rows is None else rows
    st = max(0, ADA - 9)
    n = n or len(rows[st:])
    return needs_list('full', n, ADA - st, True, ADA - st, rows=rows[st:], total=total, ncount=ncount, cm=cm, dim=((ADA - st,) if dim else ()))
def r2page(name, peek, sr, extra='', kb=None, state='live', title=None, rail_kw=None, center=None, off=False, drafts=0, cm=False, rel='', pill='Connected', counts=(23, 14), count='23 need reply', ncount=23):
    center = center or r2list(cm=cm)
    rk = dict(drafts=drafts); rk.update(rail_kw or {}); rk.setdefault('counts', counts)
    body = f'<div class="mbody">{rail(**rk)}<div style="min-width:0;overflow:hidden;display:flex;flex-direction:column">{center}</div>{peek}</div>'
    h = hdr(state, count=count)
    if rel: h = h.replace('</h1>', f'</h1><span class="rel">{rel}</span>', 1)
    write(name, shell(title or 'Messages - ' + name, h, body, kb or keys('Composer', CK), sr, extra, off=off, pill=pill, ncount=ncount))
PCK = lambda top, inner: f'<div class="pk" style="right:10px;top:{top}px;width:420px">{inner}</div>'
r2page('composer-empty', peek_r2(comp(None, focus=False)), 'Composer in the peek, empty. Target named: harbor, 412 people. Placeholder: Reply to harbor. Send is off until there is text. The formatting toolbar has a name; its buttons are bold, italic, strike, code, quote, list. Send is Control Enter; Enter is a new line.', cm=True)
PICK = ('<div class="mh">Pull requests and issues</div><div class="pi on"><span class="g pr"></span><span class="id">#3120</span><span>Seat holds: 90 s</span><span class="w">inserts the URL</span></div><div class="pi"><span class="g doing"></span><span class="id">#3055</span><span>Seat hold release</span><span class="w">inserts the URL</span></div>'
        '<div class="mh">Notes and scopes</div><div class="pi"><span class="g note"></span><span class="id">note</span><span>Seat hold design</span><span class="chip2">local only: not a link</span></div><div class="pi"><span class="g linked"></span><span class="id">scope</span><span>export/upload</span><span class="chip2">local only: not a link</span></div>'
        '<div class="pf"><span><kbd>↑↓</kbd>move</span><span><kbd>Enter</kbd>insert</span><span><kbd>Esc</kbd>close picker</span></div>')
r2page('composer-typing-picker', peek_r2(comp('Looking now. The 90 s hold matches [[sea', focus=True)), 'Composer with the double-bracket picker open. A pull request or issue inserts its public URL. A note or a scope inserts its title as plain text and the chip says local only: not a link.', PCK(440, PICK), kb=keys('Picker', [('↑↓', 'move'), ('Enter', 'insert'), ('Esc', 'close picker')]), cm=True)
RISK = '<span class="chip2 att">broadcast: @channel</span><span class="chip2 att">link</span>'
r2page('composer-agent-draft', peek_r2(comp('Hi all, the 90 s hold ships with #3120. @channel please re-test the group booking flow: https://github.com/acme/api/pull/3120', send='Review and send', line=f'<div class="ln1"><span>{agentav()}</span><span><b style="font-weight:600">Agent draft</b> by claude-code · <span class="lk">run 8860</span> · not sent</span><span class="sp"></span><span class="dt">edit it, or Esc to keep it in Drafts</span></div>', chips=RISK)),
       'Agent draft in the composer, labelled Agent draft by claude-code, run 8860, not sent. One chip per risk present: broadcast at channel, link. The button reads Review and send. An agent draft is never sent without your send action.', drafts=1, cm=True)
MENU = ('<div class="menu" style="right:60px;top:470px;width:300px;z-index:6"><div class="mhead">Send to #harbor</div>'
        '<div class="ment on"><span class="lab">Notify 412 people</span><kbd>Enter</kbd></div><div class="ment"><span class="lab">Send and mark done</span></div><div class="ment" style="opacity:.5"><span class="lab">Schedule</span><span class="when">arrives with scheduled send</span></div>'
        '<div class="mnote">The text holds @channel and this channel has 412 members, so the menu opens first. Enter commits the highlighted row.</div></div>')
r2page('composer-submit-menu', peek_r2(comp('Hi all, the 90 s hold ships with #3120. @channel please re-test', focus=True, send='Send')), 'Submit menu opened by Control Enter because the text holds at channel and the channel has 412 members. Primary: Notify 412 people. Send and mark done. Schedule is off: arrives with scheduled send.', MENU, kb=keys('Menu', [('↑↓', 'move'), ('Enter', 'commit'), ('Esc', 'close')]), cm=True)
# sent: Ada's row stays in place, dimmed with "replied 09:14"; counts drop by one
ROWS_REPLIED = [(r[0], r[1], r[2], r[3], r[4], 'replied 09:14' if i == ADA else r[5], r[6]) for i, r in enumerate(ROWS)]
SENT_LINE = f'<div class="ln1" style="background:var(--s2)"><span class="g done"></span><span>Sent.</span><span class="btn">Follow up if no reply</span><span class="sp"></span><span class="dt">closes in 5 s</span></div>'
r2page('composer-sent', peek_r2(comp(None, line=SENT_LINE, send_off=True), lines=thread_lines(youline=YOU()), replies=1, waits='them'),
       'Sent. The confirmation line offers a button, Follow up if no reply. The composer keeps focus, so press Escape to leave it, then h. The row for Ada stays in place, dimmed, with replied 09:14, until the next settle point. Counts: 22 need reply.',
       center=r2list(rows=ROWS_REPLIED, total=21, dim=True), cm=True, counts=(22, 14), count='22 need reply', ncount=22)
FAILED = thread_lines(youline=YOU(extra='<div class="fl"><span>failed: Slack refused it (restricted_action)</span><span class="btn">Retry</span><span class="btn">Discard</span><span class="dt">SND-MSG-004</span></div>').replace('class="cl first"', 'class="cl first grey"').replace('class="cl first grey"', 'class="cl first"', 3) if False else '')
def greyed(extra): return ln('You', '09:14', 'On it, reviewing #3120 now.', av='#4a3b3b', ini='YO', extra=extra).replace('class="cl first"', 'class="cl first grey"', 1)
r2page('composer-failed', peek_r2(comp('Sorry, rejoining the channel first.', line='<div class="ln1 err"><span class="g fail"></span><span>Couldn’t send. Your reply is kept on the line above.</span><span class="sp"></span><span class="dt">Details ▸ SND-MSG-004 · restricted_action</span></div>'), lines=thread_lines(youline=greyed('<div class="fl"><span>failed: Slack refused it (restricted_action)</span><span class="btn">Retry</span><span class="btn">Discard</span><span class="dt">SND-MSG-004</span></div>')), replies=1),
       'Failed send: the line stays greyed with failed, Slack refused it, Retry and Discard. The draft is kept. Details: SND-MSG-004. Announcement: Send failed. Retry is next.', drafts=1, cm=True)
r2page('composer-send-unknown', peek_r2(comp(None), lines=thread_lines(youline=greyed('<div class="fl" style="color:var(--att)"><span>Slack may have delivered this message. Check #harbor, then:</span></div><div class="fl" style="color:var(--ink-muted)"><span class="btn">It’s there (drop it)</span><span class="btn">Send again</span><span class="dt">SND-MSG-032</span></div>')), replies=1),
       'Unknown send: Slack may have delivered this message. Check harbor, then: It is there, drop it, or Send again. Details: SND-MSG-032. A row never re-sends without proof the message is absent.', cm=True)
r2page('composer-offline', peek_r2(comp('Sorry, rejoining the channel first. Typing while the wifi is down', focus=True, send_off=True, line='<div class="ln1"><span class="g parked"></span><span>Offline: your text is kept as a draft. Send when you’re back.</span><span class="sp"></span><span class="dt">SND-MSG-028</span></div>')), 'Offline composer: the box stays editable with focus. Send is off. The line says: Offline, your text is kept as a draft. Send when you are back. Details: SND-MSG-028. Nothing is queued.', state='offline', off=True, drafts=1, pill='Sindri running', cm=True)
r2page('composer-429', peek_r2(comp(None), lines=thread_lines(youline=greyed('<div class="fl" style="color:var(--att)"><span>waiting, Slack asked us to wait 12 s</span><span class="dt">after 2 minutes it fails with SND-MSG-005</span></div>')), replies=1),
       'Rate limited: the line is pending. Waiting, Slack asked us to wait 12 seconds. After 2 minutes it fails with SND-MSG-005 and Retry. It never sends on its own later. The header stays Live: a send limit is per method.', cm=True)
r2page('composer-focus-ring', peek_r2(comp('Looking now.', focus=True)), 'Focus ring on the composer: 2 px accent ring, 3 to 1 against the panel. The list shows a cursor mark, not a ring. After a send the composer stays focused.', title='Messages - focus ring on the composer', cm=True)
# agent hand-off dialog (spec 9.7 item 8)
def dim_page(name, dlg, kbd, sr, mode='', title=None, rel='', peek=None):
    body = f'<div class="mbody">{rail(drafts=0)}<div style="min-width:0;overflow:hidden">{r2list(cm=True)}</div>{peek or peek_r2(comp(None))}</div>'
    h = hdr()
    if rel: h = h.replace('</h1>', f'</h1><span class="rel">{rel}</span>', 1)
    write(name, shell(title or 'Messages - ' + name, h, body, keys(mode, kbd), sr, '<div class="dimall"></div>' + dlg))
HAND = ('<div class="dlg" style="top:130px;width:600px"><div class="dh">Hand this thread to an agent?</div><div class="db">'
        '<div class="fr"><span class="l">Provider</span><div class="val">claude-code ▾</div></div>'
        '<div class="fr"><span class="l">What goes along</span><div class="val" style="color:var(--ink-muted)">Thread of 4 messages, 1 linked row, about 1k tokens</div></div>'
        '<div class="fr" style="align-items:start"><span class="l" style="padding-top:8px">Note</span><div class="bodybox" style="font:400 13px/20px var(--font);min-height:56px;color:var(--ink-subtle)">What should the draft say? (optional)</div></div>'
        '<div style="font-size:12px;line-height:18px;color:var(--ink-muted);margin:8px 0 0 98px">The session is a normal session. It can run shell commands and gh. The thread goes as a file it reads through a fenced result, never pasted into its system prompt. The draft comes back as a separate row in Drafts. It is never sent without your send action.</div></div>'
        '<div class="df"><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="sp"></span><span class="btn primary">Send thread to claude-code</span></div></div>')
dim_page('composer-hand-off', HAND, [('Tab', 'next field'), ('Esc', 'cancel')], 'Hand this thread to an agent, dialog. Provider claude-code. Thread of 4 messages, 1 linked row, about 1k tokens. A normal session that can run shell and gh. Send is your explicit act.')
# snooze with follow-up (Release 2): the cursor is on a thread where I spoke last (Ada, replied 09:14)
snooze_page('snooze-picker-followup', True, 'Snooze picker on a thread where I spoke last (my 09:14 line is the last in the peek; the row reads replied 09:14), with the checkbox Follow up if no reply, checked, key f. Release 2.', 'Messages - snooze picker with follow up', peek=peek_r2(comp(None), lines=thread_lines(youline=YOU()), replies=1, waits='them'), rail_html=rail(drafts=0, counts=(22, 14)), rows=ROWS_REPLIED, count='22 need reply', ncount=22, total=21, dim=True)

# ---------------- peeks (user, huddle), empty Later and Drafts, emoji (Release 2)
def peek_other(name, head, idt, fields, body, sr, ob=True, rel='', kb=None):
    fl = ''.join(f'<dt>{k}</dt><dd>{v}</dd>' for k, v in fields)
    obh = '<div class="ob"><b>Open in Slack</b><br><span class="btn primary" style="margin-top:6px">Open in Slack</span></div>' if ob else ''
    p = f'<aside class="peek"><div class="peek-head"><h2>{head}</h2><span class="id">{idt}</span><span class="icon">⤢</span><span class="icon">✕</span></div><dl class="peek-fields">{fl}</dl><div class="mp">{body}{obh}</div></aside>'
    r2page(name, p, sr, kb=kb or keys('Peek', [('Esc', 'close peek'), ('?', 'all keys')]), rel=rel)
peek_other('peek-user', 'Ada Okafor', 'slack:user:U0ADA', [('Title', 'Platform engineer'), ('Local time', '09:10 · same zone'), ('Status', 'In a review'), ('In common', '#harbor · #billing')], '<div class="eyebrow">Recent with you</div><div style="font-size:13px;color:var(--ink-muted);line-height:22px">1 thread waits on you · last message 2 h ago</div>', 'User peek: Ada Okafor, platform engineer, local time 09:10, status in a review. One thread waits on you. Button: Open in Slack.')
peek_other('peek-huddle', 'Huddle · #harbor', 'slack:huddle:harbor', [('Started by', 'Ada'), ('Place', '#harbor'), ('Time', '09:05')], '<div class="hrow"><span>' + agentav() + '</span><span>Huddle in #harbor</span><span class="sp"></span><span class="btn primary">Open in Slack</span></div><div style="font-size:12px;color:var(--ink-subtle);line-height:18px;margin-top:8px">A user token cannot join a huddle, so Sindri shows the row and the link only.</div>', 'Huddle row: Huddle in harbor, started by Ada. A user token cannot join a huddle. Button: Open in Slack.', ob=False)
def emp2(name, on, h, p, sr, rail_kw):
    body = f'<div class="mbody nopeek">{rail(on=on, counts=(23, 14), **rail_kw)}<div class="mlist"><div class="mid"><h2>{h}</h2><p>{p}</p></div></div></div>'
    write(name, shell('Messages - ' + name, hdr(count='23 need reply'), body, keys('List keys', LIST2), sr))
emp2('empty-later', 'Later', 'Nothing flagged.', 'Press l on a message.', 'Later is empty after the last flag was removed: Nothing flagged. Press l on a message. The header keeps 23 need reply.', {'later': 0, 'drafts': 0})
emp2('empty-drafts', 'Drafts', 'No drafts.', 'Anything you start typing and leave is kept here.', 'Drafts is empty: No drafts. Anything you start typing and leave is kept here. No flag exists, so Later is not on the rail.', {'drafts': 0})
EMO = '<div class="emo" style="left:330px;top:215px"><div class="f">zzzq<span style="display:inline-block;width:1px;height:16px;background:var(--primary-hi)"></span></div><div class="nm">No match</div></div>'
body = f'<div class="mbody"><{"aside"} class="rail">{rail(drafts=0)[len("<aside class=" + chr(34) + "rail" + chr(34) + ">"):-len("</aside>")]}</aside><section class="conv"><div class="chead"><b># harbor</b><span class="sub">412 people · starred</span></div><div class="clines">{chan_lines("m")}</div></section>{peek_r2(comp(None))}</div>'
write('emoji-no-match', shell('Messages - emoji picker', hdr(), body, keys('Picker', [('←→↑↓', 'move'), ('Enter', 'pick'), ('Esc', 'close')]), 'Emoji picker for the cursor message, Ada at 07:30. Query zzzq: No match. Arrows move in two dimensions, typing filters, Enter picks.', EMO))

# =================== Release 3 frames (labelled): make issue, attach, brief, search, files, app messages ===================
R3 = 'Release 3'
BODYTXT = 'https://acme.slack.com/archives/C0HARBOR/p1762932600000100\n\nSeat holds: the group booking path still uses 120 s.'
MK = ('<div class="dlg"><div class="dh">Make an issue from this thread</div><div class="db">'
      '<div class="fr"><span class="l">Title</span><div class="val">Group booking seat holds still use 120 s</div></div>'
      '<div class="fr"><span class="l">Team</span><div class="val">Platform (PLT)</div></div><div class="fr"><span class="l">Project</span><div class="val">Seat holds (project of #3120)</div></div>'
      '<div class="fr"><span class="l">Assignee</span><div class="val">you</div></div><div class="fr"><span class="l">Label</span><div class="val" style="color:var(--ink-subtle)">none</div></div>'
      f'<div class="fr" style="align-items:start"><span class="l" style="padding-top:8px">Body</span><div class="bodybox">{BODYTXT}</div></div>'
      '<div style="font-size:11px;color:var(--ink-subtle);margin:6px 0 0 98px">The body is the Slack link plus text you type. Sindri never copies Slack text into it. This is exactly what Linear receives.</div>'
      '<div class="fr" style="margin-top:6px"><span class="l"></span><span style="display:flex;gap:8px;align-items:center"><span class="g box"></span>Reply in the thread with the issue link</span></div></div>'
      '<div class="df"><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="sp"></span><span class="btn primary">Create in Linear <kbd>Ctrl+Enter</kbd></span></div></div>')
dim_page('make-issue-sheet', MK, [('Tab', 'next field'), ('Ctrl+Enter', 'create'), ('Esc', 'cancel')], 'Release 3. Make an issue from this thread, dialog. The sheet shows the full body before Create: the Slack link plus your text. Reply in the thread is a checkbox, off. Button: Create in Linear.', rel=R3)
ATT = ('<div class="dlg"><div class="in"><span class="scope">Attach</span><span>3120</span><span class="caret" style="display:inline-block;width:1px;height:18px;background:var(--primary-hi)"></span></div>'
       '<div class="gh">Issues and pull requests</div>'
       '<div class="pi2 on"><span class="g pr"></span><span class="id">#3120</span><span>Seat holds: 90 s</span><span class="w">CI green · ada</span></div>'
       '<div class="pi2"><span class="g doing"></span><span class="id">#3055</span><span>Seat hold release</span><span class="w">In Progress · you</span></div>'
       '<div class="pi2"><span class="g pr"></span><span class="id">#3125</span><span>Stacked fare change</span><span class="w">open · stacked on #3120</span></div>'
       '<div class="gh">Not a match</div><div class="pi2" style="color:var(--ink-subtle)"><span></span><span></span><span>No other issue matches 3120</span><span></span></div>'
       '<div class="df"><span class="tag">Only objects you can read are listed</span><span class="sp"></span><span class="btn ghost">Cancel <kbd>Esc</kbd></span><span class="btn primary">Attach <kbd>Enter</kbd></span></div></div>')
dim_page('attach-picker', ATT, [('↑↓', 'move'), ('Enter', 'attach'), ('Esc', 'cancel')], 'Release 3. Attach picker over issues and pull requests, query 3120. Selected: pull request 3120. Only objects you can read are listed. Enter attaches; nothing is created.', rel=R3)
def brief(model_on=True):
    def wl(t, src): return f'<div class="wl"><span class="g c1"></span><span>{t}</span><span class="src">' + ''.join(f'<span>{s}</span>' for s in src) + '</span></div>' if model_on else ''
    note = '<div class="note">Written from the store as of 09:10. Provider: claude-code. A DM or a channel that is not starred gets facts only.</div>' if model_on else '<div class="fbox att" style="margin:8px 0;display:flex;gap:10px;background:var(--att-soft);border-radius:6px;padding:8px 12px"><span class="g parked" style="margin-top:3px"></span><div>Model off: facts only. Turn the model on in Settings to get written lines for starred channels.<div class="mono" style="font-size:11px;color:var(--ink-subtle)">Details ▸ messages.model.allow is empty</div></div></div>'
    o = f'<div class="brief"><div class="bh"><b>Catch up: 9 h away</b><span class="tag">{"model on" if model_on else "model off"}</span></div>{note}'
    chans = [('# harbor', '2 new, 1 mention you, 1 thread with 3 replies', [('Holds are now 90 s in staging', ['08:41', '08:55']), ('Ada asks you to review #3120', ['07:30'])]),
             ('# billing', '6 new, 0 mention you (not starred: facts only)', []),
             ('Sam (DM)', '1 new: a direct question', []), ('# ferry-ops', '3 new, 12 messages missing', [])]
    for cn, facts, wls in chans:
        o += f'<div class="ch"><div class="cn">{cn}</div><div class="facts">{facts}</div>' + ''.join(wl(t, s) for t, s in wls) + '</div>'
    return o + '</div>'
def brief_page(name, on, sr):
    body = f'<div class="mbody nopeek">{rail(drafts=0)}<div style="min-width:0;overflow:hidden">{brief(on)}</div></div>'
    h = hdr().replace('</h1>', f'</h1><span class="rel">{R3}</span>', 1)
    write(name, shell('Messages - ' + name, h, body, keys('', [('j/k', 'move'), ('Space', 'peek the source'), ('Enter', 'open'), ('Esc', 'back')]), sr))
brief_page('brief', True, 'Release 3. Catch-up brief, 9 hours away. Per channel: a facts line, then at most 3 written lines for starred channels, each with chips for its source messages. billing is not starred, so it has facts only. Space peeks the source; Enter opens the conversation.')
brief_page('brief-model-off', False, 'Release 3. Catch-up brief with the model off: facts lines only, and the page says why. Details: messages.model.allow is empty.')
def srch(name, scope, q, groups, sr):
    foot = [('↑↓', 'move'), ('Enter', 'open'), ('Esc', 'close')]
    other = 'Everything' if scope == 'Messages' else 'Messages'
    inner = f'<div class="dlg" style="top:90px;width:720px"><div class="in"><span class="scopebtn">{scope} ▾</span><span class="mono" style="font-size:14px">{q}</span><span style="display:inline-block;width:1px;height:18px;background:var(--primary-hi)"></span></div>{groups}<div class="df" style="margin-top:6px">{kk(foot)}<span class="sp"></span><span class="tag">Scope: click the chip or run messages.search-scope</span></div></div>'
    body = f'<div class="mbody">{rail(drafts=0)}<div style="min-width:0;overflow:hidden">{r2list(cm=True)}</div>{peek_r2(comp(None))}</div>'
    h = hdr().replace('</h1>', f'</h1><span class="rel">{R3}</span>', 1)
    write(name, shell('Messages - ' + name, h, body, keys('', foot), sr, '<div class="dimall"></div>' + inner))
def hit(gl, who, place, t, tag='', age='2h'):
    return f'<div class="pi2"><span class="g {gl}"></span><span class="id">{who}</span><span>{t}<span class="tagq">{place}</span>{("<span class=tagq style=color:var(--att)>" + tag + "</span>") if tag else ""}</span><span class="w">{age}</span></div>'
srch('search-messages', 'Messages', 'seat holds from:ada', '<div class="gh">Messages · from the store, 38 ms</div>' + hit('c1', 'Ada', '#harbor', 'seat holds are still 120 s in staging', '', '3h') + hit('c2', 'Ada', '#harbor', '@joi review #3120 when you can', '', '2h') + hit('c1', 'Ada', '#billing', 'seat holds copy for the invoice email', '', '1d')
     + '<div class="gh">Slack, 3 calls a minute</div>' + hit('c1', 'Ada', '#ops-archive', 'old thread on seat hold release', 'from Slack', '4w') + hit('c1', 'Ada', 'DM Priya', 'seat holds and the demo env', 'from Slack', '5w') + '<div class="pi2" style="color:var(--ink-subtle)"><span></span><span></span><span>Search Slack too found 2. Press Enter on a Slack row to open it.</span><span></span></div>',
     'Release 3. Search in Messages: local results first, then results from Slack tagged from Slack. Operator from. The scope chip is a button, with the palette entry messages.search-scope; Tab does not switch it. Results: 3 from the store, 2 from Slack.')
srch('search-everything', 'Everything', 'seat holds', '<div class="gh">Messages 4</div>' + hit('c1', 'Ada', '#harbor', 'seat holds are still 120 s in staging', '', '3h') + hit('c2', 'Ada', '#harbor', '@joi review #3120 when you can', '', '2h') + hit('c1', 'Ada', '#billing', 'seat holds copy for the invoice email', '', '1d') + hit('c1', 'Sam', '#harbor', 'config for staging: seat hold seconds', '', '20m')
     + '<div class="gh">Pull requests and issues 2</div>' + hit('pr', '#3120', 'PR', 'Seat holds: 90 s', '', 'ada') + hit('doing', '#3055', 'issue', 'Seat hold release', '', 'you')
     + '<div class="gh">Notes 1</div>' + hit('note', 'note', 'eng/', 'Seat hold design', '', '2d') + '<div class="gh">Code 1</div>' + hit('linked', 'code', 'scope', 'export/upload (seat, hold)', '', '14 files'),
     'Release 3. Search in Everything: results in groups of up to 5, not one fused rank: Messages 4 (at least the 3 that from:ada finds), Pull requests and issues 2, Notes 1, Code 1. Each header count equals its rows. The scope chip is a button; Tab does not switch it.')
peek_other('peek-file', 'File · #harbor', 'slack:file:F0SEAT', [('From', 'Pat'), ('Name', 'fare-table.png'), ('Size', '184 KB')], '<div class="eyebrow">Thumbnail</div><div class="fileth">fare-table.png</div><div class="filechip"><span class="g note"></span>fare-table-diff.pdf · 14 MB</div><div class="fbox att" style="display:flex;gap:10px;background:var(--err-soft);border-radius:6px;padding:8px 12px;margin-top:6px;font-size:12px"><span class="g fail" style="margin-top:2px"></span><div>fare-table-diff.pdf is 14 MB, over the 10 MB limit. Open it in Slack.<div class="mono" style="font-size:11px;color:var(--ink-subtle)">Details ▸ SND-MSG-010 · too large</div></div></div>', 'Release 3. File peek: a thumbnail 96 pixels high, a file chip with name and size, and a file that is too large: 14 MB, over the 10 MB limit. No Retry, because a retry cannot change the size. Button: Open in Slack. Details: SND-MSG-010.', rel=R3)
peek_other('peek-app-message', 'CI bot · app message', 'slack:app:B0CI', [('App', 'CI bot'), ('Place', '#harbor'), ('Time', '09:10')], '<div class="blkmsg"><div><b style="font-weight:600">Build 8861 failed</b></div><div class="ctx">agentic-workflow · main · 3 tests</div><hr><div style="color:var(--ink-muted)">Retry the nightly export job or open the run.</div><div style="display:flex;gap:8px;margin-top:6px;align-items:center"><span class="btn off">Retry</span><span class="btn"><span class="lk">Open run</span> ↗</span></div><div style="font-size:11px;color:var(--ink-subtle);margin-top:4px">Retry is off: press it in Slack. Link buttons open the URL.</div></div><div class="blkmsg" style="border-color:var(--att)"><div style="color:var(--ink-muted)">Unknown block (type: input): shown as text. Open in Slack to see it as sent.</div></div>', 'Release 3. App message with blocks: a section, a context line, a divider and an actions row. Retry is disabled with its reason: press it in Slack. Open run is a link button that opens the URL. An unknown block shows as text plus Open in Slack.', rel=R3)
