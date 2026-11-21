# Projects frames (#3717): notes (round 2: truth fields from #3718)

Clock of every frame: Sat 14 Nov 2026, 10:42. The owner's login is `joi` ("you"). Base: ../projects-2961-r4 (spec section 14): same top bar, `.tabs` row, `.group`, `.row`, `.peek`, `.chip`, `.bar` and tokens from ../grammar-2978/grammar.css. New classes are in projects.css.

## Where the row's data comes from (server add-on, scope-3718.md "Truth JSON example")
Per project: `openPrs[]` (state draft | ready | ciRed | waiting | unknown, decidedAt on ciRed, waitingOn on waiting), `openItems`, `doneItems`, `totalItems`, `runningAgents`, `health` { level onTrack | atRisk | offTrack | unknown, reason } or null. Nothing else is read.
- PRs cell: one glyph + count per state, in this order: ciRed (x), waiting (3/4 ring), unknown (ring with ?), draft (dashed ring), ready (ring with dot, green). Unknown is never green. There is no conflict and no changes-requested state (#3720).
- Items cell: done/total. Agents cell: runningAgents. Blocked on: "you" (bold) when waitingOn holds joi, or when the owner leads the project and one of its PRs is ciRed; otherwise the first waitingOn login. In Needs me the cell adds the ask: "you · review" or "you · fix CI".
- Health: the one-line reason on the row is the server's `health.reason` word for word ("At risk: 1 of 4 PRs red, 1 unknown", "Off track: 3 of 5 PRs red", "CI unknown for 2 of 5 PRs", "On track"). `null` (no PRs and no items) shows nothing on the row. Linear's own health is a separate field: it shows in the peek as "Linear says ... differs: Sindri says ..." and is never merged into the row's reason.
- Target is the card's own field; no health rule reads it any more.

## What changed against the sheet
- The tab row is the segmented control (Needs me 6, My work 6, Projects 32, Triage 0, keys 1-4). The sheet's right-hand "Filter" box is now the one bar: `/` search over name, note and lead; Sort (last activity, target date, name); Group (state, health, repo, none). Header, bar and column heads stay on screen; only the rows scroll. These frames are the page scrolled to the top.
- Row = 40 px: state dot, name + health reason, PRs, Items, Agents, Blocked on, Target, Active. The note is not on any row; it shows in the peek. Search matches the note and says so ("matched in note") instead of printing it.
- Groups in the Projects view follow `health.level`: Off track, At risk, Unknown, On track, No open PRs (null), then a collapsed Finished fold (10). Rows in a group sort by last activity.
- A row keeps its place after an edit until the owner leaves the page: the order is computed once when the page opens.

## Per frame
1. needs-me: 6 rows (a PR waits on joi, or the owner's own PR is ciRed). Sorted by last activity.
2. projects: all 32 (22 active rows in health groups plus the Finished fold). 15 rows fully in view.
3. my-work: the 6 projects the owner leads, grouped by state.
4. triage-empty: no search/sort/group bar, one action (Back to Needs me, key 1); says what Triage will hold and when (#3002, needs Linear).
5. peek: Space on Billing v2 (Linear-led, 9 PRs). 420 px; the list keeps every column and stays usable. The note is in the peek.
6. search: "Checkout" typed, "2 of 32" in the field. Matches: Checkout rewrite (name, highlighted) and Billing v2 (its note).
7. menus: Sort menu open. menus-group (7b): Group menu open.
8. not-connected: all 32 by hand; the quiet line "Linear not connected · Connect" (28 px, no box, no red) under the title; no "Linear says". The On track group is collapsed so the No open PRs rows (null health: blank reason, no PR glyphs) are in view.
9. needs-me-1000 (1000x700): the whole row fits; top bar condenses. peek-1000 (1000x700): the peek does NOT overlay the list; it takes 420 px and the list narrows to 580 px (grammar: "the list stays, narrows, keeps focus"; an overlay would hide the right half of every row while j/k moves the peek). The list keeps name, PRs, Blocked on and Active; Items, Agents, Target and the reason line drop (the reason is in the peek's Derived row). Esc closes the peek and the columns return.
10. no-linear-row: the by-hand project (Checkout rewrite) in the peek, built from the server's example verbatim: 4 PRs (ciRed decided Nov 12, waiting on joi and sam, unknown, draft), 3 of 5 items open, 1 running agent, health at risk "At risk: 1 of 4 PRs red, 1 unknown". Source reads "by hand, no Linear".

## Data
22 active projects: names, states and the targets the shots show come from meta's shots 00 and 11. The PR / item / agent numbers are invented to cover every PR state and every health level at least once: ciRed (SSO rollout, Tray hotkey, Billing v2, Checkout rewrite, Inbox redesign, Dark mode), waiting (Import wizard, Feature flags, SSO rollout, SDK cleanup), unknown (Usage metering, Release train, API v3), draft (Checkout rewrite, Dark mode, Rate limits), ready; health offTrack (red at least half: SSO rollout, Inbox redesign), atRisk (Tray hotkey, Checkout rewrite, Billing v2, Dark mode), unknown (3), onTrack (8), null (5).

## Round 3 changes
- Edited row keeps its place (peek frame): Billing v2 had its note saved just now. Its Active cell reads "now" but the row stays in its old slot (third in At risk, between 2h and 2d), and the peek says "Note saved just now. This row keeps its place, even though it is now the newest, until you leave Projects."
- Avatar initial is 11 px (minFontPx is 11 on every frame).
- not-connected: the Unknown and On track groups are collapsed so every row ends inside the view (11 of 11).
- needs-me, needs-me-1000, peek-1000: the column is headed "Ask" and holds only the ask ("fix CI", "review"); the group row says all six wait on joi.
- peek-1000: Import wizard's note is real text now.
- triage-empty: the button has no key badge; "or press 1" sits beside it.
- A target before the clock is red and says "passed" ("Nov 12 passed").

## Reason strings used (server wording, scope-3718.md)
- At risk: "At risk: {red} of {total} PRs red" and "At risk: {red} of {total} PRs red, {unknown} unknown" (the doc's own examples: "At risk: 3 of 9 PRs red", "At risk: 1 of 4 PRs red, 1 unknown").
- Unknown: "CI unknown for {unknown} of {total} PRs" (doc example "CI unknown for 2 of 5 PRs").
- Off track: "Off track: {red} of {total} PRs red" (the doc has no off-track example; this is the at-risk pattern with the level word, red at half or more).
- On track: "On track: {total} open PRs, none red" ("On track: 1 open PR, none red" for one PR). Never a bare "On track" on a row.
- Null health: no text.

## Round 4 (api's exact strings)
Health null = no open PR. Those rows (7) sit in the group "No open PRs", with no reason; they keep their Items count when they have items. All reason strings are api's: off track, at risk (with ", N unknown" tail), "On track: N open PRs, none red", "CI unknown for N of M PRs".
