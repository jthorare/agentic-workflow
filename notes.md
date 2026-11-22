# Home frames (#3733, D1): notes (current after round 5b)

## Current rules (round 5b; they win over any older line below)
1. **Shell = ../projects-3717.** Pill "Sindri running" (or "Not running" when stopped), search "Search…" at 1440 and 1000, the 1000 top bar rules (cmdk 150, nav padding 7, gap 8), tab counts 6 px from the label, footer and keycaps 11 px. Nav counts: Home is the All tab's number (none when 0); Reviews 4, Items 31, Agents 3 are **other pages' totals**, copied from Projects (guess 59): they are not Home's own Reviews and Agents splits, so "Reviews 4" next to an empty Reviews split is not a miscount. **Home wins** where its spec sets its own: rows are 32 px compact (spec 7.1, D3; Projects rows are 40 comfortable) and all content text is at least 12 px (the footer and keycaps stay at the shell's 11).
2. **One table for a row's buttons and footer keys:** `UI` in gen.py, keyed by `ui_id(row)`. The cursor row and a hover row draw their buttons from it; the footer after the divider is the cursor row's keys from it. **The footer has two halves (r6 H1):** before the divider the grammar keys `j/k Space Enter Esc / Ctrl+K ?` on every frame, after it the cursor row's keys (the same as Projects). Only a menu or picker (spec 7.4: hand-picker x2, snooze-picker, submit-menu, action-panel), the `?` sheet and the filter text field (both modal) swap the whole footer and lack the grammar half. The Done list is a page and keeps it (r7 H2). Allowed exceptions to "the keys after the divider are the cursor row's" (each a `kmode` in gen.py, checked in measure.js): a menu is open, focus is in the peek's options, the thread failed, Sindri is stopped, the Undo toast (it adds `Ctrl+Z undo` and drops the run's own `Ctrl+Enter retry run` and `t` for the 5 s the toast shows, so the one way back is on screen), the `?` sheet, a header or brief row has the cursor, no cursor row, the Done list, focus in the filter box, focus on "b N more". In a peeking list the buttons live in the peek, not in the row.
3. **Connections are explicit data per frame** (`conn` in `#rowdata`: per source `on`, `off` or `later`; CONN_SEED, CONN_GH, CONN_FULL in gen.py). `off` = no rows from it and one Connect row (open or Done); `on` = no Connect row, even with no rows; `later` = no rows and no Connect row. The seed has GitHub off, Linear on with nothing assigned, Slack later; linear-off-1440 is the seed with Linear off (All 8, a Linear Connect row). A split whose source isn't connected says "Connect GitHub to see reviews" (Messages: "Connect Linear to see mentions") with a Connect button, never "Nothing in <split>".
4. **New band.** A row entering above the cursor scrolls the list by the band's height (28 header + 32 per row + 8), so the cursor row keeps y 124 (checked). The stuck bar reads "New n above | Needs you m". The New half has no chevron and never folds while it holds a row (spec 7.4). Enter, Left and Right on the bar act on the "Needs you" half: Left folds it, Right opens it. A click on the New half does nothing (guess 64).
5. **Waiting since** has one format in every peek: weekday, clock time, age ("Fri 09:12, 18m"), from the row's minutes. Reason chips are lower case; weight 500 is for "blocks N" only. Sessions are rounded squares, people are circles.
6b. Connect verbs: a setup row (`system`) uses `Ctrl+Enter connect`; a failed or stale act uses `. r connect`. They are two row kinds, so two hints.
6c. At 1000 px the footer keeps full words because they fit (checked); spec 7.1's shrink rule (`h snooze · t hand · e done · ? all keys`) is only for when they don't.
6d. A peek card shows what fits: the qa terminal is 28 lines at 1440 and 14 at 1000.
6e. Known gaps, accepted: a frame for "Mark all seen" (spec 5.4), `home.brief: off`, and Moving with a CI line (spec 6.2).
6a. `.` opens the row's action panel on every row, so the footer never hints it alone; the `?` sheet lists it. The only `.` hints are the combined `. r retry` and `. r connect` on a failed act.
7. **Populated splits** (work-tab, agents-tab, reviews-tab): a split filters New, Needs you and Moving to its kind; the brief and the Todo line show on All only (spec 7.1).
6. **Keys, as the code stands at origin/main (home lane answers 6, 8, 9):** j k, arrows, Home End, Space, Enter, Esc, Left Right and `.` work on a `RowList` now; bare h t r e b o and `/` reach `ShellViewModel.Press` and need only U1's handlers; `?` is global and opens the shell's one sheet, to which Home adds one section; Ctrl+Z works now; `R`, `1` to `5`, `[` `]` and Ctrl+Enter need new plumbing (U1 extends `KeyName`, U2 adds Ctrl+Enter). `o` has no clash (the Todo panel owns it only while focus is inside the panel). Bare A, X and Y act on Home today and U1 turns them off on the rows page, so **no frame hints A, X or Y** (checked). N, G and C stay global and are not Home hints.

