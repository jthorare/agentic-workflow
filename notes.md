# Home frames (#3733, D1): notes, round 2

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
- default-1000: same at 1000x700; footer keys shrink to `h snooze · t hand · e done · ? all keys`.
- default-full-1440: 22 rows of spec 3.2 kinds (made up, caption "Full load, not the pass seed"); 22 rows in view, the brief and Todo line below the fold.
- d3-17-1440: the D3 proof: exactly 17 rows (the first 17 of the full load), Moving, a facts-only brief (2 lines + "b 3 more lines") and the Todo line, all above the footer (Todo line ends at y 867). Tab counts are computed from row kinds.
- agent-asking-1440: cursor on qa, buttons Reply / Terminal; footer `r reply · o terminal · e done · h snooze` (no `t`, spec 5.1).
- run-failed-1440: cursor on the run, buttons Retry run / Open run; footer `Ctrl+Enter retry run`.
- row-failed-1440: the Retry run act failed; attention sub-line "failed: <reason>", Retry / Discard; footer `. r retry · . actions`.
- new-band-1440: two rows arrived after the cursor moved; the list scrolled 100 px so the cursor row stays at y 124; "New 2" sticks at the top.
- new-band-top-1440: scrolled to the top: New 2 (qa, run) with "new" in ink in the age column, cursor on the first new row only.
- snooze-woken-1440: Re-scope woke early: in New, age "back", sub-line "back early: new reply"; Needs you keeps the other 6.
- snoozed-1440: Snoozed toggle on; 3 rows with "back Mon 9:00" / "back Nov 27" in the waits-on column.
- snooze-picker-1440: presets 1 to 4 with their times, field 5; footer swaps to `↑/↓ move · Enter pick · Esc cancel`.
- empty-1440: "You're done." 20 px, "3 snoozed, the next back Mon 9:00", the brief; no Moving.
- empty-stale-1440: state line "Agent sessions last read 12 min ago" + Refresh; "Nothing waiting in what I could read" and what Refresh does.
- brief-1440: summary line, 2 fact lines, `b 3 more lines`.
- brief-states-1440: the four spec 6.1 cases stacked for review, not one screen: model limit + Resume, no writer + Sign in, failed + Retry, 0 lines "Nothing changed."
- stopped-1440: cached rows, header line "Sindri isn't running · showing 09:02" + Start Sindri, pill "Not running", dashed disabled buttons, footer reason.
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
16 new frames: d3-16-summary, updating, read-failed, tab-reviews-empty, tab-messages-empty, still-sending, changed, connect-stale, peek-failed, done-list, done-list-empty, moving-open, moving-empty, hand-picker, hand-picker-empty (plus d3-17 from round 2b). 42 frames in all.
Captions: default-full, d3-17, d3-16-summary and brief-states (a review sheet).

## Round 4 (review-r4.md)
- "b N more" moved onto the brief header (right side); d3-17 is the summary case, 17 rows, Todo line bottom 872. d3-16-summary is deleted. 41 frames.
- new-band: one sticky bar "New 2 above | Needs you 5"; cursor row at y 124 (scroll 100).
- hand-picker-empty: data state with no sessions (qa gone, Moving "Nothing in flight").
- Brief references: brief lines carry `data-ref`; measure.js checks each against the frame's rows, Done rows and known runs (an open failure must be a row). It caught lanes-glyphs naming r-0412 with no row; that brief now names two ok runs.
- done-list is a separate data state (Re-scope and the Connect row were done), like brief-1440.
