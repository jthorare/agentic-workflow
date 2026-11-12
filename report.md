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
