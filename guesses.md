# Guesses, Reviews frames (Part of #3177)

Fixtures come from the spec ("Harbor ferry", #3120, `a1b2c3d`). Other names (people, files, numbers) are made up.

1. Dark frames: the app is dark only, so there is no light theme to contrast. `reviews-queue` and `reviews-a-1440` are the dark queue and surface frames. No separate copies. Needs a decision: do you want a light variant drawn later?
2. 1200 px of the three-pane walk is itself the 1100 to 1439 inline form, so `walk-1200` covers it. `walk-1280-popover` adds the popover. No separate "inline 1100-1439" frame beyond those two.
3. Layout A's file list is a two-column strip of folder groups (Booking/Holds; Data, Tests, Other and folded), max 152 px, one column below 1100. The spec says only "folder-grouped file list above".
4. Layout A's seven panels are collapsed to one line each with a summary ("Required checks 2 of 2 pass"). Summary texts are invented. All panel bodies are open on `walk-overview-1440`.
5. Queue glyphs: drawn ring shapes for the six sections (needs you: dotted ring, new commits: ring with chevron, returned: half-filled ring, waiting: dashed ring, ready: check disc, draft: dashed square). The spec gives text marks only. These are new glyph candidates for the grammar.
6. Peek actions: Review (primary), Run pre-review `g r`, Cancel run, Run again, Request reviewer `a`, Open on GitHub. `g r` is invented; the spec lists the action without a key.
7. Pre-review peek copy: running "started 2m ago, 3 findings so far, $0.12 of the $5 day cap"; failed "hit the model limit after 4m, $0.31" with SND-REVIEW-017; absent "auto mode skipped it: day cap reached"; stale "read 9f8e7d6, 6 findings, 2 may point at moved lines".
8. Ready peek: "Not run: your own PR, auto mode skips it" and "approved by ada" are made up.
9. Partial list banner: "GitHub search failed: rate limit reached, resets 09:58", `SND-REVIEW-019`, Details, Retry (F5). The spec gives no code for a partial list (#3065).
10. Serve-down: banner button "Start Sindri" is added (the spec names only the text and `sindri serve`). The CLI line sits under Details, as the grammar says. The status pill reads "Not running".
11. First run: three numbered steps, macOS / Linux toggle, "Check connection" as the one primary. Commands are the spec's (section 8). "About 10 minutes" is the spec's target.
12. Submit sheet is a 640 px right side sheet. Comment is the one primary (it holds focus); Approve and Request changes are secondary; Discard draft sits at the left, Cancel at the right.
13. Flag copy: "from finding, unedited" (spec), "has an @mention and a link" (made up), and "Verdicts are off until each flagged comment gets Keep, Edit or Remove (2 left)".
14. Offline: banner "Offline: reconnect to post. Your draft is kept and stays editable." plus the line "Nothing is queued: a queued verdict could post on a moved head" and SND-REVIEW-021.
15. Empty sheet reason: "Comment is off: add a body or a comment first." Approve and Request changes stay enabled (GitHub allows an empty approve).
16. Discard draft: toast "Draft discarded. Viewed marks kept." with Undo Ctrl+Z and a 5 s bar at bottom left, so it does not hide the draft counter.
17. Head moved: the banner on the surface has Reload as the primary and the header's Submit review drops to secondary. Banner and sheet copy follow section 5.6 word for word.
18. Merge picker: a popover under the header's Merge button; labels "Squash and merge", "Create a merge commit", "Rebase and merge" with tags "repo default" and "rewrites history". The conflict text ("conflict with main in Pier/Schedule.cs") is made up. The merge frames use a second fixture (#3109) with no findings.
19. The Merged toast has an "Open on GitHub" button; the spec gives the text only.
20. Posted toast: "Posted. Next: #3127", Open (Enter), 5 s bar; #3120 moves to Waiting on others (count 3).
21. Palette groups: Files, Hunks, Actions, with "Sync viewed to GitHub". Row hints (core, file 3 of 7) are invented.
22. Reviewer picker: names and the hints ("author, can't review", "reviewed 2 PRs this week", "out until Mon") are made up. Team row "@harbor-api" follows the spec's team reason.
23. Reviewer states copy follows 5.6 ("Loading reviewers...", "No match for ... and Clear", "Couldn't load reviewers ... Retry"); "Requesting..." is the spec's word.
24. Focus order panels restate 352 and 410 as numbered stops. The surface list adds the draft counter as stop 5 (spec: "header, file list, diff, panel column, draft counter").
25. Focus rings: row and file use the grammar's inset 1 px primary-hi ring; a diff line adds a 3 px gutter bar so it shows on a tinted line; buttons and toggles get a 2 px outline, offset 1 px.
26. Hover and pressed: the grammar defines hover only (s2). I drew pressed as one surface step back down (s3), primary dimmed 18%, menu hover s4, no movement or scale. Needs a decision before ui builds it.
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
37. Ready line in a row truncates "required check build" to "check build" to fit the 160 px chip; the peek and header show the full phrase.
38. Footer key lists are per frame: queue uses the spec's footer; review frames use 5.2's footer; sheets list Tab, Esc.
