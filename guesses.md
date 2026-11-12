Guesses for the Projects round 3 frames (#3168 to #3172). "Settled" means ideas answered it in the round 3 check comments (6822693408, 6822693769, 6822694125, 6822694426, 6822694799). Settled items are kept for the record and drawn as ideas decided.

Settled
- 3 (#3168 "Merges slowed" sentence): dropped. The API returns burn-up and throughput data only, so the two charts stay and no sentence reads them.
- 8 (#3169 cursor after a bulk Accept): the cursor lands on the row after the last picked row in list order. The picks are EXP-431, gh-3094, WSP-110, BIL-304 and gh-3107, so it lands on WSP-114.
- 9 (#3171 menu with no failing PR): Open PR and Re-run CI are greyed with "No failing PR" (frame `pr-menu-no-failing`).
- 10 (#3171 filter): matches words anywhere in the label, so "ci" finds "Re-run CI".
- 13 (#3172 Home reminder): a Needs you row whose actions are Draft update and Snooze, buttons only. No Done button and no `h` / `e` keys in the footer.
- 14 (#3170 hotkey): Ctrl+Alt+I.
- 15 (#3170 error in the create window): one state line, "Linear can't be reached. Your text is kept." with Retry; the window stays open (frame `pr-create-window-error`).
- 16 (#3170 confirmation): the system notification stays the confirmation, with Open in Linear and Copy link.

Rules and numbers (spec section 14, "Health rules")
1. The rules, groups and reason strings are the spec's. Off track: target passed with open work, main CI red 2 days or more. At risk: a PR red 2 days or more, a review waiting 3 days or more, under half done with 14 days or less left. On track. No links ("Link a repo"). The peek lists all seven rows under "How health is worked out" with the matched row marked.
2. The counts in the fixtures (2 of 4 PRs red, 3 of 9 PRs red, 9 days left, main red 2d (api)) are made up.
3. A mismatch is spelled in the row's sub-line ("At risk · Linear says on track · 2 of 4 PRs red > 2 days") and in the peek ("Linear says: on track, differs: Sindri says at risk"). No glyph or colour carries it alone.
4. Burn-up axis: 28 days back and 16 ahead, today and the target marked; the scope step (+2 on Nov 6) and the weekly merge counts (2, 3, 4, 2, 1, 1) are made up.
5. To fit the peek, Work and Latest update fold to one collapsed line.

Triage and bulk
6. Multi-pick uses the checkbox glyph on every row once a pick exists, a primary-soft fill on picked rows and a "5 picked" chip in the header. The bulk bar is the grammar's action bar above the footer; the primary is "Accept 5" with Ctrl+Enter. The row's own action strip is off while rows are picked.
7. Accept, Decline and Snooze share one 5 s Undo. The toast reads "Accepted 5 · Undo (Ctrl+Z)" with a bar and "The Linear writes go out when the 5 s end." The count is 5 because five rows are picked in the select frame.
8. A GitHub (`gh-`) row: Accept adds it to the project in Sindri and writes nothing to GitHub, Decline hides it from Triage, Snooze works as for Linear, Duplicate works on Linear items only and is greyed with a tooltip. In the bulk bar Duplicate is greyed when any picked row is GitHub (2 of the 5 here).

Menu
9. Accelerator letters (o r s a) only move the highlight; Enter picks. The footer reads "letter jump, Enter pick". "Open PR #2931, failing" and "Re-run CI on #2931" name the project's first failing PR.

Draft update and Home
10. The draft has a health line, a bulleted list of merged PRs, and "Open risks" as the health reason string, with a "Next:" line left for the owner. It lists no PR numbers for risks (the API returns reason strings only). The wording of the merged PR titles is made up.
11. Esc closes the draft and keeps it; Discard is a button with a 5 s Undo.

Create window and Settings
12. Frameless dialog over a plain desktop with a tray strip. Project and Assignee are preset; "in Linear · EXP". Title field only.
13. Settings copy for a refused hotkey is ideas' exact text: "The OS refused this hotkey (taken, or no global-shortcuts portal on Wayland). Use the tray menu's New issue." One primary, "Choose another shortcut". The tray strip shows New issue. The Settings page layout (groups, rows) is my guess; the spec has no Settings frame.

Screen reader lines
14. Every frame carries one "Screen reader:" line saying what a row (or the window, menu or notification) reads as. The wording is mine; none of the frames before this round had aria names.
