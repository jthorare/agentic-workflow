# Guesses in the #3717 frames

Checked by the projects lane against origin/main and `feat/gh-3718-project-truth` (issue comment 7072903178). Confirmed guesses are marked. Guesses the lane corrected are gone, and the frames follow the facts now (notes.md "Round 8, part 2"). The rest are still open.

## Confirmed
- Health comes from open non-draft PRs only (HealthOf). The target never colours a row. (lane 1)
- Needs me sorts by last activity; the Sort menu has last activity, target date and name. (lane 6)
- Finished is a fold that starts closed; the 32 count holds. (lane 8)
- The five PR glyph states: ciRed, waiting, unknown, draft, ready. (lane 10)
- A ciRed PR's `decidedAt` shows as "CI red · Nov 12", an "as of" date. (lane 11)
- The reason strings are the server's text (ProjectTruths.cs:115-123). (lane 16)
- `o` for sort is free. (lane 17)
- Drafts don't count; "PR" for 1, "PRs" otherwise; a drafts-only project has no health. (lane 20)
- Focus is one control at a time; focus-controls draws four rings only to show the look. (lane 24)

## Answered by the projects lane (issue 3717, 2026-11-22)
1. CONFIRMED: "CI not checked" is its own health group (server level `unknown`). Note: On track doesn't mean every PR was checked; a waiting PR with no CI counts as checked.
2. CONFIRMED: on a Linear mismatch the row appends "· Linear says <word>"; the app joins the two fields.
3. CONFIRMED: "Blocked on you" comes from the server flag `blockedOnYou` (#3797). By-hand projects have no lead, so "you lead it and CI is red" never fires for them.
4. CONFIRMED: "passed" is an app rule (compare the target with today); no server flag.
5. CONFIRMED: the app formats each freshness row's `at` for "as of"; failing context and sindri rows have no `at`.
6. CONFIRMED (decision): My work = projects you lead + projects made by hand; attached ones are left out.
7. CONFIRMED (decision): no bare key for Add project; button + the palette's "Add project" row; `b` groups, `o` sorts. `p` is free on main, kept free for #2996.
8. CORRECTED and drawn: on a Context error only attached and hand-made cards show (stale frame: 2 rows).
9. CONFIRMED in code: no card has a stale field, so linear-expired marks the last read only in the line under the title. Still open: spec §3.1's per-card "as of 13:52" mark is a spec call (see 15).
10. CONFIRMED: the Projects tab counts grouped rows + Finished, never Hidden.
11. CONFIRMED: Group by Repo uses each open non-draft PR's repo; a project with no open PR sits in "No open PRs".
12. NEW (decision) and drawn: a failed truth read shows no health groups and one line (truth-failed frame).

## Still open
13. The top-bar pill reads "Sindri running"; the pill is app-wide, so ui should confirm the wording.
14. truth-failed falls back to grouping by State while the Group button keeps Health; the decision only says "no health groups".
15. Spec §3.1 card mark "counts show 'as of 13:52'": no frame draws it; the code has no stale field; spec call.
16. Finished split under stale: 7 of 10 are attached or hand-made (invented).

## Design picks (app only; nothing in the code to check)
11. Row layout counts: 14 rows in view on Projects (five group heads), 11 in linear-expired. (lane 12)
12. At 1000 with the peek: Items, Agents, Target and the reason drop. At 1280 with the peek (no frame): Agents drops first (notes.md). (lane 13)
13. needs-me-empty and my-work-empty keep search, sort and group; their footers drop the row keys. (lane 22)
14. The footer hint "b group" takes focus (drawn in focus-controls). (lane 23)
15. State dots are rounded squares. (lane 25)
16. The CI-not-checked dot is a hollow ring (--ink-muted); its group head reads "CI not checked". (lane 26)
17. The group menu's row and button both say "No grouping". (lane 27)
18. Menu hint ink #a4a9b2 is not a token; the nearest tokens are InkSubtle #8a8f98 and InkMuted #d0d6e0. Pick one at build. (lane 28)
19. A list cut by height draws the cut group's head when it fits (▾, open: its rows are below the fold), then stops. No Finished after a cut. (lane 29)
20. Under about 120 px the reason column shows the level dot only. (lane 30)
21. first-run hides the bar like Triage and has no state line: the empty state already offers Connect Linear. The primary is Add project (no key).
22. The kept-place cue: a 1 px left edge on the row plus the `<1m` age (projects frame, Import wizard, by hand).
23. Invented data: Checkout rewrite and Import wizard are by hand (no lead). In the server tests Checkout rewrite is Linear-led, which doesn't matter for the frames. (lane 9)
24. Group by Repo shows a two-repo project in both groups; the group counts then add up to more than the projects. Spec gives no rule.
25. "counting…" applies to Linear-led rows only; by-hand rows count items from their PRs' links (no re-read). Docs portal stands in for an attached project.
26. Hidden projects are not in the Projects 32 count.
