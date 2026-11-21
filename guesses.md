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
