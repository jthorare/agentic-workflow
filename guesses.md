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

## Open questions (need a spec line or the server)
1. The spec's group list has no group for the server's Unknown level; frames draw 'CI not checked'. The spec needs one line. (Lead's decision, round 9: groups Off track 3, At risk 3, CI not checked 3, On track 5, No open PRs 8.)
2. CONFIRMED and drawn: on a mismatch the row appends "· Linear says on track" as its own span that never shrinks; the server's reason ellipsizes first. (lane 2, review-r8 H6, review-r9 H2)
3. "Blocked on you" stays in the frames (lead's call). Two gaps on the server: the projects answer has no GitHub login, and a red PR never carries `waitingOn`. A by-hand project has no lead, so "you lead it and its CI is red" never fires for one: Checkout rewrite's ask is now "review", not "fix CI". If a by-hand project's maker should count as its lead for that rule, the server needs to say so. (lane 5, review-r8 H5)
4. "Nov 12 passed": no past-target flag or wording exists. It needs the server or an app rule. (lane 19)
5. The stale line's "as of 10:31": the served text is "synced N min ago". The app would format each freshness row's `at` to get "as of". (lane 21)
6. The top-bar pill reads "Sindri running" in the frames; the pill is app-wide, so ui should confirm the wording.
7. Create project has no bare key (lead's decision): `n` is the app-wide quick-add and `p` is #2996's priority. Button + palette entry only. `b` for group is free in the app, #2996 and the §14 sheet. ui should confirm.
8. My work = projects you lead, attached (spec §0: "a Linear project the owner drives but doesn't lead") or made by hand. No spec or server rule defines any view's membership; spec §14 needs one line for the four views.
9. stale keeps every column (truth is served from Sindri's store since #3718); spec §3.1 state 3 says "names only". The spec needs a line. (review-r8 small 4)
10. linear-expired shows Linear-led rows as last read 3d ago; the per-row data is not marked stale beyond the line under the title.

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
21. first-run hides the bar like Triage and has no state line: the empty state already offers Connect Linear. The primary is Create project (no key), per the §14 sheet.
22. The kept-place cue: a 1 px left edge on the row plus the `<1m` age (projects frame, Import wizard, by hand).
23. Invented data: Checkout rewrite and Import wizard are by hand (no lead). In the server tests Checkout rewrite is Linear-led, which doesn't matter for the frames. (lane 9)
24. Group by Repo shows a two-repo project in both groups; the group counts then add up to more than the projects. Spec gives no rule.
25. "counting…" applies to Linear-led rows only; by-hand rows count items from their PRs' links (no re-read). Docs portal stands in for an attached project.
26. Hidden projects are not in the Projects 32 count.
