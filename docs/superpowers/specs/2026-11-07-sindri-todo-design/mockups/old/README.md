# Todo page mockups (Ph0)

Spec: `../../2026-11-07-sindri-todo-design.md` (§3.1 the page, §3.2 quick-add, §3.4 states, §6 errors, §11 / TP-7 the ten-tab bar). Static mockups,
no production code. Colors, sizes and radii are the app's tokens (`src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml`); no new tokens. The top bar is the
one from the PR review list mockup with a tenth tab. Sample date: Sat 2026-11-07; data is made up.

`todo.html` draws every screen from URL parameters (`page=todo|reviews|home|bars`, `state=`, `sel=1`, `edit=1`, `conflict=1`, `toast=1`,
`done=1`, `quick=parsed|pr|fail`, `v=home`, `ts=1.3`). `bash shots.sh` renders `screens/*.png` with headless Chrome (Google Fonts are fetched).

| Screen | File |
|---|---|
| The page, 1440 and the 1000 px minimum | `screens/page-1440.png`, `screens/page-1000.png` |
| The page tall (From elsewhere whole, Linear capped at 5 with "Show all 9 in Work") | `screens/page-full-1440.png` |
| A row selected (focus ring) | `screens/page-selected-1440.png` |
| Done today open | `screens/done-expanded-1440.png` |
| Empty: "Nothing on your list. Press N to add one." | `screens/empty-1440.png` |
| Sindri not running (card, pill, `N` says so) | `screens/notrunning-1440.png` |
| Quick-add over Reviews, "check flaky test fri" -> "Due Fri 11-13" | `screens/quickadd-1440.png`, `screens/quickadd-1000.png` |
| Quick-add with a pasted PR url: "Links PR #2071" | `screens/quickadd-pr-1440.png` |
| Quick-add save failed, text kept, reason shown | `screens/quickadd-failed-1440.png` |
| A row in edit (text, date, link) | `screens/edit-1440.png` |
| The 409 row: "Changed somewhere else. Here's the latest" | `screens/conflict-409-1440.png` |
| Undo toast after check-off (tab count drops to 1) | `screens/undo-toast-1440.png` |
| From elsewhere, Linear failing: "Couldn't load Linear" + Retry | `screens/elsewhere-failing-1440.png` |
| Home with the new line | `screens/home-1440.png` |
| Ten tabs measured (TP-7) | `screens/bars.png` |
| Fallback (not needed): Todo as a button in Home's header, nine tabs, 1000 px | `screens/fallback-home-1000.png` |

## Ten tabs at 1000 px (TP-7)

`screens/bars.png` runs the Reviews list mockup's bar model with the real Inter widths (it waits for the font to load before measuring) and
also reads the drawn bar's overflow from the DOM. Worst-case counts on Todo 2, Workflows 12, Work 12, Recipes 8, Agents 4, Reviews 3.

- **Fits, nothing clipped.** 1000 px at 100% text needs 924 px at the `tightNav` step (76 px spare). Drawn overflow: 0 px.
- **What it costs.** Nine tabs at 1000 px land one step earlier (`noAge`, 954 px, needs no padding squeeze). Todo is what pushes the bar into the
  `tightNav` step: ⌘K folds to its magnifier, the pill drops its age, and tab padding goes 7 -> 2 px, so tab labels sit close together.
  "Todo" has no short label, so §11's label fallback doesn't apply; "Workflows" -> "Flows" is not needed.
- **At text 130%** it fits only at the last step (`noCounts`, 918 px, 82 spare): the counts disappear, **including Todo's "2"**, and the brand shows
  its mark only. TP-7's "nothing is cut" holds, but the count is the point of the tab at that size.
- The `v=home` fallback is drawn anyway (`screens/fallback-home-1000.png`); it is not needed.

## Found while drawing

- **Dates in the brief and the spec.** Today is Sat 11-07, so "Fri" is 11-13, not 11-14. The parsed line shows "Due Fri 11-13"; the "11-14"
  upcoming row is a Saturday, shown as a date because it is 7 days out (§REQ-O2: relative word for the next 6 days).
- **Home line vs the tab count.** Tab count = overdue + today = 2 means "1 todo due today · 1 overdue", drawn that way (the brief said 2 · 1).
- **From elsewhere cap.** Linear shows 5 rows and "Show all 9 in Work" (the brief said 4 issues, which would never reach the cap).
- **409 text.** §3.4 says "Changed somewhere else. Here's the latest"; §6 says "That todo changed somewhere else. Reload and try again." The row
  uses the §3.4 text and shows the latest values under it; §6's is the API message.
- **Save-failed reasons.** §3.4 points at "§8" for the reasons, but §8 is Docs to update; the reasons are §6's (drawn: Sindri unreachable).
