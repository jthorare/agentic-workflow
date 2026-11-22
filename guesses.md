# Guesses (Home D1, #3733): product facts the spec leaves open. One line each; the owner lane confirms or corrects.

## Every frame
1. Why chip: "blocks N" when the row blocks 2 or more (spec 7.1's frame and 4.2: "The owner reads them on the row's why chip ("blocks 3")"), else the reason word; qa (blocks 1) reads "asking", as in spec 7.1's frame. "blocks N" is weight 500 ink so the rank reason reads first.
2. Reason words "overdue" and "due today" for todo rows; the spec's word list has only "due".
3. Objects: decisions `repo web` / `repo billing`, the system row `github`, todo rows `todo` with an empty from cell, the failed run `run nightly` (id `r-0412` left of the title).
4. Decision rows' from is a person (Mira, Leo); the spec names no person for a Sindri decision.
5. Footer and kbd text are 12 px (grammar 11) so no text is under 12 px.
6. Cursor: one 2 px primary-hi ring inset 16 px from each list edge, on rows and on group headers; no left bar.
7. Row buttons (spec 5.1) take the age and waits-on cells and the from cell stays, except on a row whose act failed: there Retry (or Connect) and Discard take the from and age cells so the waits-on cell can keep "failed: <reason>" (spec 5.2 puts the reason in waits-on). See 13.
8. Footer key for a primary action with no bare key is `Ctrl+Enter <action>` (spec 7.4 submit menu).
9. Brief facts on the seed frames: "Run r-0412 failed at 09:10." and "Run r-0409 ended ok at 09:05." (spec 6.1 "a run ended"); runs r-0406..r-0409 are invented.

## Per frame
10. default-1000: the third fact stays (list 1000 px; spec drops it under 840). The drop shows on peek-1000 (list 560: both facts and the empty waits-on column go).
11. new-band-1440 (decided by the lead from spec 4.3): one sticky bar reads "New 2 above | Needs you 5" and the cursor row stays at y 124. Open: the exact bar wording.
12. snooze-woken-1440: qa's row woke early with "back early: asking again" (spec 5.3: the session asking again).
13. row-failed-1440 / connect-stale-1440: the failed act's reason sits in the waits-on cell as spec 5.2 says, in a short form that fits 112 px ("failed: paused", "failed: signed out"); Retry (or Connect) and Discard take the from and age cells on that row only. The full adapter reason is in the peek.
14. row-failed-1440: footer `. r retry · . actions` (spec 5.2: Retry is `.` then `r`; Discard sits in the `.` panel).
15. snoozed-1440: the Snoozed toggle on shows Needs you (4 open) and then a Snoozed group (3); tabs count open rows only. The cursor on a snoozed row shows `. unsnooze` and `h snooze again` (palette id `home.unsnooze`).
16. snooze-picker-1440: the picker opens under the cursor row, 372 px wide; dates for Fri 20 Nov 09:30.
17. empty-1440: the Connect row was Done for this data state (a done row doesn't count, spec 5.5). Brief facts are finished things ("Run r-0412 passed on retry at 09:20."). The Todo line is not drawn (spec 5.5 lists only "You're done.", the snoozed line and the brief).
18. empty-stale-1440: the stale source is Agent sessions; no sub-line, only the spec's heading, the stale line and Refresh.
19. brief-states-1440: a review sheet of four spec 6.1 states, captioned; tabs count the rows it draws (0).
20. stopped-1440: pill "Not running"; cache time 09:28, so every row and fact on screen predates it; dashed disabled buttons; the footer gives the reason.
21. stale-source-1440: GitHub is connected for this data state, so there is no Connect row; muted rows use ink-subtle (5.19:1) for title and age only (spec 7.3), the why chip keeps its ink.
22. default-full / d3: Slack keyword and thread-reply rows name the channel (`channel ops`, `channel infra`); spec 7.1 lists only lane, repo and dm.
23. Row budget (decided by the lead from spec 6.1 and D3): the brief is its default 3 lines (summary + 2 facts) and "b 3 more" sits right-aligned on the brief header instead of its own 28 px line. 17 rows fit with Moving, the brief and the Todo line; the Todo line ends at y 872 (d3-17-1440). The same header placement is used on every frame with more facts.
24. Brief references: every item a brief line names resolves in the frame's data. An open failure (red x) must be a row of that frame; a finished item may be a row, a Done row, or a workflow run Sindri knows (spec 6.1: a link resolves to "a row or object id"). checks.py fails otherwise.
25. lanes-glyphs-1440: meta is merging both queue entries; waits-on reads "merging, queue 1" (#3099) and "merging, queue 2" (#3101), short for spec 5.1's "meta is merging (queue position N)". Neither row offers Merge.
26. loading-1440: a "Needs you" header with no count over 6 skeleton rows (rows land at the real y 124); tabs and the Snoozed toggle show no numbers until known.
27. brief-1440 / default-full: a different data state from the seed frames: 5 facts and a written summary ("The nightly index sync failed at step 3; four other runs ended ok."), count 6 (summary + 2 shown + 3 more).
28. peek-1440: Blocks names a Sindri item ("item: Ship the web build"), since the seed has no GitHub or Linear; Linked holds `repo web`. The terminal shows a dry run, not a deletion.
29. peek-decision-1440: no option is chosen until the owner picks one; focus is in the peek, so the list row keeps only its selection fill and the ring sits on the first option. Answer is dashed (off) and the footer shows `↑/↓ option` instead of `Ctrl+Enter answer` until a pick.
30. peek-failed-1440: the peek keeps the fields and the actions; the body is "Couldn't load the thread" with Retry (spec 7.3).
31. updating-1440: "Updating…" is a quiet ink-subtle header line after the tabs (spec 7.3).
32. read-failed-1440: the log path `~/.agentic-workflow/sindri-app/logs/serve.log` is invented; tabs show no numbers.
33. tab-reviews-empty / tab-messages-empty: the split shows its Needs you header with 0 and one plain line, verbatim from spec 7.3; Moving, the brief and the Todo line are hidden off All (spec 7.1).
34. still-sending-1440: the Re-scope answer is still sending; waits-on "still sending" in ink-subtle.
35. changed-1440: "Changed while you were editing" is an attention sub-line after the title, with Reload on the cursor row.
36. hand-picker: opens under the cursor row; the header reads "Hand run r-0412 to…" (qa round 4: "(this repo)" read like a template); spec 5.1 lists the repo's running sessions, which is why only 3 of the 6 Moving sessions show; sessions are agent squares. The "Goes along" line is short enough for one line. Entries: new session per provider, then running sessions, an asking one disabled with "asking you"; a "Goes along" line. The empty form is a data state with no session at all (qa gone, Moving "Nothing in flight").
37. moving-open / moving-empty: unfolded Moving shows lines "lane: what (N min)" with no age (the minutes are in the title); "Show all N in Agents" only above 8 lines (spec 6.2). Empty Moving reads "Moving" and "Nothing in flight".
38. done-list: a separate data state (Re-scope and the Connect row done earlier), like brief-1440. Header "Done list" with no tabs; group label "Done in the last 7 days" is invented; the age column holds the done time; footer `Esc back to Home · Ctrl+Z undo last`.

## For the U1 build (not frame changes; checked against origin/main)
39. Key names (reviewed against home lane answer 6): `ShellWindow.KeyName` names every letter in upper case and passes it only with no modifier, so bare r and Shift+R both read "R" today. U1 extends `KeyName` to return "Shift+R" (re-rank), "1" to "5", "[" and "]"; Ctrl+Enter is U2's Home KeyDown handler.
40. Reviewed (answer 8): `C` opens Create on every page and stays global; `Y` (copy the CLI) and bare `A`, `X` act on Home's rows today and U1 turns all three off on the rows page. No frame hints A, X or Y (measure.js counts them: 0). The old line "Y is still live" is replaced by this.
41. Dropped (answer 9): `o` has no clash. The Todo panel owns `o` only while focus is inside it (`TodoOwns` checks `Todo.HasFocus`); with focus in the list `o` reaches `Press`.
42. Reviewed (answer 6): shifted letters, digits and brackets never reach `ShellViewModel.Press` today (`KeyName` returns null for them); U1's smallest change is the `KeyName` extension in guess 39.
43. Reviewed (answer 7): Shift+A is already global "mark every Needs you row seen" (`ShellWindow.Keys.cs:28`) and matches spec 5.4's Mark all seen; U1 leaves it alone. Bare N (quick-add), G (chord) and C (create) are not Home hints.
44. Spec wording conflict for the spec owner: spec 7.1 says the empty Messages tab reads "Messages arrive with #2979", spec 7.3 says "Slack messages arrive with #2979". Round 5b: qa round 4 asked to drop the raw issue number; the frame now reads "Slack messages arrive in a later release" with a "See what is planned" link, which differs from home lane answer 5 (keep 7.3's "#2979"). Open for the lead and the spec owner: guess 61.

## Round 5 (later build phases)
45. Sticky New bar fold arrow: New can't fold while it holds a row (spec 7.4: "The New band is never folded when it holds a row"), so the bar has no chevron on "New 2 above" and one chevron before "Needs you 5"; that chevron folds Needs you.
46. submit-menu-1440: the Ctrl+Enter submit menu is the page's existing menu shape, opened under the cursor row, titled "Submit on #3099", entries verbatim from spec 7.2 with Merge preselected and "Enter" on it. GitHub is connected for this data state. Pick: the title line and its placement.
47. action-panel-1440: the `.` panel is the same menu shape, titled "Actions on session qa", each action with its key (spec 7.4). Mute has no key in the spec; `m` is a pick (it matches the Context page's `.` menu).
48. undo-1440: the 5 s Undo is a toast at the bottom right above the footer with the spec's live-region text "Marked done. Undo with Ctrl+Z.", an Undo button and a "5 s" countdown; the cursor moved to the next row (spec 4.3). The footer shows `Ctrl+Z undo`. Pick: toast placement and the countdown.
49. keys-sheet-1440 (reworded in 5c): see guess 66; the sheet is the shell's single list scrolled to Home's section, not three columns.
50. peek-pr-1440: body = diff summary (files, +/-), checks with state, the closing issue (spec 7.2); no Slack thread because Messages hasn't shipped. Actions: Open review (Enter) and Submit… (Ctrl+Enter). File names and counts are invented.
51. peek-lane-1440: body = context size against the mark, last digest time, what the lane owns (spec 7.2, item-of list from spec 4.2's example). Actions: Copy digest request (`o`) and Open session (spec 5.1).
52. peek-message-1440: a Slack DM before Messages M2c: the last 5 messages, and "Open in Slack" in the composer's place (spec 7.2, 5.1). The line "Replies go through Slack until Messages ships its composer" is my text. The 48 px header cuts the title at a word.
53. peek-issue-1440: Linear fields shown are State, Priority, Team, Cycle, Assignee, Labels (spec says "Linear fields" only); linked PRs "none yet". Actions: Start (Ctrl+Enter, the primary), Change state, Comment (spec 5.1).
54. folded-1440: the folded Needs you header keeps its count ("Needs you 7"), shows `▸`, carries the cursor, and the footer shows "Left fold · Right open" (spec 6.2, 7.4). Moving, the brief and the Todo line move up.
55. brief-cap-1440: 23 facts: the summary line, the first 9 facts, then "and 14 more: see Moving, Work" (spec 6.1, verbatim). Pick: the summary line sits above the 10-line cap (9 facts + the cap line); runs r-0388..r-0409 are invented.
56. The full load (default-full, d3, peeks) has GitHub connected, so it no longer carries the "Connect GitHub" row; default-full is now 21 rows.
57. filter-focus-1440: Tab puts focus in the filter box (ring and caret); no row has the cursor; the footer reads `Enter to the list · Tab next · Esc clear`. Pick: those three labels (spec 7.4 only says Tab moves focus between the filter box, the list, the peek and the footer).
58. Row buttons use short labels from one table (`UI` in gen.py): Answer / Decline, Check off / Due date, Open review / Submit…, Merge / Open, Copy digest / Open, Open session / Resume, Reply / Hand, Open in Slack / Hand, Start / Change state, Unsnooze / Done. The peeks spell them out ("Copy digest request", "Jump to terminal"). Pick: the second button of each pair, and "Hand" for Hand to agent.
59. Nav counts: Reviews 4, Items 31, Agents 3 are the other pages' numbers copied from ../projects-3717; only Home's number is Home data (the All tab's). They don't track Home's own Reviews split on purpose.
60. The footer and keycaps are the shell's 11 px, as on Projects; Home's 12 px floor applies to content text (measure.js splits the two checks).
61. tab-reviews-empty / tab-messages-empty: when a split's source isn't connected the empty line is "Connect GitHub to see reviews" (Messages: "Connect Linear to see mentions", then "Slack messages arrive in a later release" with a link) with a Connect button, not "Nothing in <split>". Needs a spec 7.3 edit by the spec owner (qa round 4, hold 4); it also changes spec 7.1's Messages wording, which home lane answer 5 kept.
62. The Connect row is open data exactly when GitHub isn't connected (or it is done in the frame's Done data); a source that isn't connected draws no rows (spec 7.3). Checked in every frame.
63. snooze-woken: the woken row enters New above the cursor; the list scrolls the band's height so the cursor row keeps y 124 (spec 4.3). snooze-woken-top-1440 is the scrolled-to-top view, where the "back" mark shows.
64. Combined New bar: the New half never folds while it holds a row (spec 7.4); a click, Enter, Left or Right on the Needs you half folds or opens Needs you; a click on the New half does nothing. Pick.
65. brief header "b N more": a bordered button-shaped link with the `b` keycap, its own click target; the header around it folds the group on a click. brief-more-hover and brief-more-focus show the two states; the hover tooltip reads "Show 3 more lines (b). A click on the header folds the group." Pick: the wording.
66. keys-sheet-1440 is redrawn after the shell's real sheet (`ShortcutsSheet.axaml`: a 600 px card, one list, scrollbar always shown). It is scrolled to Home's own section, and it leaves out the fixed A, X and Y rows because U1 turns them off on Home. The Home section rows are labelled as the catalog would (Snooze, Hand to agent, Reply, Done with Undo, Submit menu, Row actions, Re-rank now, More brief lines, Previous / next split, Jump to a split, Fold / open a group). Pick: those labels.
67. row-failed: the reason is "failed: run paused" (it must fit the 112 px waits-on cell; the full reason is in the peek).
68. Moving lines leave the object and from cells empty: the title names the lane once (qa round 4).
69. long-title-1000 and long-title-peek-1000 add one invented decision with a very long title; the tooltip shows the whole title (every title carries it as `title`).
70. done-list-empty: the footer is `Esc back to Home` only, with no row keys (nothing to move on or open).
71. Waiting since is "weekday time, age" in every peek, derived from the row's minutes at the clock Fri 20 Nov 09:30 (a day-old row reads Thu 09:30, 1d).
72. peek-failed: Reply is dashed (disabled) with "Load thread first"; the footer drops `r reply`.
73. Round 5c, connections: the seed has Linear connected with nothing assigned (tab-messages-empty reads "Nothing in Messages"); linear-off-1440 and tab-messages-linear-off-1440 show Linear off. The Linear Connect row text "Connect Linear to see issues and mentions" is mine (spec 7.3 gives only GitHub's). Spec owner: confirm the wording.
74. Round 5c, the `?` sheet gains rows for every key a footer hints: Space, Tab, up/down, `o` (Jump to terminal, or copy the digest request on a lane), Ctrl+Z and a "Done list" row with no key (spec 5.4: reached from the footer link, the sheet and the palette). Labels are mine.
75. Round 5c: `.` is hinted on no row (the sheet lists it); `. r retry` and `. r connect` stay on a failed act. Snoozed rows no longer hint `. unsnooze` (the Unsnooze button stays).
76. Round 5c: done-list's Ctrl+Z reads "undo the last change"; its nav Home badge counts the open rows (5 with Re-scope and the Connect row done).
77. Round 5c: Sam's unanswered DM starts 09:02, which is the row's 28m, so the peek says Fri 09:02, 28m.
78. Round 5c, for the spec owner: empty-1440 draws no Todo line (spec 5.5 lists none) although 6 non-due todos still exist; guess 17 stays, the question is open.
79. Round 5d: the Messages tab offers one Connect per source that is off (GitHub and Linear), each worded exactly as its setup row on All ("Connect GitHub to see reviews", "Connect Linear to see issues and mentions"); in the seed GitHub is off, so tab-messages-empty offers GitHub. Wording is mine; spec 7.3 owner to confirm (replaces the 73 wording).
80. Round 5d: the `?` sheet frame draws all 22 rows at a compact 27 px row (the real card rows are 40 px and scroll); a mock.
81. Round 5d: Todo line = 6 non-due todos + the due ones that are open or snoozed in the frame; d3-17 and lanes-glyphs don't draw the 2 due rows, so they say "Todo, 6 open".
82. Round 6: guess 78 and 81 are closed by the home lane's take: the Todo line is always on All, N = the Todo panel's open count (8), whatever Home does to a due row. Spec 5.5 needs "the Todo line" added to what the empty Home shows.
83. Round 6: guess 79 is replaced: Messages offers only Linear; guess 61 closed: no "#2979", no link. Spec 7.1 and 7.3 edits are the home lane's (take 1).
84. Round 6, new guess: loading-1440 and read-failed-1440 draw "Todo, 8 open" too (the Todo store is read apart from Home). If the app hides the line while Home itself is down, say so.
85. Round 6, new guess: brief-states-1440 (a stacked review sheet) draws one Todo line at the bottom, not four.
86. Round 6: nav counts are derived: Items = open Linear issue rows (blank in most frames), Agents = Moving sessions + asking, limited and lane rows (a lane row is its own session, never one of the Moving ones). The real app would read these from the Reviews, Items and Agents pages.
87. Round 6: brief-cap's "and N more: see Moving, Work" keeps the spec 6.1 words (qa found them aimless); it is drawn as a link. Spec owner: should it name where the facts live?
