# Sindri list grammar: row, peek, full (#2978)

One way to show a list of things, used on every page. `grammar.css` (same folder) is the only source of sizes and colours; a mockup
links it with `<link rel="stylesheet" href="grammar.css">` and adds layout only. If a page needs a size or colour the CSS doesn't have,
it's a question for the grammar, not a local fix.

## The frame (1440x900)
Top bar 40 · page header 48 (title 15/22, count, view tabs, filter at the right) · body · key hint footer 28.
So the body is 784 px tall: with 8 px padding and 2 group headers (28 each), that's **22-23 rows at 32 px on one screen**,
25-27 with one group. That is the density target: 25-30 rows, never 6 cards.

## Row (32 px compact, the default; 40 px comfortable via `.comfortable` on .app)
`[glyph 16] [title, flex, min 200] [3-5 facts, fixed columns 104 or 136] [age 44, right-aligned] [waits on 112]`, 12 px column gap,
24 px side inset. Columns line up down the whole list and between pages: the eye scans a column, not a sentence.
- **Glyph:** the state, drawn, 14 px. `linked` (two chained links, ink-subtle; never a ring, so it never reads as a PR, #3581) = a confirmed link or a saved thing: neutral, never green. Colour means status only: todo (open ring, grey), doing (half ring, primary), done (check, green),
  draft (dashed ring), in review (3/4 ring, primary; never the doing half ring), needs you (diamond, attention), failed (x, error), parked (pause ring, attention), agent running (arc, primary),
  message (bubble with a tail, never a box: `x` selects, so a box reads as a checkbox), mention (dot, primary), PR open (ring with dot, green), PR draft (same, grey), merged (violet disc), CI running (dotted ring, attention).
- **Title:** the only 13 px ink text in the row. The id (mono 12, ink-3) sits LEFT of the title on every row kind, never in a fact column
  or as a chip (`#4160`, `BIL-301`, `scope_7f3k2`), a sub-line (12, ink-3) after it on the same line.
  Unread = weight 500; read = ink-muted.
- **Facts:** 12/18 ink-subtle, one per column, the SAME columns for every row of a list. **One fact kind per column**: a column is
  "review count" or "CI" or "priority", never a mix. A list of mixed row kinds (Home) shows only the columns every kind has
  (source, ref, ask); the rest goes in the peek. An empty fact is a blank cell, never `–` or `no PR`. Facts are text, not bordered
  chips, except a status chip; every fact ellipsizes (no hard clip). Kinds: chip (label, CI state), a 56x4 progress bar,
  a diffstat (+120 −8, mono), a mono path or branch, a person (16 px avatar + name), a count. Status colour only on status facts.
- **Age:** "4m", "2h", "3d", ink-subtle, right-aligned. Ages, ids and counts are ink-subtle, never ink-3: ink-3 is 4.49:1 on canvas
  and 3.4:1 on the selected fill; ink-subtle is 6.13:1 and 4.64:1 (AA for 12 px). ink-3 stays for captions on canvas only.
- **Refs** (repo, branch, path, run name) take the 136 column and shorten in the MIDDLE (`acme…/exports`, `run/…/plans`): the end is
  what tells two rows apart. Names and sentences shorten at the end.
- **Progress** is a 56x4 bar plus a count (`6/9`, `4/7`, `41%`), no words. A bar + count takes the 136 column, and the count is at most 5 characters (`6/9`, `41%`): a big count shows as a percent, with the full count in the sub-line (`reading 1,204 of 3,880 files`). A step name goes in the peek.
- **A column with one value in a whole group says nothing.** The group header says it once ("Needs you · all waiting on you"), and the
  column shows the next most useful fact. In Needs you that is the ask, as a verb: `reply`, `review`, `unblock`, `decide`, `retry`.
- **Ids by kind:** `#4182` = PR, `BIL-301` = issue (KEY-N), `r-0412` = run, `j-318` = job, `dep-77` = deploy. `#` means a PR only.
- **Waits on:** who has the next move: **you** (ink, bold: the one loud fact on the row), a person, reviewer, CI, agent (square avatar, primary-soft).
  A done row (done, merged, closed) has no waits-on: the cell is blank.
- **States:** rest transparent · hover s2 · keyboard cursor = 1 px primary-hi inset ring · selected (the row being peeked or open) =
  primary-soft fill + 2 px primary bar at the left + the cursor ring, on every page (the cursor is on the peeked row). No borders between rows; a 28 px group header separates groups.
- **Peek open:** the list is 1000 px wide at 1440, so a row keeps its first 3 facts. Write facts most important first; the 4th and 5th
  carry `.drop-peek` (or set `--facts-peek` when the first fact is wide). Padding, gaps and the title minimum never shrink.
- **Row actions:** on hover or cursor, 2-3 small buttons replace the right end of the row (Reply, Snooze, Open...). Never a menu for the main one.
  They are pointer targets; from the keyboard a write goes through `.` or Ctrl+Enter (Keys).

## Group header (28 px)
Sentence case, 12/500 ink-subtle, count in ink-subtle (400), chevron ▾ open / ▸ closed (never letters), optional chevron (collapsible), optional right-side summary. Never all caps.

## Peek (Space)
Tap Space to toggle the peek; hold Space to show it until you let go (Linear's hold-to-peek).
One row kind = one peek, wherever the row is listed: a PR peeked from Home is the Reviews peek, footer and all
(`Review… Ctrl+Enter`, `Open Enter`). A peek footer never puts a write on `.`.
A 440 px pane on the right (s1, left hairline). The list stays, narrows, keeps focus; j/k moves the peek with the cursor. Contents:
header 48, always `h2 title · id (mono) · ⤢ open full · × close` (icon buttons, same on every page), fields as 2 columns of 28 px rows (label ink-3 / value ink-muted), then the body
(conversation, diff summary, description), then 2-3 actions at the bottom: each a button with its key as a kbd inside (`Open diff [Enter]`,
`Review… [Ctrl+Enter]`), the main one primary; no plain-text actions, no keys in parentheses. Esc closes. Lists never navigate away for a look.

## Full (Enter)
The working surface (thread, diff, editor, canvas) fills the body; the header row becomes a breadcrumb (`Reviews / #2905 System grid`).
Esc goes back to the list with the cursor on the same row.

## Keys (same on every list; shown in the footer)
j/k move · Space peek · Enter open · Esc back · / filter · c create · . actions · x select · Ctrl+K palette.
The footer prints exactly that, in that order and spelling, on every page (`j/k` as one key, never `j` `k`), then `.keys-div`,
then the page's own keys.
Rules (meta, on #2978): the grammar keys are the same on every list. A bare letter may differ per surface only when it opens
something cancellable (a menu, a composer, a picker). A bare letter NEVER commits a write: approve, merge, pin, drop, state changes go
through `.` (the actions menu) or the Ctrl+Enter submit menu. Page-specific keys go after a divider in the footer
(e.g. Reviews: r opens the reply composer).
Keys live in ui's `RowKeys` (`Views/Controls/RowKeys.cs`, #2952); `RowList` is built on it and has no key handler of its own.

## Home is the one inbox
Three sections, ranked by what unblocks the most, each row actionable in place:
1. **Needs you** — mentions and DMs awaiting a reply, review requests, blocked agents, failed runs, issues assigned today.
2. **Moving** — what agents and CI are doing for you, one line each, collapsed.
3. **Since you last looked** — a ranked brief, 5-10 short lines written by an agent, each with its links.
If Home is empty, you're done.

## Empty and error states (#3019)
Every state a page can be in has a screen, and every screen has exactly ONE primary button that fixes it. Two shapes; the shape is
`.empty` and `.state-line` in `grammar.css` ("Empty and error states (#3019)").

1. **Page empty state** (`.empty`, replaces a list body). Left-aligned in the body at the 24 px row inset, never centred in a card.
   24 px glyph in its status colour, title 15/22 ink, one sentence 13/20 ink-muted that says what happens on click, ONE primary
   button with its key as a kbd (`Start Sindri [Enter]`), optionally ONE ghost button. Never a CLI line and never a raw code: both
   go behind a small `Details` disclosure (`.details`, mono 12).
2. **Inline state line** (`.state-line`, one 32 px row). Inside a list, a group, or under the top bar: glyph, one sentence, and the
   fix button at the right end where row actions sit. Tint: attention (stale, stalled, expired) or error (can't be read); a
   progress line (scanning) is plain s1. Used for "can't be read" (Retry), "a newer Sindri is installed" (Restart Sindri),
   "connection expired" (Reconnect Linear) and "stalled" (Rebuild).

Rules:
- **One primary, the verb of the fix:** `Restart Sindri`, `Retry`, `Reconnect Linear`, `Build the map`, `Start Sindri`, `Run hello-sindri`.
  Not "OK", not "Fix", not "Try again". A second button is ghost and is never the fix.
- **Failed is not empty.** A failed read never shows the empty text ("Nothing running."). Failed = error tint + Retry; empty = the
  `.empty` shape, with the button that gets the first thing in.
- **The old-serve case is one shared line**, not a fix per page: "The Queue list can't be read from this Sindri. It is older than the
  app." with `Restart Sindri`, the same line and button on every page that reads a list (qa found it on 8 pages).
- **Retry** sits on every read failure; the code is behind `Details ▸` at the end of the line, never in the sentence.
- **Copy:** sentence case, plain words, no exclamation marks, no "Oops". Product words; a CLI command appears only in Details.
- **Accounts:** a not-yet-connected account is a `todo` open ring (not urgent, so no needs-you diamond). Once connected the row gets the `linked` glyph and "connected as jthorare", with no button.
- **First-run screens** (`setup-repos`, `setup-connect`) use the row grammar with a checkbox (`.g.box`) or a state glyph in the glyph
  column, a `.state-line` for progress, and a `.actionbar` at the bottom: ghost left of the primary, count at the far left.
- **Setup steps** are a RowList with always-visible fix buttons: `.row.fix` (glyph, title + why as sub-line, fact, button column).

Drawings: `design/first-run-3019/` (`not-running`, `setup-repos`, `setup-connect`, `setup-steps`, `states`).