Clock: Fri 20 Nov 2026, 09:30. Seed: 2 decisions, 8 todos, 7 agent sessions (1 asking), 1 failed run, no connections.
Needs you has 7 rows: qa asking, 2 decisions, the failed run, the overdue and due-today todos, and the `system` row "Connect GitHub to see reviews". The other 6 todos sit in "Todo, 8 open"; the 6 other sessions in "Moving: 6 agents" (collapsed). Brief = 2 fact lines, no model.
Order (spec 4.2): blocks, weight, age old first. qa has blocks 1; the rest 0: asking, decisions (1d, 2h), failed run, overdue, due today, setup.
Split counts: All 7, Reviews 0, Messages 0, Work 5 (2 decisions, run, 2 todos), Agents 1. `system` is in no split.

## Sizes (measure.txt holds every number, `frame, item, value, pass|fail`)
- Frame 1440x900: top bar 40, header 48, body 784, footer 28. 1000x700: body 584.
- Row 32 (40 comfortable and at scale 1.3), group header 28 (36 at 1.3), inset 24, gap 12.
- Columns: glyph 16, title flex (804 at 1440, 364 at 1000), why 104, object 104, from 136, age 44, waits-on 112.
- Every title, sub-line, brief header and the Todo line text start at x 52; every glyph centre is at x 31 (32 for the message bubble).
- Peek 440, header 48, field rows 28; list 1000 wide beside it at 1440, 560 at 1000.
- Contrast: lowest 4.60:1 (picker placeholder); no text under 12 px.
- Shots: 1440x900, `*-1000` at 1000x700. Measure runs in chrome at the same window size (shoot.sh now passes `--window-size` to the DOM dump too).

