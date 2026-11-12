# Guesses, Reviews frames (Part of #3177)

Fixtures come from the spec ("Harbor ferry", #3120, `a1b2c3d`). Other names (people, files, numbers) are made up.

1. Dark only (ideas and the lead agree): there is no light theme. `reviews-queue` and `reviews-a-1440` are the dark frames.
2. 1200 px of the three-pane walk is itself the 1100 to 1439 inline form, so `walk-1200` covers it. `walk-1280-popover` adds the popover. No separate "inline 1100-1439" frame beyond those two.
3. Layout A opens on Files (decision 4): the list is grouped by real folders (docs, src/Booking/Holds, src/Data/Migrations, tests/Booking), alphabetical, no band tags, max 190 px. The Order toggle is off. The spec says only "folder-grouped file list above".
4. Layout A's seven panels are collapsed to one line each with a summary ("Required checks 2 of 2 pass"). Summary texts are invented. All panel bodies are open on `walk-overview-1440`.
5. Section glyphs: accepted by ideas as the new grammar set; the word stays in the accessible name.
6. Run pre-review has no key chip (ideas: not in the key table, no second key after `g`). It is a plain button on the peek and a menu item in `.`.
7. Pre-review peek copy: running "started 2m ago, 3 findings so far, $0.12 of the $5 day cap"; failed "hit the model limit after 4m, $0.31" with Details closed (SND-REVIEW-017 inside); absent "No pre-review for this head. Run one." (also used on a PR of your own); stale "read 9f8e7d6, 6 findings, 2 may point at moved lines".
8. Ready peek: "approved by ada" is made up. The "not run" sentence is gone: every PR without one reads "No pre-review for this head. Run one."
9. Partial list banner: "GitHub search failed: rate limit reached, resets 09:58" and Details, Retry (F5). No code shown: a failed read has none (ideas: SND-REVIEW-019 is for a refused write).
10. Serve-down: banner button "Start Sindri" is added (the spec names only the text and `sindri serve`). The CLI line sits under Details, as the grammar says. The status pill reads "Not running".
11. First run: three numbered steps, macOS / Linux toggle, "Check connection" as the one primary. Commands are the spec's (section 8). "About 10 minutes" is the spec's target.
12. Submit sheet is a 640 px right side sheet. Comment is the one primary (it holds focus); Approve and Request changes are secondary; Discard draft sits at the left, Cancel at the right.
13. Flag copy: "from finding, unedited" (spec), "has an @mention and a link" (made up), and "Verdicts are off until each flagged comment gets Keep, Edit or Remove (2 left)".
14. Offline: banner "Offline: reconnect to post. Your draft is kept and stays editable." plus the line "Nothing is queued: a queued verdict could post on a moved head" and SND-REVIEW-021.
15. Empty sheet reason: "Comment is off: add a body or a comment first." Approve and Request changes stay enabled (GitHub allows an empty approve).
16. Discard draft: toast "Draft discarded. Viewed marks kept." with Undo Ctrl+Z and a 5 s bar at bottom left, so it does not hide the draft counter.
17. Head moved: the banner on the surface has Reload as the primary and the header's Submit review drops to secondary. Banner and sheet copy follow section 5.6 word for word.
18. Merge picker: popover under the header's Merge button; labels "Squash and merge", "Create a merge commit", "Rebase and merge" with tags "repo default" and "rewrites history"; Cancel and one primary Merge (Enter). Conflict shows GitHub's reason only (text made up), code under Details; the header redraws to "1 conflict" and "Merge refused". The merge frames use a second fixture (#3109).
19. The Merged toast has an "Open on GitHub" button; the spec gives the text only.
20. Posted toast: "Posted. Next: #3127", Open (Enter), 5 s bar; #3120 moves to Waiting on others (count 3).
21. Palette groups: Files, Hunks, Actions, with "Sync viewed to GitHub". Row hints (core, file 3 of 7) are invented.
22. Reviewer picker: names and hints are made up. "ada, author, can't review" is drawn disabled with its reason. Footer: Down names, Space toggle on a name, Tab to Request, Esc close; the Request button has no Enter chip because Enter never requests from the search field.
23. Reviewer states copy follows 5.6; the load error keeps SND-REVIEW-019 under Details.
24. Focus order panels restate 352 and 410 as numbered stops. The surface list adds the draft counter as stop 5 (spec: "header, file list, diff, panel column, draft counter").
25. Focus rings: row and file use the grammar's inset 1 px primary-hi ring; a diff line adds a 3 px gutter bar so it shows on a tinted line; buttons and toggles get a 2 px outline, offset 1 px.
26. Hover and pressed are not a guess any more: they use the app's values (ClickableStyles.axaml:5, Tokens.axaml:62-69). Hover #1c1d20 with the strong hairline; pressed #222326 with the tertiary hairline and scale 0.98 about the centre; primary hover #535fc6 and pressed #4c57be (Tokens.axaml:53-57). Not drawn: the colour ease, which is Motion.Hover.
27. Motion: toast rises 4 px and fades, sheet slides 24 px, fold eases height and turns the chevron, scroll-to-anchor under 100 ms (spec 5.5), spinner one turn per second. 120 ms is the spec's. Reduced motion: all at once, spinner still, word carries the state.
28. Findings states copy: partial "stopped after 2 findings: it hit the 2 KiB cap" (SND-REVIEW-024); failed "hit the model limit" (017); stale chip plus the 016 line. The "2 new" chip is a primary-soft tag next to the count.
29. Threads: "GitHub answered 502 Bad Gateway" is an example reason; the inline 019 failure keeps Retry and Reply.
30. Tree: Ordering skeleton bars, "Order unavailable: the code index is not built for this repo. Files mode shown.", a "!" mark for a file that couldn't be read, "showing 50 of 300 . Load 50 more".
31. Since your review: the scope shows as a tag next to the title; the tree lists the 3 changed files and "11 files unchanged since your review"; the agent proposal is a pending-reply box with y, e, d.
32. Overview: summary text and the seven panels as two columns of cards. Panel contents are invented.
33. 1000 px: the top bar's search shrinks to 150 px; the tree drop-down button reads "Files" with `[`; the file counter "file 3 of 7 in order" is made up.
34. Header: line 1 title, meta, "Actions" ghost button and the primary; line 2 pre-review, CI, required, conflicts, the ready tag, Order/Files and All/Since toggles as segments. Header grows to 84 px.
35. Split diff toggle (Unified | Split with `g l`) sits in the file header.
36. Suggestion composer note: "Sindri copies the patch, it never commits to a branch you don't own" (decision 8, shortened).
37. The ready chip on a row keeps the spec's full phrase "Waiting on: required check build" and truncates with an ellipsis; the tooltip (not drawable) holds the full text.
38. Footer key lists are per frame: queue uses the spec's footer; review frames use 5.2's footer; sheets list Tab, Esc.
39. Error codes: inline only in the submit sheet (offline, head moved) and on the inline resolve failure. Everywhere else (peek, merge conflict, stale and partial findings, failed pre-review, picker load) the code sits under a closed Details row. Threads-load and queue-partial reads show no code.
40. Fixture counts: pre-review is "2 findings, 2 left" and the Findings pane lists both; "6 findings, 4 left" is gone so the numbers match the rows.
41. The "since your review" frame uses 9f8e7d6 as the reviewed sha (3 commits before a1b2c3d).
42. A provenance tag "order from the code index at a1b2c3d" sits next to the Order toggle in the header on frames in Order mode (not at 1200 and below, where there is no room beside the chips).
43. Key chips on primary buttons are an outline chip with white text (the filled chip measured 3.64:1).
44. Palette: only matching rows; the no-match state is its own frame (`reviews-palette-no-match`). Row hints say "file 3 of 14".
