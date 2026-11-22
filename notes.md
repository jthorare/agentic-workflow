# Projects frames (#3717): notes

## Current rules (round 12; these win over anything in the round logs below)
- Clock Sat 14 Nov 2026, 10:42. The owner is `joi`, shown as "you" on rows. The freshness line's "Linear says you're Joi" is the owner's Linear display name (`viewer`, ProjectsPage.cs:12), not the GitHub login.
- Row: state dot · name · level dot + the server's reason (shrinks first) + "· Linear says <x>" on a Linear mismatch (never shrinks) · PRs (red, waiting, not checked, ready, then | draft) · Items done/total · Agents · Blocked on · Target · Active (`<1m` under a minute).
- The draft glyph sits after a divider (--ink-3, about 4.4:1); the glyphs before it add up to the reason's "of N". Drafts never count as open.
- Health groups: Off track, At risk, CI not checked, On track, No open PRs; then Finished (counted) and Hidden ("not counted"). Projects tab = grouped rows + Finished. Counts here: 3/3/3/5/8, Finished 10, Hidden 2, tab 32.
- Views: Needs me = a PR waits on you, or you lead it and its CI is red (ask "review" / "fix CI"); My work = projects you lead + projects made by hand; attached ones are left out (projects lane, 2026-11-22). By-hand projects have no lead.
- 0 projects: every view shows the first-run state ("No projects yet", Add project, Connect Linear); Needs me is the default view (first-run-needs-me). needs-me-empty / my-work-empty are for when projects exist but none is in that view.
- Column "Blocked on" in every view; Needs me cells add the ask ("you · review").
- Keys: j/k, Space, Enter, Esc, /, Ctrl+K, 1-4 views, o sort, b group. Add project has no key: the header button (+ at 1000), the empty states, and the palette's existing "Add project" row, which opens the sheet (today it copies the CLI; the build changes that). `p` is kept free for #2996. Empty views drop row keys.
- A list cut by height ends on a whole row or the cut group's head; no Finished after a cut.
- Failed truth read (Sindri can't read PRs, checks and agents): no health groups; one line "PRs, checks and agents couldn't be read just now · Retry"; rows fall back to State (the Group button keeps the owner's pick, Health, and the line adds "grouped by state until then"); PRs, Items, Agents and Blocked on are blank; Needs me shows no count (frame truth-failed).
- Origin of each project (server `origin`): led (Linear, you lead it: 4), attached (Linear, someone else leads it: 16), hand (2). My work = led + hand.
- Context can't answer (stale): led projects drop out; the 16 attached and 2 hand-made cards stay with their truth. Attached rows lose the Linear tail, Items and Active (those come from Context). Tabs 4 / 2 / 25 (18 + 7 attached or hand-made Finished); groups 3/2/3/2/8.
- One project = one data set in every frame. The only frame about an edit is projects-kept (Import wizard just edited, `<1m`, keeps its place; one hovered row). Empty frames take their tab counts from modified data (gen.py `counts`).
- "passed" after a target: the app compares the target with today (no server flag).
- Which state each problem frame is:
  - stale = spec §3.1 state 3, "Context can't answer". Linear and GitHub data are gone, but the truth read works, so the 18 kept cards still show health groups; the line adds "PRs and checks from Sindri".
  - linear-expired = the §14 sheet's "Linear connection expired". Linear-led rows show the last read; health groups stay.
  - truth-failed = the lane's decision 1, the failed truth read (a failing `sindri` freshness row). It has no spec state number. No health groups, one line.
  - When both Context and the truth read fail, the truth-failed rule wins: no health groups.
- Lines below marked superseded keep their history only.

## Round logs

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
3. [superseded] my-work: the 6 projects the owner leads, grouped by state.
4. triage-empty: no search/sort/group bar, one action (Back to Needs me, key 1); says what Triage will hold and when (#3002, needs Linear).
5. peek: Space on Billing v2 (Linear-led, 9 PRs). 420 px; the list keeps every column and stays usable. The note is in the peek.
6. search: "Checkout" typed, "2 of 32" in the field. Matches: Checkout rewrite (name, highlighted) and Billing v2 (its note).
7. menus: Sort menu open. menus-group (7b): Group menu open.
8. [superseded] not-connected: all 32 by hand; the quiet line "Linear not connected · Connect" (28 px, no box, no red) under the title; no "Linear says". The On track group is collapsed so the No open PRs rows (null health: blank reason, no PR glyphs) are in view.
9. needs-me-1000 (1000x700): the whole row fits; top bar condenses. peek-1000 (1000x700): the peek does NOT overlay the list; it takes 420 px and the list narrows to 580 px (grammar: "the list stays, narrows, keeps focus"; an overlay would hide the right half of every row while j/k moves the peek). The list keeps name, PRs, Blocked on and Active; Items, Agents, Target and the reason line drop (the reason is in the peek's Derived row). Esc closes the peek and the columns return.
10. no-linear-row: the by-hand project (Checkout rewrite) in the peek, built from the server's example verbatim: 4 PRs (ciRed decided Nov 12, waiting on joi and sam, unknown, draft), 3 of 5 items open, 1 running agent, health at risk "At risk: 1 of 4 PRs red, 1 unknown". Source reads "by hand, no Linear".

## Data
22 active projects: names, states and the targets the shots show come from meta's shots 00 and 11. The PR / item / agent numbers are invented to cover every PR state and every health level at least once: ciRed (SSO rollout, Tray hotkey, Billing v2, Checkout rewrite, Inbox redesign, Dark mode), waiting (Import wizard, Feature flags, SSO rollout, SDK cleanup), unknown (Usage metering, Release train, API v3), draft (Checkout rewrite, Dark mode, Rate limits), ready; health offTrack (red at least half: SSO rollout, Inbox redesign), atRisk (Tray hotkey, Checkout rewrite, Billing v2, Dark mode), unknown (3), onTrack (8), null (5).

## Round 3 changes
- Edited row keeps its place (peek frame): Billing v2 had its note saved just now. Its Active cell reads "now" but the row stays in its old slot (third in At risk, between 2h and 2d), and the peek says "Note saved just now. This row keeps its place, even though it is now the newest, until you leave Projects."
- Avatar initial is 11 px (minFontPx is 11 on every frame).
- [superseded] not-connected: the Unknown and On track groups are collapsed so every row ends inside the view (11 of 11).
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

## Round 5 (after r4 973a1979c6; ideas and qa holds)
Clock of the new frames is the same (Sat 14 Nov 2026, 10:42). All reason strings live in ONE block, `STR` at the top of gen.py ("strings"): swap the wording there and run gen.py.

Holds fixed:
1. triage-empty: no ticket number and no build name. "Triage isn't built yet. When it is, it will hold new Linear projects and project updates that wait for a decision: accept, decline, mark as duplicate or snooze. It needs Linear connected."
2. needs-me-empty (Needs me 0, same header and bar, key 3 button) and my-work-empty (My work 0) added. Needs me counts: a PR waits on your review, or your own PR has red CI.
3. search-none ("0 of 32", "No project matches “xyz”", Clear search Esc), loading (11 placeholder rows on the 40 px rhythm, no text, no tab counts), stale (28 px line under the title "GitHub and Linear data unavailable. Showing what Sindri read at 10:31 · Retry", the PRs column head reads "PRs as of 10:31"). "Sindri not running" reuses the app's existing panel; it has no frame here.
4. Long names: long-name-1000 (peek open at 1000) and long-name (peek open at 1440). Rule, drawn in both: the reason line shrinks first and gets its own ellipsis (long-name); the name keeps at least 140 px, then gets an ellipsis (long-name-1000: 168 px); the full name shows in a hover tooltip (drawn) and in the peek title (it wraps, never cuts).
5. Group hint reads "derived from each project’s open PRs" (projects, peek, menus, menus-group, no-linear-row, not-connected, stale, focus-controls).
6. Group = No grouping draws no group head (needs-me, needs-me-1000, peek-1000). The owner's own page says "you": peek chips read "waiting on you, sam".

Health look: an 8 px dot before the reason on every row with health: red Off track, amber At risk, hollow grey ring Unknown, green On track. A chip stays only for At risk and Off track (peek). State dots are now rounded squares, so no state dot shares a round pie shape with a PR glyph. A project with only draft PRs has no health, no group head of its own (it sits in "No open PRs") and draws no dot or reason (Rate limits).

Smalls: row cursor and selection are a 2 px primary-hi ring; focus-controls draws the same ring on a tab (My work), Sort, Group and a footer hint (g group); the row ring dims to the hairline colour while the search field has focus (search, search-needs-me) and the footer then reads "Esc clear · ↓ to the list · Enter open". "of N" in the search box counts the rows of the current view: Projects 32, Needs me 6 (search-needs-me: "1 of 6"), My work 6. Menu hint ink is #a4a9b2 (menu minimum contrast is now above 5.19). projects.css: #5e6268 is token --bar-fill and #70757c is --box-edge. [superseded] Which ask word wins: when a project has both a red CI of its own and a review waiting on you, the Ask cell says "fix CI" (CI first); the peek says it in "Asks you": "fix CI first (api#412), then review web#97" (no-linear-row). Kept-place row with the peek closed: 1 px primary-hi left edge (Billing v2 in projects, Active reads "now"). One hovered row is drawn (Usage metering in projects, grammar s2). Button and menu say the same words: "Last activity" and "No grouping". Footer: "/ search"; "c create" is gone (no create control is drawn).
Spec section 14 (the #3168 health table) versus #3718 groups: the lead files the spec note; not a frame change.

## Round 6 / 7
- Final #3718 strings (str3718-final.md) are in the STR block: "N not checked yet", "CI not checked yet for N of M PRs", singular PR, drafts excluded. "CI unknown" is gone from the frames (the peek PR chip reads "not checked").
- Dark mode (1 red, 1 draft, 1 ready) is now Off track: "1 of 2 PRs red"; group counts 3 / 3 / 3 / 5 / 8. Rate limits (1 draft) has no health.
- long-name frames: the long-named project is the last row, so the tooltip covers no text; long-name rows are in Last activity order. Lists end on a whole row (footerCut=0 in measure.txt). Empty-state buttons are text plus key badge everywhere; triage-empty footer drops search, sort, group. Focus rings are 2 px inside every control.

## Round 7 (review-r6 hold and smalls)
- Group order is never broken. When the height budget cuts a group, the list stops there: no later group and no Finished head (gen.py `by_health`, the `cut` flag). The rest of the list scrolls. projects, peek, menus, menus-group, no-linear-row and focus-controls now end inside On track; stale ends the same way. Order stays Off track, At risk, Unknown, On track, No open PRs, then Finished.
- Long name: both frames rename the same project, SSO rollout (Off track 2 of 4, Mira, Dec 7, 5/12). At 1000 the long-named row is last (it ties with Billing v2 at 1d), so its tooltip covers nothing.
- The SSO peek has "Asks you: review web#2215".
- long-name-1000 peek ends on a whole section (Open PRs); the Note sits below the fold.
- Narrow reason rule: when the reason has under about 120 px, the row shows only the level dot (long-name at 1440, peek open). The full reason is in the peek's Derived row.
- Focus: one ring, 2 px primary-hi, drawn inside the control. focus-controls draws the tab ring on Needs me, which has no pill beside it.

## Round 8 (qa on r7: 3 holds, 10 nits)
- long-name (Group = State, peek open): every group that fits, in order, rows by last activity. In progress 6 (Checkout rewrite first at 1h, the long-named SSO rollout last at 1d), Planned 6, then Paused 5 cut after 4 rows (Tenant isolation). Same budget rule as by_health (gen.py `by_state`). The name fits at 1440 (no ellipsis), so this frame draws no tooltip; long-name-1000 keeps it.
- Draft PRs never count as open. Every count in a frame follows that: the Checkout rewrite peek reads "Open PRs 3 · 1 draft · from acme/api and acme/web", matching "At risk: 1 of 3 PRs red". The other peeks (Billing v2 9, Import wizard 2, SSO rollout 4) have no draft and already agreed. On a row the draft glyph sits after a hairline, apart from the PRs that count (Checkout rewrite, Dark mode); a drafts-only row (Rate limits) has no hairline.
[superseded] - first-run (new): no Linear and no project. Tab counts 0 / 0 / 0 / 0, no search/sort/group bar (nothing to find), the §14 sheet's words: "No projects yet. A project groups issues and PRs and shows whether they are on track. Create one, or connect Linear to bring yours in." Primary: Create project (`p`); secondary: Connect Linear. The footer adds `p create project`.
- The CI-not-checked dot ring is --ink-muted (was --ink-subtle). (Its group is gone, see part 2.)
- A list cut by height draws the cut group's head when it fits (projects, peek, menus, menus-group, no-linear-row, focus-controls end on "No open PRs 8" and its first row; stale on the head alone); its rows scroll. Still no Finished after a cut.
- Peek: Derived shows the level once (dot + reason, no chip). The by-hand peek lists items once: "Items 2/5 3 open, from the PRs' linked items".
- needs-me-empty: "No PR waits on your review, and no project you lead has red CI."
- search-none footer: only "Esc clear" (0 rows: nothing to move to or open).
- Top-bar pill reads "Sindri running" on every frame, so it can't be read as Linear's state.
- List width with the peek open (no 1280 frame): fixed columns + gaps take 600 px, the 24 px insets 48, and name + reason need at least 220, so 868 px in all. Breakpoints of the list width: ≥ 868 every column; 808–867 Agents drops (it is in the peek; name + reason get 60 px back); < 808 the 1000 set (name, PRs, Blocked on, Active). At 1280 the list is 860, so Agents drops. Under ~120 px for the reason, the row shows the level dot only.

## Round 8, part 2 (the projects lane checked guesses.md against the code)
- Health groups follow the spec's list (spec:447): Off track, At risk, On track, No open PRs (the spec's "No links"; null health = no open non-draft PR). [superseded] The spec has no group for the unknown level, so its 3 rows (reason "CI not checked yet for N of M PRs", hollow dot) sit in On track, where nothing is red. Group counts are now 3 / 3 / 8 / 8 (gen.py `GROUPS`, `grp`, assert); the level counts stay 3 / 3 / 3 / 5 / 8. Open question: guesses 1.
- Row order is the served order: exact last activity, then name (ProjectCards.cs:40). SSO rollout is a little older than Billing v2 (both show 1d), so it is last in Needs me and the long-named row stays last at 1000.
- Kept-place row: Import wizard, now a by-hand project (only a by-hand project's activity moves on an owner edit, ProjectCards.cs:144). Its age reads `<1m` (Labels.NeedsYou.cs:74), not "now". Billing v2 (Linear-led) has no "note saved" line any more: a notes edit on a led project doesn't change its age, so nothing moves.
- The top bar's search is always "Search…" plus the Ctrl+K keycap, at 1440 and 1000 (Labels.cs:69, TopBar.axaml:91). The 1000 variant without the keycap is gone.
- "Blocked on you" stays as drawn.

### Key map (every key on the Projects page, one action each)
Shell keys that are taken everywhere: bare `g` starts the page chord (ShellViewModel.Keys.cs:26), `c` opens the item create box (:66-68), `n` opens the quick-add todo box on every page (ShellWindow.Overlays.cs:53-68), `j`/`k` move, `Enter` opens, `y` copies the row's CLI, `/` search, `?` shortcuts, Ctrl+K the palette, Ctrl+N the create form. `l` switches Work's view (Work only). Page-only keys on other pages (`a`, `d`, `x`, `h`) don't reach Projects.

| Key | Action on Projects | Where it shows |
|---|---|---|
| `j` / `k` | move between rows | footer |
| `Space` | open / close the peek | footer |
| `Enter` | open the project (also the peek's "Open" button) | footer, peek |
| `Esc` | layered: clear search when the field has text, else close a menu, else close the peek, else back | footer, search box, menus |
| `/` | search | footer, search box |
| `Ctrl+K` | palette | footer, top bar |
| `1` to `4` | Needs me, My work, Projects, Triage (needs its own handler: the shell passes no digits) | footer; badges "Back to Needs me 1", "Open Projects 3" |
| `o` | sort menu (no shell handler takes a bare O; O after G is the Connections chord) | footer |
| `b` | group menu ("group by"; no shell handler, and #2996 / the §14 sheet don't use it) | footer |
| [superseded] (no key) | Create project: no bare key (`n` is the app-wide quick-add, ShellWindow.Overlays.cs:53-69, and runs first; `p` is #2996's priority; lead's decision, round 10). Reach it with the "Create project" button in the header bar (a + icon at 1000, same accessible name), on first-run and my-work-empty, by Tab / Enter, or as "Create project" in the Ctrl+K palette | header bar, first-run, my-work-empty, palette |
| `↑` `↓`, `Enter`, `Esc` | inside an open menu: move, choose, close | menu footer |
| `↓` | from the search box to the list | search footer |
| `Ctrl+Enter` | Draft update (peek; on Needs you it is a page-only key) | peek |
| (empty views) | no row keys: `Esc` back, `/` search (views with a bar), `Ctrl+K`, `1-4`, on first-run | footer of first-run, needs-me-empty, my-work-empty, triage-empty |
| later, from the §14 sheet | `.` actions (#3171), `x` select (#3169), `s` state…, `a` lead…, `i` self-assign…, `p` priority…, `m` mine only (#2996) on a project row | not drawn yet; none of them is used above |
No key is bound to two actions on this page, now or once the sheet's row keys land.

## Round 9 (review-r8: 6 holds, 5 smalls)
- Health groups are the server's five levels in the enum doc's order: Off track 3, At risk 3, CI not checked 3, On track 5, No open PRs 8 (gen.py `GROUPS`, assert). OnTrack means every open PR was checked, so the not-checked rows never sit under On track. The spec's list needs one line (guesses.md).
- Every count comes from the row data in gen.py: tab counts (Needs me and My work from the rows), group heads, search "N of M" (search matches name, note and lead, gen.py `match_where`), the peek's "Open PRs" line (gen.py `prs_head`), "4 more open PRs".
- Search "Checkout": Projects 2 of 32 and Needs me 2 of 6 (Checkout rewrite by name, Billing v2 "matched in note").
- PR cell order: CI red, waiting, not checked, ready, then the draft after its hairline. The glyphs before the hairline add up to the reason's "of N" (Dark mode: 1 red + 1 ready = "of 2").
- linear-expired replaces not-connected, with the §14 sheet's words: "Linear connection expired · Linear projects last updated 3d ago · Reconnect Linear". Rows are Linear's last read. first-run keeps "Connect Linear" (spec §3.1 state 4: no Linear at all).
- By-hand projects have no lead: the peek reads "Lead none · by hand". My work = projects you lead plus the ones you made by hand (still 6). my-work-empty: "No projects of yours yet. Projects you lead or made by hand show here, grouped by state." Checkout rewrite's ask is now "review" (the "you lead it and CI is red" rule needs a lead).
- A Linear-led row whose Linear health differs spells out both: "At risk: 3 of 9 PRs red, 1 not checked yet · Linear says on track" (Billing v2). When the column is narrow the tail ellipsizes like any reason (peek frames, needs-me-1000); the peek's Linear says row keeps it.
- Empty views draw no row keys (first-run, needs-me-empty, my-work-empty, triage-empty).
- stale uses spec §3.1 state 3 verbatim: "Linear and GitHub data unavailable: <the error's one line>", here "GitHub API rate limit, resets at 10:58", then a Retry link (not in the spec's sentence).
- Column header: one name, "Blocked on" (issue #3717's row list), in every view. In Needs me the cell adds the ask: "you · review", "you · fix CI".

### Spec states, now drawn (review-r8 small 3)
- marks (new frame): the freshness line in the normal state with the re-read running (spec §3.1 and §4.3 words): "Linear projects synced 4 min ago · Linear issues: re-reading for project links, 1,200 / ~4,000 · GitHub 3 min ago · Linear says you're joi". Linear-led rows' Items read "counting…" (by-hand rows count from their PRs). Docs portal is an attached project no longer in the store: greyed name, reason "no longer synced from Linear", no counts; its peek offers Remove (not drawn). The list ends on Finished 10 and Hidden 2.
- Hidden (n) fold: after Finished in every list that reaches the end (marks, linear-expired), "Hidden 2 · projects you hid"; not in the 32.
- group-repo (new frame): Group = Repo. One group per repo of the project's open PRs (drafts don't count), acme/api 8 then acme/web 10, then No open PRs; a project with open PRs in both repos shows in both groups. The web group is cut by height after 9 rows (same cut rule).
[superseded] - The create sheet that the Create project button opens: the existing Add project sheet (spec §3.2), not redrawn here.

## Round 10 (review-r9: 2 holds, 3 smalls)
[superseded] - Create project has no key badge anywhere. It is a button in the header bar after Group ("Create project"; a + icon at 1000 with the same accessible name), on first-run and on my-work-empty, and a palette entry. At 1000 the search field is 150 px so the four tabs stay on one line.
- The reason line is three parts: the level dot, the server's reason in its own span (shrinks first, ellipsis), and the Linear tail "· Linear says on track" in a span that never shrinks. The tail is whole in every frame: needs-me-1000, peek, no-linear-row, long-name, projects, search, stale, group-repo. The server string is unchanged; the full reason is in the tooltip and the peek's Derived row.
- My work = projects you lead, attached or made by hand (no attached rows in the data, so still 6). my-work-empty: "Projects you lead, attach or make by hand show here, grouped by state."
- By-hand peek: Lead reads "none" (Source already says "by hand").
- stale: "Linear and GitHub data unavailable: Sindri's context store isn't answering (timeout after 5 s) · Retry" (spec state 3 is one Context error for both).

## Round 11 (review-r10: 1 hold, 4 smalls)
- Hidden is not in the Projects count. The fold reads "Hidden 2 · projects you hid · not counted" (spec §3: "Hidden (n) (projects the owner hid)"). The tab count = every grouped row + Finished: 22 + 10 = 32, never Hidden (assert in gen.py next to `HID`).
- [superseded] The freshness line's "Linear says you're …" shows the Linear viewer's display name (`viewer`, ProjectsPage.cs:12), not a GitHub login. The frames use the spec's sample name, "Jo Smith".
- group-repo: the PR column is headed "PRs in this repo". A project in two repos shows only that repo's PRs in each group (gen.py `REPO_PRS`, asserted to add up to the project's PRs). The reason stays the project's own, so "of N" there counts every repo.
- The group menu's Repo hint reads "acme/api, acme/web…", the same names as the groups.
[superseded] - focus-controls draws the 2 px inset ring on the Create project button too.

## Round 12 (qa's nits on round 11)
- Draft divider: --ink-3 with a 2 px gap (was --hair-strong, 1.61:1).
- Freshness line: "Linear says you're Joi" (display name, consistent with the owner everywhere else).
- Breakpoints written as ranges; Current rules block at the top; superseded lines marked.
- Projects lane decisions (2026-11-22): My work = lead + by hand; Add project (one name, no key); truth-failed frame; stale shows only attached and hand-made cards.
- New frame first-run-needs-me: 0 projects in the default view.