## Frames
- default-1440 / first-paint-1440: the ranked list, cursor on row 1, no New band. first-paint is the same data on open: every row in its ranked place, no "Nothing needs you" over rows.
- default-1000: same at 1000x700; the footer is the cursor row's keys (the same as 1440).
- default-full-1440: 21 rows of spec 3.2 kinds (made up, GitHub connected so no Connect row, caption "Full load, not the pass seed"); 21 rows in view, the brief and Todo line below the fold.
- d3-17-1440: the D3 proof: exactly 17 rows (the first 17 of the full load), Moving, a facts-only brief (2 lines + "b 3 more lines") and the Todo line, all above the footer (Todo line ends at y 867). Tab counts are computed from row kinds.
- agent-asking-1440: cursor on qa, buttons Reply / Terminal; footer `r reply · o terminal · e done · h snooze` (no `t`, spec 5.1).
- run-failed-1440: cursor on the run, buttons Retry run / Open run; footer `Ctrl+Enter retry run`.
- row-failed-1440: the Retry run act failed; attention sub-line "failed: <reason>", Retry / Discard; footer `. r retry · . actions`.
- new-band-1440: two rows arrived after the cursor moved; the list scrolled 100 px so the cursor row stays at y 124; one sticky bar "New 2 above | Needs you 5".
- new-band-top-1440: scrolled to the top: New 2 (qa, run) with "new" in ink in the age column, cursor on the first new row only.
- snooze-woken-1440: qa woke early (asking again): it enters New above the cursor; the list scrolled 68 px under the sticky bar "New 1 above | Needs you 6" so the cursor row stays at y 124. snooze-woken-top-1440 is the same data scrolled back to the top: the "back" mark shows and the cursor row is 68 px lower because the owner scrolled.
- snoozed-1440: Snoozed toggle on; 3 rows with "back Mon 9:00" / "back Nov 27" in the waits-on column.
- snooze-picker-1440: presets 1 to 4 with their times, field 5; footer swaps to `↑/↓ move · Enter pick · Esc cancel`.
- empty-1440: "You're done." 20 px, "3 snoozed, the next back Mon 9:00", the brief; no Moving.
- empty-stale-1440: state line "Agent sessions last read 12 min ago" + Refresh; "Nothing waiting in what I could read" (no sub-line, guess 18).
- brief-1440: summary line, 2 fact lines, `b 3 more lines`.
- brief-states-1440: the four spec 6.1 cases stacked for review, not one screen: model limit + Resume, no writer + Sign in, failed + Retry, 0 lines "Nothing changed."
- stopped-1440: cached rows, header line "Sindri isn't running · showing 09:28" + Start Sindri, pill "Not running", dashed disabled buttons, footer reason.
- stale-source-1440: GitHub is connected for this frame only (so no Connect row). 32 px state line "GitHub last read 12 min ago" + Refresh under the header; GitHub rows muted (title and age ink-subtle).
- lanes-glyphs-1440: illustration, not the seed. Lane-recycle rows use the needs-you diamond, ready-to-merge rows the PR-open ring; #3099 is being merged by meta (waits-on "merging, queue 1").
- connect-1440: cursor on the system row with Connect; footer `Ctrl+Enter connect`.
- loading-1440: a "Needs you" header and 6 skeleton rows at the real rows' y; header and footer paint at once.
- peek-1440: Space on qa: 440 px peek, fields, 28 terminal lines, pending question, reply box, Reply / Jump to terminal.
- peek-decision-1440: question and three named options, Answer / Decline / Copy command.
- peek-1000: peek at 1000x700, list 560 (both facts drop), 14 terminal lines.
- density-comfortable-1440: rows 40.
- scale13-1440: text 1.3x, row 40, group header 36, columns unchanged in px.

## Round 2 changes (after looking at every PNG)
1. Captions moved from over the footer to a dashed note in the top bar; now only default-full and d3-17 keep one (see 13). The rest moved here.
2. new-band: scroll 92 -> 100 so the cursor row is at y 124 again; no half-hidden Needs you header under the stuck New header.
3. new-band-top: only the first new row has the cursor; new rows say "new" in the age column (spec 4.3).
4. row-failed: the "failed: <reason>" text was missing; now an attention sub-line.
5. Stopped: the pill said "Connected" next to "Sindri isn't running"; now "Not running". Footer says why actions are off.
6. stale-source: an empty band of ~200 px between the state line and the list (grid stretch); muted rows were 4.49:1, now 5.19:1; Work count 4 -> 3.
7. snoozed: muted rows were 4.49:1, now normal ink; Work 3 -> 2. default-full: Messages 2 -> 4, Agents 3 -> 4 (counted by kind).
8. snooze-woken: two seed rows had gone missing; now the full seed (All 7).
9. Peeks: the terminal's last line was cut in half; peek header 49 -> 48; 11 px eyebrows -> 12; a second selection mark (left bar) removed; Blocks names the dependent; peek-1000 lost the brief, now shown.
10. Brief rows' glyphs sat 2 px left of the row glyphs; the Todo line text sat at x 53. Both now on the shared edges.
11. Keys: `r` was used for "retry" (spec: `r` is the reply composer only) -> `Ctrl+Enter retry run`; `Del discard` (not in the spec) -> `. actions`; empty-stale footer drops row keys.
12. scale13: mono facts and ids stayed 12 px; now scaled with the rest.

