# Projects round 3 frames (Part of #2961)

All 1440x900 PNGs in this folder (`pr-<name>.png`), on `../grammar-2978/grammar.css`. Regenerate with `python3 gen.py && ./shoot.sh`. Built on the round 2 sheet. Health words, groups and rules are spec section 14; the fixes are ideas' round 3 check comments. Every frame carries a "Screen reader:" line. Each PNG was looked at after the last shoot.

| File | Issue | What it shows | Guesses |
|---|---|---|---|
| pr-peek-burnup | #3168 | List in the four health groups (Off track, At risk, On track, No links) with the final reason strings; peek on Exports v2 with burn-up, PR merges per week and "How health is worked out" (7 rows, matched row marked). Mismatch is spelled in words | 1 to 5 |
| pr-triage-select | #3169 | 5 rows picked (Shift+j range plus one x), checkbox glyphs, "5 picked" chip, bulk bar with the shared 5 s Undo note, Duplicate greyed because GitHub rows are picked | 6, 7, 8 |
| pr-triage-after | #3169 | After Accept: 5 rows gone, cursor on WSP-114, "Accepted 5 · Undo" toast with a 5 s bar and the Linear-writes note | 7 |
| pr-triage-github | #3169 | Cursor on a GitHub row: Accept adds to the project, Decline hides, Duplicate greyed with a tooltip saying why | 8 |
| pr-menu | #3171 | The `.` menu on Exports v2: Open PR, Re-run CI, Scope this, Ask an agent to fix; letters jump, Enter picks | 9 |
| pr-menu-filtered | #3171 | "ci" typed, one match | settled 10 |
| pr-menu-no-failing | #3171 | A project with no failing PR: Open PR and Re-run CI greyed, tooltip "No failing PR" | settled 9 |
| pr-draft-update | #3172 | Peek on Exports v2 with the update drafted: health line, bulleted merged PRs, open risks as the reason string, "Next:" for the owner; Post, Redraft, Discard (5 s Undo); Esc keeps the draft | 10, 11 |
| pr-home-update-reminder | #3172 | Home: the "update due" row with Draft update and Snooze only; footer has no h / e keys | settled 13 |
| pr-create-window | #3170 | One-line create window, shortcut Ctrl+Alt+I in the tray strip | 12 |
| pr-create-window-empty | #3170 | Same, before typing | 12 |
| pr-create-window-error | #3170 | "Linear can't be reached. Your text is kept." with Retry; window stays open | settled 15 |
| pr-create-window-done | #3170 | Notification "Created EXP-437" with Open in Linear and Copy link | settled 16 |
| pr-settings-hotkey-refused | #3170 | Settings drawn in the real SettingsView layout: a Shortcuts section (heading over a card) with Ctrl+Alt+I, ideas' exact refused-hotkey line and one primary (Choose another shortcut), beside the real This app section; the tray menu shows New issue | 13 |
