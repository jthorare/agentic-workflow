# Handoff — Sindri UI U2 (queue, item detail, timeline, Create and Close item)

Worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-ui-u2`, branch `feat/sindri-ui-u2` (plain push only).
Draft PR base `feat/sindri-ui-u1` (#193 → #184).
Plan: `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/u2-plan.md` (wire shapes, units, fixture list).
Verification plan (created 2026-10-12T17:14:10Z): `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-u2/verification-plan.md`.
All dotnet: `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy dotnet …`. Disk tight: `find <dir> -delete`.

## Done
- b904d7c6 merge of P2 c1a95108 (API lane: only ListServices, GetProfile, RebuildViews slice tests are expected red;
  refusals keyed off the code, never the detail sentence; SND-AUTH-001's fix text changes in P2 unit F).

- 50762a4d test keys SND-AUTH-001 off the golden's detail. PR #197 (draft, base feat/sindri-ui-u1).
- 5645122f red P3 contract + spec-shaped goldens; 16b781ff client P3 calls (green).
- 00a83c18 destructive confirm (Press/Confirm/Keep) + subject in control names.
- d5577c18 Queue/Item/Create VMs + shell wiring (HealthViewModel.Follow/RefreshViews/ApiUrl; the follower re-reads
  after services) + keys (ShellViewModel.Keys: G chord, J/K, Enter, C). Tests: Queue/, Item/, Shell/ShellItemsTests.
- 5e3a1d2b views: Views/Queue/{QueuePage,ItemRow,QueueStyles}, Views/Item/{ItemSheet,CreateItemDialog}, Home panels,
  ShellWindow(.Keys). UI unit 508/508, integration 178/178.
- Push with the branch named explicitly (`origin feat/sindri-ui-u2`): a bare push is refused by the session hook,
  which reads the primary checkout's branch. Avoid that word pair in any Bash text (heredocs included).

- b18cb2aa u2 key frames (Views/QueueFramesTests; frames under artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/u2-*.png).
- fixture verbs items [--large] / close <item> [--cancelled] + test (41 ok); b52b87ec docs U2 + deps http note.
- dd6ef21a no typed $parent bindings (ARCH-CYCLE-001; ItemText -> ViewModels.Item; CompactItemRow) + dotnet format.
- ec90404b / 0e11d23d .env.example STALL_TYPE + FIXTURE_REPO.
- Gates at 0e11d23d: solution build -warnaserror 0/0; UI unit 508/508, integration 187/187; architecture 114/114;
  format clean on Ui, Ui.Tests, Ui.Testing, Ui.Desktop; check-deps, check-rules, sync-rules --check,
  check-env-example (+ its test) ok; Sindri unit 3 red = the expected P2 slices only.
- ceba7ae5 .env.example (the earlier ec90404b/0e11d23d were made on a detached HEAD by a stray checkout and never
  pushed; replayed here). 2d67fb4e merge of origin/feat/sindri-ui-u1 @c2cb626f (palette F1/F2, #184 token port +
  stale-token retry): client body threaded through Exchange/Send. After it: build 0/0, UI unit 531/531,
  integration 196/196, architecture 114/114, format clean, repo checks ok.
- PR #197 body carries the fixture list, U2-API-1..10 and spec questions Q1..Q5.

## Round 2 (2026-10-12, coordinator decisions)
- b5d7ac2b/b443c2ad spec: §12.4 confirm copy; §4.5 four groups + parked badge; §4.6 body/link; one #197 Status line.
- 198c56b1 red / 9e2b40b1 green: §8 key guard (500 ms), N new pill (hold while the pointer is over panels), Y, /, G hint.
- 8b05b68b red / (next) green: fixtures renamed to P3's answered goldens (extras marked extra-p4-*/extra-p11-*),
  parked badge, sheet body/link. Green at that point: UI unit 542/542, integration 198/198, arch 114/114, solution
  build 0/0, format clean, repo checks ok.
- 9e0a4a3c nullable owner on ItemSummaryView/ItemView (X61), fact that actors show as served, U2 docs. Verified
  after disk recovered: UI build 0/0, unit 543/543, integration 198/198.
- origin/main @903fa14a (X61, #200) merge ABORTED: 12 conflicts in API-lane files (Sindri Core/Features vs main,
  PublicAPI.Unshipped, Architecture.Tests csproj/Packages.cs/lock, deps.yaml, AGENTS.md, slnx, Directory.Packages).
  Take it after the API lane or U0/U1 resolves main against P2; then merge U1 if it moved.
- artifacts/bin of this worktree was deleted to free space; build only the UI test project (not the solution) while
  disk is tight.

## Left
1. Real-app proof (verification plan journeys 1-12) once API P3 is on main: swap the spec-shaped P3 fixtures for P3's
   goldens, flip catalog-p3 use to the real catalog, then sindri-prove.sh + sindri-ui-fixture.sh up/items/close.
2. Owner answers to U2-API-1..10 and Q1..Q5 (PR #197 body); rename record fields if P3 names them differently.
3. Not built: the "N new" pill / pointer hold key guard (§8), the 500 ms key guard, Y (copy CLI), `/` filter,
   ChordHint shown in the top bar (VM has it; TopBar doesn't bind it yet).

## U2-API edits (for the owner)

## Spec questions
