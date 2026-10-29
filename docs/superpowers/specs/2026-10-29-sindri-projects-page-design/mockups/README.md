# Projects page mockups (Ph5, for the owner's pick)

Spec: `../../2026-10-29-sindri-projects-page-design.md` (§3 the card, §3.1 page states, §3.2 the sheet, §7 the page). Static mockups,
no production code. Colors, sizes and radii are the app's tokens (`src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml`); no new tokens.
The top bar is drawn with the eight tabs, Projects second (§7); its right side is a stand-in.

`projects.html` draws every screen from URL parameters (`v=a|b|c`, `state=`, `sheet=`, `fold=`, `marks=1`). `bash shots.sh` renders
`screens/*.png` with headless Chrome. The sample data is the spec's fields only: seven projects across the three origins (led, attached,
by hand), two finished, one hidden, with the ship-2 PR line included.

## The three variants

| | A: the spec's card | B: grouped, counts first | C: one row per project |
|---|---|---|---|
| 1440 | `screens/a-1440.png` | `screens/b-1440.png` | `screens/c-1440.png` |
| 1000 (new minimum) | `screens/a-1000.png` | `screens/b-1000.png` | `screens/c-1000.png` |
| Sheet open | `screens/a-sheet-1440.png` | `screens/b-sheet-1440.png` | `screens/c-sheet-1440.png` |
| Finished open | `screens/a-finished-1440.png` | `screens/b-finished-1440.png` | `screens/c-finished-1440.png` |

- **A** is §3 as written: state and health top right, then lead and target, issues and last activity, the PR line. Ordered by state with
  no headings, so the state word is on every card. Fits all seven projects on one 1440 x 900 screen.
- **B** puts a heading per state (In progress 4, Planned 1, ...) and shows open issues and PRs as large numbers. The state word moves to
  the heading. Easier to see "how much is open" at a glance; it's taller, so at 900 px high Paused and Backlog sit below the fold.
- **C** is a table: one row per project, columns State, Health, Issues, PRs, Repos, Activity, Target. Best for comparing projects
  side by side and the densest. It isn't a card grid, so §7's "wrapping grid" and §3.3's arrow keys change to up and down only. At
  1000 px Repos and Target drop.

My pick: **A**. It's the spec, it fits one screen, and every card reads alone. B is the pick if the open counts matter most; C if the
list grows past about ten projects.

## Page states and sheets (same in every variant, drawn in A)

| Screen | File |
|---|---|
| Loading: three placeholders, no text (§3.1 row 2) | `screens/state-loading-1440.png` |
| Context can't answer: banner, attached and hand-made names only (row 3) | `screens/state-ctxerr-1440.png` |
| No Linear, nothing attached (row 4) | `screens/state-empty-1440.png` |
| Linear still syncing (row 5) | `screens/state-syncing-1440.png` |
| Owner leads nothing (row 6) | `screens/state-noled-1440.png` |
| Card marks: "counting…" during the re-read, an attached project no longer synced (greyed) | `screens/state-marks-1440.png` |
| Sheet below 1,100 px: covers the page | `screens/sheet-covers-1000.png` |
| Add project, From Linear tab | `screens/sheet-add-1440.png` |
| Edit an attached project, with the stale-version error under Save (SND-PROJECT-409) | `screens/sheet-edit-1440.png` |
| Remove asks inline, focus on Keep | `screens/sheet-remove-1440.png` |

## Found while drawing

- **Three columns at the minimum width cut names.** §7 says three columns from a 900 px page; at the new 1,000 px window the page is
  about 950 px, so A gets three 300 px cards and "Hiring loop for the platform team" and "Design system v2" are cut, and the
  attached / by-hand chip is pushed off. Two ways out: three columns from about 1,040 px of page instead of 900, or let the name wrap
  to two lines. B cuts the long name the same way and its footer wraps to two lines; C doesn't cut.
- **The finish date isn't on a card.** Finished cards (open the fold) show only last activity; the 14-day rule counts from the finish
  date, which only the sheet could show.
