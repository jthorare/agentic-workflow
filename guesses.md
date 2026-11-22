# Guesses in the #3717 frames (rules I had to decide myself)

1. Health levels and reasons follow the server add-on only: no target-date rule, no "main red", no "under half done", no review-wait rule (the server derives health from PR states). A project's target is shown as data and never colours the row.
2. The row's reason shows `health.reason` unchanged; Linear's health is kept apart (peek only). The spec's "At risk · Linear says on track · ..." row text would change the server's string, so it is not on the row.
3. "On track" has no counts in its reason; the server examples only show at-risk and unknown reasons. I used the plain "On track".
4. The `unknown` level has its own group, "Unknown"; `null` is the group "No links" with an empty reason. Group names are mine.
5. "Blocked on you": waitingOn contains joi, or the owner leads the project and one of its PRs is ciRed. The ask words "review" and "fix CI" are derived from those two facts. No "conflict", "changes" or "unblock" (not built, #3720).
6. Needs me is sorted by last activity (the default sort); the Sort menu has only activity, target, name.
7. Footer keys after the divider: `1-4` view, `o` sort, `g` group, `c` create. `o` is a guess (#3717 names no sort key).
8. The 10 Finished names are in no shot, so Finished is a collapsed fold; the count of 32 holds.
9. Targets the shots do not show (Inbox redesign, Dark mode, Billing v2 and others) and every PR / item / agent number are invented. The by-hand project is Checkout rewrite; its PRs are the server example's (acme/api and acme/web).
10. PR glyph shapes are mine: x (ciRed), 3/4 ring (waiting), ring with ? (unknown), dashed ring (draft), ring with dot (ready). The grammar already has the 3/4 ring, dashed ring and ring-with-dot; only the ? ring is new.
11. A ciRed PR's `decidedAt` shows in the peek as "CI red · Nov 12" (date only; it is "as of", not "since").
12. rowsFullyInView is 15 in Projects (14 with the Linear line): 22 rows need 5 group heads and the 28 px column head. 13 in not-connected, where On track is collapsed.
13. At 1000 with the peek: Items, Agents, Target and the reason line drop. Without the peek the full row fits at 1000.
14. minFontPx 10: the avatar initial (grammar 9, set to 10). 11 elsewhere.
15. At 1000 the top bar shortens "Search or run a command" to "Search..." and drops the Ctrl+K hint.
16. Reason strings are api's exact text (ProjectTruths.cs:114-121); no guess left there.
17. Footer key `o` for sort: confirm with ui.
18. The edited row's cue is a line in the peek plus the "now" age; no mark on the row itself.
19. "passed" after a past target is my wording for the target cue.

## Round 5 guesses
20. Drafts do not count toward health (api's coming change): Dark mode now has 3 PRs that count, so it keeps "At risk: 1 of 3"; Checkout rewrite is "1 of 3 PRs red, 1 unknown" (its 4th PR is a draft); Rate limits (one draft) has no health. Group counts become 2 / 4 / 3 / 5 / 8. Singular and plural follow the same rule ("1 of 1 PR"). All of it is in the STR block.
21. The stale line says "as of 10:31", not 13:52: 13:52 would be after the frames' clock (10:42).
22. needs-me-empty keeps the full bar (search, sort, group) so the header matches; a search over zero rows does nothing, so the owner may want it hidden like Triage.
23. The footer hint "g group" is drawn as a focusable control (ring); the footer keys are not clickable in the grammar, so this is a guess that they take focus.
24. Focus is one control at a time in the app; focus-controls draws four rings in one frame to show the look.
25. State dots as rounded squares is my pick for "a different outline from the PR pies".
26. The Unknown level dot is a hollow grey ring; On track green, so no two levels share a look.
27. The group menu "No grouping" row and button both say "No grouping"; State and Health keep their names.
28. Menu hint ink #a4a9b2 is mine (one step above ink-subtle).

## Round 7 guesses
29. A list cut by the height budget stops at the cut and draws no Finished head; Finished shows only once the owner scrolls past No open PRs.
30. The "under about 120 px, dot only" threshold is mine; 120 px fits "Off track: 2 of 4…" plus the dot.
31. Ties in Last activity (SSO rollout and Billing v2, both 1d) keep any stable order; the frames put the long-named row last.