## Round 2b (home lane's review-r2-findings.md)
13. Captions only on default-full and d3-17 (the lead's decision); lanes-glyphs, stale-source and brief-states say what they are here instead.
14. new-band: "2 above" on the stuck New header. stale-source: the two due todos are back (All 9, Work 5).
15. brief: summary copy no longer says "Nothing else changed" above "b 3 more lines"; count 6. empty: brief lines are finished things.
16. scale13: avatars 21 px with 15.6 px letters. lanes-glyphs: merge-queue waits-on. loading: header so rows don't jump. empty-stale: "cannot".
17. New frame d3-17-1440 for the row budget (guess 27).

## Round 3 (review-r3.md)
gen.py is rewritten around one data model (SEED, FULL, TODOS, SESSIONS_RUNNING, FACTS). Every number on screen comes from the rows a frame draws: tab counts (`counts_of`), group counts, the window title's "n new", the Snoozed toggle, Moving, the Todo line, the brief count and "b N more". Each frame embeds its row data as JSON (`#rowdata`); measure.js recounts the DOM against it (`count …` lines) and checks.py exits 1 on any mismatch. A hand-edited "Todo, 9 open" fails (tested).
Rank follows spec 4.2 in `rank()` and the why chip says "blocks N" for 2 or more (`why_text()`).
Data states that differ from the seed frames: brief-1440 and default-full (5 facts and a summary), empty-1440 (Connect row Done, finished facts), stale-source and connect-stale (GitHub connected), stopped (cache 09:28).
16 new frames: d3-16-summary, updating, read-failed, tab-reviews-empty, tab-messages-empty, still-sending, changed, connect-stale, peek-failed, done-list, done-list-empty, moving-open, moving-empty, hand-picker, hand-picker-empty (plus d3-17 from round 2b). (the frame count is in the latest report.)
Captions: default-full, d3-17, d3-16-summary and brief-states (a review sheet).

## Round 4 (review-r4.md)
- "b N more" moved onto the brief header (right side); d3-17 is the summary case, 17 rows, Todo line bottom 872. d3-16-summary is deleted. 41 frames.
- new-band: one sticky bar "New 2 above | Needs you 5"; cursor row at y 124 (scroll 100).
- hand-picker-empty: data state with no sessions (qa gone, Moving "Nothing in flight").
- Brief references: brief lines carry `data-ref`; measure.js checks each against the frame's rows, Done rows and known runs (an open failure must be a row). It caught lanes-glyphs naming r-0412 with no row; that brief now names two ok runs.
- done-list is a separate data state (Re-scope and the Connect row were done), like brief-1440.

## Round 5
New frames from the spec's own sections: submit-menu (7.2), action-panel (7.4), undo (5.4), keys-sheet (7.4), peek-pr / peek-lane / peek-message / peek-issue (7.2), folded (6.2), brief-cap (6.1). 51 frames.
Smalls: the disabled Answer button has no key hint; the sticky New bar's only chevron folds Needs you.
The full load lost its Connect row (GitHub is connected there). The brief summary line spans the facts columns so it never cuts at 1000 px.
A folded header counts against the row data, not drawn rows (measure.js).

## Round 5b (qa round 4: 4 holds, 14 nits; the home lane's answers)
Holds: one table for buttons and footer keys; snooze-woken keeps the cursor y; the Connect row follows the connection data; a split with a source that isn't connected offers Connect. Nits: the shell matches Projects; long-title frames; hover and filter-focus frames; the combined New bar's divider is 3.15:1; hand-picker wording; "failed: run paused"; one Waiting since format and one terminal label; "overdue" is no longer weight 500; the brief-ref check exempts review sheets by an explicit flag; these notes; avatars and "dm"; Moving lines name the lane once; Messages split wording; peek-failed disables Reply; the empty Done list drops row keys.
New frames: snooze-woken-top, hover, filter-focus, long-title-1000, long-title-peek-1000, brief-more-hover, brief-more-focus. The `?` sheet is redrawn as the shell's global sheet with a Home section.

Round 5c (review r6): footer grammar half back on every frame; Work, Agents and Reviews tabs with rows; explicit connection data and a Linear-off frame; the `?` sheet gains Space, Tab, up/down, `o`, Ctrl+Z and a Done list row, and every footer key must be in it; `. actions` hints dropped; Done list nav badge and Ctrl+Z wording; one DM time; the sticky-bar guard is flagged by data (`cursor_kept`).

Round 5d (review r7): the `?` sheet is drawn from its top (shell rows, then Home) and checked from its drawn rows plus a Done list row; the Done list keeps the grammar footer; one OFFER string per Connect source, checked on the setup row and every tab offer; Messages offers each source that is off; empty-1440's brief line hints e and h; folded header hints only Right; the Todo line counts 6 other todos plus the due ones (d3-17 and lanes-glyphs say 6).
