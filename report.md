# First-run frames after meta's UX pass (#3236)

Base: cf1ed88daf. Spec head: d4592e8b24. List: plans/first-run-r3/design-redraw-ux.md (31 rows). Generator: `python3 gen.py && python3 shoot.py setup-connect setup-repos setup-repos-page setup-accounts setup-steps stale-line not-running states`.

## Contrast of the lifted sub-line colour

`#8a8f98` (the grammar's ink-subtle) on the first-run column backgrounds, WCAG 2.2:

| Background | Where | Ratio |
|---|---|---|
| `#08090a` (Canvas) | the lede line under the heading, the eyebrow, the Signed-in line | 6.13:1 |
| `#141517` (Surface1) | the sub-line of each row card | 5.62:1 |
| `#1c1d20` (Surface2) | a hovered or selected card | 5.19:1 |

The old sub-line colour `#74787f` on Canvas is 4.49:1, below 4.5:1. All three rows above clear 4.5:1.

## Rows

| # | Done in | Where to look |
|---|---|---|
| 1 | setup-connect, setup-repos | 640 px centred column, 24 px heading, lede `#8a8f98`; first panel 1440 wide, second 800 wide, in both files |
| 2 | setup-connect | no gh-login default panel; "After Change on question 2" is the only gh-login Q1 |
| 3 | setup-repos | "Signed in as ada through gh · Change" on every gh-login panel (not on the no-account panel) |
| 4 | not-running | six panels: Starting by itself, Didn't start, Not running (after a failed start), Already starting, Still stopping, broken install |
| 5 | not-running | reduced bar (wordmark + pill) on all six |
| 6 | states | "hello-sindri not run" removed |
| 7 | states | "hello-sindri succeeded, the map is still building" |
| 8 | states | failed has Open the run (primary) and Run again |
| 9 | states | "a map build failed" |
| 10 | states | no-profile relabelled "only after a restart error"; no-repo relabelled "after Skip for now on question 2" |
| 11 | states | the hello-sindri panels use the full bar, drawn 1440 wide so the bar fits |
| 12 | setup-repos | Add failed for one repo: Continue to Home primary, Retry on the row secondary |
| 13 | setup-repos | none found, scan failed, every add failed: Skip for now beside the primary |
| 14 | setup-repos | ring on row 1 in Scanning and Done |
| 15 | setup-repos-page | Done primary (focused) and Add 1 repo primary |
| 16 | setup-steps | Add repos on the done row |
| 17 | setup-steps | ring on the row that holds the primary |
| 18 | setup-steps | hello-sindri running by itself, then succeeded with Open item and Run again |
| 19 | setup-steps | map sub-line ends "You can keep working; it finishes in the background." |
| 20 | setup-steps | map failed for one repo; map build stopped |
| 21 | setup-steps | no-agent keeps Dismiss |
| 22 | setup-connect | "Skip for now" |
| 23 | setup-connect | token typed: Save token (Save key for Linear) is the primary, opener secondary, Continue secondary |
| 24 | setup-connect | five single-state panels |
| 25 | setup-connect | "gh login found: ada" once, in the sub-line of the After Change panel |
| 26 | setup-connect | sign-in cancelled panel |
| 27 | setup-connect, setup-repos | footer keys that act only; pill "Sindri running" |
| 28 | stale-line | Ctrl+Shift+R chip on Restart when they finish |
| 29 | setup-accounts, setup-steps, stale-line, states | full bar with counts, Ctrl+K and Setup 6/8 |
| 30 | setup-steps | Loading, Steps read failed, serve stale, sources expired, sources first read failed |
| 31 | setup-repos | GitHub repos unreadable, Stop scan pressed, dragged folder |

## Not done

- The older panels of `states` that predate the first Add (Shell not responding, Stale serve under the top bar and the page-state panels) keep their compact bars. Only the hello-sindri and map panels got the full bar, and those panels are drawn 1440 wide because the full bar does not fit in 720.
- Ring rule: on the "hello-sindri running by itself" panel no row holds a primary, so the ring sits on the first row not done (the running map row). That follows the spec's fallback in spirit; the done rows' Accounts and Add repos buttons are secondary. Meta to confirm.
- With a primary elsewhere on the Setup page the ring sits on that row. With none, the ring sits on the hello-sindri Open item row (it holds the page's one primary), never on the running map row.

## Fixture check against spec §5.0 (#3329)

Spec at origin/main 13744d9 or later. Checked by reading each frame's HTML text and every panel's primary buttons (one script counts `btn primary` per panel, ignoring a busy one).

| §5.0 row | Frames that show it | Result |
|---|---|---|
| Accounts: gh login `ada`, Linear and Slack optional and not connected | setup-connect (After Change, connected rows), setup-accounts, setup-repos (header line), setup-steps (accounts row) | matches; "After Change" primary says "Use the gh login (ada)" (#3315) |
| Scan: `~`, depth 3, "41 folders · 6 repos so far", done "6 repos" | setup-repos Scanning, Done | matches. **Changed:** "Stop scan pressed" said "4 repos found"; it now says "41 folders · 6 repos found" and lists the six |
| Repos found: six repos, paths, owners, worktrees, known | setup-repos (every panel with the list), setup-repos-page (the four not yet added) | matches (`jthorare/agentic-workflow` 104, `ledgerly-app/exports` 3, `ledgerly-app/core`, `ada/dotfiles`, `ada/prism-mcp` 1, `~/src/old-site` no remote) |
| Ticked: 2 of 6, "Add 2 repos" | setup-repos Scanning, Done, Adding, Stop scan pressed | matches; other counts derive from it (drag: 3 of 7, one failed: 1 of 2 added, nothing pre-ticked: 0 of 6) |
| Repo size: 15,223 files, 82,825 symbols | setup-steps (map rows say 15,223 files). **Changed:** the done map row now reads "a current map of agentic-workflow (15,223 files, 82,825 symbols) and exports" | matches |
| Index progress: "Indexing files: 7,000 of 15,223" | setup-steps Map phase 1 | matches |
| Map progress: "Building the map: 4,720 of 15,223 files", chip 31% | setup-steps phases 2 and the panels below, states "Map building", Home succeeded line | matches |
| Builds: running `80f69df` built 2026-11-11 22:10Z, installed `7c81646` built 2026-11-12 10:31Z | stale-line (Stale, Details open, Restarted toast), states Stale panel. **Changed:** setup-steps "The serve row is stale" now says "A newer Sindri is installed (7c81646, built today 10:31). Running 80f69df, built yesterday 22:10." The other setup-steps panels keep "running 7c81646, the installed one" because no update is waiting there | matches; no build numbers anywhere |
| Open set of 3 | stale-line Runs open, Waiting | matches |
| hello-sindri: four steps | "step 2 of 4" on Home and Setup; failure names step 2, Make a greeting | matches (the frames never list the steps, the spec's example needs only step 2 of 4) |
| hello-sindri failure line | setup-steps failed panel, states Home failed | matches word for word |
| hello-sindri success line | setup-steps, states Home succeeded. **Changed:** the Home row lacked the final period | matches |
| Map failure line | setup-steps "Map failed for one repo" (full sentence); states Home map-failed (short form with Details, as §4.6) | matches |
| Still stopping: pid 48213, serve.log path | not-running panel 5, stale-line Still stopping | matches |
| Didn't come back: port 7410 | stale-line Didn't come back; not-running "Didn't start" | matches |
| Setup page: 6 of 8, "Setup 6/8" | setup-steps panels before hello-sindri's result (stale, first-read-failed, no-agent, running-by-itself), Home being made and running, stale-line | 6/8 before hello-sindri runs, 7/8 after (ideas, #3315): setup-accounts, the older states panels, and every panel that shows hello-sindri's result read 7 of 8 and Setup 7/8, because the map is the one step left |
| Services: 16 | **Added:** states "Home: health strip with the services count (#3127)" reads "Sindri running · 16 services · profile saved" | now shown |
| Today's times | not-running Starting says "about 10 seconds" (serve up 9.9 s); the other times appear in no frame | the 3 s, 81 s and 18 min figures are not drawn |

One primary per state (decision 22): every panel has exactly one primary, or none where the spec says none (a running or waiting state, Adding, the Repos page with a busy Add, All done, own rows present). No panel has two. The Button states legend shows one primary per column by design.
