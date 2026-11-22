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
36. hand-picker: opens under the cursor row; the header says "(this repo)" because spec 5.1 lists the repo's running sessions, which is why only 3 of the 6 Moving sessions show. Entries: new session per provider, then running sessions, an asking one disabled with "asking you"; a "Goes along" line. The empty form is a data state with no session at all (qa gone, Moving "Nothing in flight").
37. moving-open / moving-empty: unfolded Moving shows lines "lane: what (N min)" with no age (the minutes are in the title); "Show all N in Agents" only above 8 lines (spec 6.2). Empty Moving reads "Moving" and "Nothing in flight".
38. done-list: a separate data state (Re-scope and the Connect row done earlier), like brief-1440. Header "Done list" with no tabs; group label "Done in the last 7 days" is invented; the age column holds the done time; footer `Esc back to Home · Ctrl+Z undo last`.

## For the U1 build (not frame changes; checked against origin/main)
39. `ShellViewModel.Press` gets uppercase letter names, so `R` (re-rank) and `r` (reply) can't be told apart today; U1 has to pass Shift through.
40. The shell's `C` (Create.Open, `ShellViewModel.Keys.cs:66`) and `Y` (copy the CLI on Home rows, `:46`) are still live on Home; spec 7.4 says `c` is not a Home key.
41. `o` is in the Todo panel's `_panelKeys` (`ShellViewModel.Todo.cs:52`) and `TodoOwns` runs before the page keys, so `o` (terminal) needs the panel closed.
42. Shifted letters never reach the view model today: `KeyName` (`ShellWindow.Keys.cs:158`) returns a letter only with no modifier, so `R` re-rank needs new plumbing; digits (`1-5` in the split footers and the snooze picker) and `[` `]` aren't passed through either.
43. Shift+A is already global "mark every Needs you row seen" (`ShellWindow.Keys.cs:28`); that matches spec 5.4's Mark all seen, no clash. Bare N (quick-add), G (chord) and C (create) are not shown as Home hints, so no frame clashes.
44. Spec wording conflict for the spec owner: spec 7.1 says the empty Messages tab reads "Messages arrive with #2979", spec 7.3 says "Slack messages arrive with #2979". The frame uses 7.3.
