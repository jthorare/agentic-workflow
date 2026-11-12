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
- **Glyph:** the state, drawn, 14 px. Colour means status only: todo (open ring, grey), doing (half ring, primary), done (check, green),
  draft (dashed ring), needs you (diamond, attention), failed (x, error), parked (pause ring, attention), agent running (arc, primary),
  message (bubble), mention (dot, primary), PR open (ring with dot, green), PR draft (same, grey), merged (violet disc), CI running (dotted ring, attention).
- **Title:** the only 13 px ink text in the row. An id (mono 12, ink-3) may sit before it, a sub-line (12, ink-3) after it on the same line.
  Unread = weight 500; read = ink-muted.
- **Facts:** 12/18 ink-subtle, one per column, the SAME columns for every row of a list. Kinds: chip (label, CI state), a 56x4 progress bar,
  a diffstat (+120 −8, mono), a mono path or branch, a person (16 px avatar + name), a count. Status colour only on status facts.
- **Age:** "4m", "2h", "3d", ink-3, right-aligned.
- **Waits on:** who has the next move: **you** (ink, bold: the one loud fact on the row), a person, reviewer, CI, agent (square avatar, primary-soft).
- **States:** rest transparent · hover s2 · keyboard cursor = 1 px primary-hi inset ring · selected (the row being peeked or open) =
  primary-soft fill + 2 px primary bar at the left. No borders between rows; a 28 px group header separates groups.
- **Peek open:** the list is 1000 px wide at 1440, so a row keeps its first 3 facts. Write facts most important first; the 4th and 5th
  carry `.drop-peek` (or set `--facts-peek` when the first fact is wide). Padding, gaps and the title minimum never shrink.
- **Row actions:** on hover or cursor, 2-3 small buttons replace the right end of the row (Approve, Reply, Snooze...). Never a menu for the main one.

## Group header (28 px)
Sentence case, 12/500 ink-subtle, count in ink-3, optional chevron (collapsible), optional right-side summary. Never all caps.

## Peek (Space)
A 440 px pane on the right (s1, left hairline). The list stays, narrows, keeps focus; j/k moves the peek with the cursor. Contents:
header 48 (title 15/22, id, open-full and close), fields as 2 columns of 28 px rows (label ink-3 / value ink-muted), then the body
(conversation, diff summary, description), then 2-3 actions at the bottom. Esc closes. Lists never navigate away for a look.

## Full (Enter)
The working surface (thread, diff, editor, canvas) fills the body; the header row becomes a breadcrumb (`Reviews / #2905 System grid`).
Esc goes back to the list with the cursor on the same row.

## Keys (same on every list; shown in the footer)
j/k move · Space peek · Enter open · Esc back · / filter · c create · . actions · x select · Ctrl+K palette.
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
