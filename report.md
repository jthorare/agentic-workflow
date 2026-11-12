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

## Round 3d

| File | Issue | What changed |
|---|---|---|
| projects-list, projects-peek | #3168, qa on #2961 | Redrawn on section 14's final rules (replace the round 2 files): groups Off track, At risk, On track, No links; no "blocked" chip and no "waits on you"; mismatches spelled "At risk · Linear says on track"; reason lines with counts; the peek action is "Draft update…". A 28 px column-header row: Health (derived), Lead's call, Lead, Target, Updated, Waiting on. projects-list shows hover labels on the two health cells. Sub-lines render from grammar.css (`.row .title .sub`, ink-subtle). The cursor and selected outline sit inside the 24 px content inset |
| pr-peek-burnup | #3168 | The review rule reads "a review request or a changes-requested review has waited 3 days or more"; the peek no longer carries a collapsed Work / Latest update line (7 rule rows need the room) |
| pr-settings-hotkey-refused | #3170 | The Ctrl+Alt+S "Open Sindri" row is gone: one global hotkey, Ctrl+Alt+I. (The tray menu still lists Open Sindri as a menu item) |
| pr-create-window, pr-create-window-empty, pr-create-window-error | #3170 | Issue / Note switch at the left of the one-line window, set to Issue; Ctrl+Tab switches kind; the window opens on the last kind used. Footer shows Ctrl+Tab |
| pr-create-window-no-switch | #3170 | New: the switch is hidden when no notes folder is set in Settings; the window reads issue-only |
| every list and triage frame | qa | Progress track, cursor and picked-row fills follow the points below |

Progress track colour: `#5e6268`, which is `--ink-3` (#74787f) at 80% over the canvas (#08090a). Measured 3.25:1 on canvas (WCAG), up from 1.2:1 for `--s4`. The old `--hair-3` is 1.88:1. The checkbox border (`.g.box`) used `--hair-3` too, so it takes the same `#5e6268`; both are grammar candidates.

For design-notes-r2: the window to copy is `pr-create-window` (with the switch) and `pr-create-window-no-switch` (without). Its error line is `pr-create-window-error`.
