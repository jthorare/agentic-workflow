# Handoff — Sindri UI U1 (catalog, headers, freshness)

Worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-ui-u1`, branch `feat/sindri-ui-u1` (never push/PR).
Plan: `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/u1-plan.md`.
Verification plan (created 2026-10-12T15:23:30Z): `~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-u1/verification-plan.md`.
All dotnet: `AW_HEAVY_SLOTS=3 ~/.agentic-workflow/bin-r1b/heavy dotnet …`. Disk is tight: publish only to temp dirs, delete with `find <dir> -delete`.
PublicAPI helper: build to a log, then `python3 <scratchpad>/bin/publicapi.py <log>` (applies RS0016/RS0017).

## Done (commits on the branch)
- Merges: 0e9c849d (P1 feature branch, 7 conflicts resolved), 84b9272b (origin/main @1027a13f, clean),
  b55bb622 (origin/feat/sindri-api-p2-projector @62bfa0e9, clean). feat/sindri-ui-u0 @42e0b9d2: already up to date.
- 2eb52b69 Host test (headless never calls the opener).
- afac5fa3 (red) + green: IHumanTokenSource port + FileHumanTokens/InMemoryHumanTokens + contract; client sends
  X-Sindri-Token, X-Sindri-After once after an accepted command, reads ProblemDetails as (title, detail);
  Commands/Services/RebuildViews; GoldenFiles + RouteAnswer serve P1/P2 goldens and spec-shaped bodies.
- 179ab61a: ControlViewModel (ready/pending/cli-only + §4.1 lifecycle), CatalogViewModel, ViewsBanner, E-14 pill
  (word, problems, age from views.projectedAt), catalog read on connect, status + services re-read per event.
- next commit: CommandControl view, AvaloniaClipboard, stalled/rebuilding banner in ShellWindow, Health "ACTIONS"
  section (pending by phase + copy blocks), pill problems in TopBar.
- 2f9ad227: fixtures swapped to P2's real shapes.
- Gates: UI tests 609/609, architecture 114/114, check-deps/check-rules/sync-rules ok, format clean on touched files.
  Solution build fails only on P2's own tests/Aw.Sindri.Testing/InMemoryViewRows.cs:40 (CS8619), identical to P2's tip.

## Session 2026-10-12 (later)
- Q6 debounce: a3e5ce9d (red) + 7942547b (ControlPlaneFollower throttles event refreshes to one per view per 250 ms).
- Merged origin/feat/sindri-api-p2-projector @0ecd3bf4 (CS8619 fixed upstream).
- Palette: 7734cb81 (red VM), 980bc842 (VMs: ViewModels/Palette/*, Labels.Palette.cs, shell Palette/Shortcuts + paletteHistory),
  1b627312 (red view tests Views/CommandPaletteTests.cs), ae266a26 (Views/CommandPalette*, Views/ShortcutsSheet*,
  ShellWindow.Overlays.cs, TopBar ⌘K field click/Enter). UI tests 636/636. Branch pushed (draft PR #193; plain `git push` often).
- Owner answers: Q1 pill "Running" while views/health null; Q2 (re-read token + retry once on SND-AUTH-001) is #184's job,
  not in our port. Q3/Q4/Q5/Q6 as built.

## Session 2026-10-12 (end): items 2-5 done, branch pushed @a5321188 (draft PR #193, base feat/sindri-ui-u0)
- 2d6ecad3 key frames (tests/Aw.Sindri.Ui.Tests/Views/CatalogFramesTests.cs: u1-banner-stalled/-rebuilding,
  u1-control-sending/-accepted, u1-health-controls; CommandPaletteTests: palette, palette-filtered, shortcuts).
- efebb92d scripts/sindri-ui-fixture.sh (+ scripts/tests/sindri-ui-fixture.test.sh, AGENTS.md lines; bash-tests.sh
  discovers it, no list change). Guard = `SINDRI=true sindri-prove.sh guard`. stall row type service.ran (STALL_TYPE).
- e4d1b6ea docs/csharp/sindri-ui.md "U1" section; deps.yaml: Sindri files += sindri-app/auth/human.token, SindriUi
  file/http notes name the token, goldens and U1 routes.
- a5321188 palette views moved to Views/Palette (namespace Aw.Sindri.Ui.Views.Palette) for ARCH-CYCLE-001.
- Gates: solution build -warnaserror 0/0; UI unit 462/462, integration 178/178; architecture 114/114; check-deps,
  check-rules, sync-rules --check, fixture test (31 ok) pass; dotnet format clean except P2's
  tests/Aw.Sindri.Testing/InMemoryViewRows.cs:40 IDE0001 (API lane's file). Sindri unit lane: 7 red, all P2's
  (ListServices, GetProfile, RebuildViews slices + 4 ProjectorProgressContractTests stubbed "P2 unit B").
- Q2 note: our HealthViewModel.Failed still maps SND-AUTH-001 to Not running; #184's 025809d7 (retry once, then the
  Unreadable banner) brings it at the u0 merge. Not added here on purpose.

## Left
1. Merge feat/sindri-ui-u0 once #184 lands its token port (025809d7 and earlier): keep #184's port, delete ours
   (Platform/IHumanTokenSource.cs, Desktop/FileHumanTokens.cs, ControlFilePath.Token, Testing InMemoryHumanTokens +
   HumanTokenSourceContract + bindings, SindriApiClient.Calls.cs token code); expect a deps.yaml conflict on the
   human.token note (0941db8a) and on HealthViewModel.Failed.
2. Real-app proof (verification-plan journeys 1-11) once #184's token fix is merged: `env -u CLAUDECODE
   AW_SINDRI_PROVER=1`, scripts/sindri-ui-fixture.sh up/stall/unstall/down, NativeAOT sindri-ui
   (bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <tmpdir>), shots into proof/sindri-ui-u1/shots/. P2-dependent
   journeys (6-8, 10) need P2's projector (unit B) done.
3. Swap the spec-shaped fixtures for P2's goldens as they land.

## Session 2026-10-12 (merge U0 + P2 + main; real-app proof run 1), branch pushed @36b2ec1d
- 0194c5ae merge origin/feat/sindri-ui-u0 @caa56a51: kept #184's port (IHumanTokenSource, FileHumanToken, InMemoryHumanToken,
  ControlFilePath.TokenFrom, SindriPlatform.Token, client Request/Exchange/StaleToken/Refusal). Deleted U1's duplicates
  (FileHumanTokens, InMemoryHumanTokens + their contract bindings, ControlFilePath.Token, FakeSindriApi.HumanToken).
  U1 calls (Commands/Services/RebuildViews, X-Sindri-After) now live in SindriApiClient.Calls.cs on #184's Request and its
  one retry. Refusal: a 403 with a title (as #184), plus any other non-2xx whose ProblemDetails has an SND title and a
  detail; keyed off the code. HealthViewModel.Failed: only SND-UI-002 is Not running, every other error is Unreadable.
  The fake handler's RequiredToken serves P1's goldens human-no-token/human-stale-token. Host files take U0's side.
  deps.yaml: one note covering both. New guard tests: SindriApiClientHeaderTests.A_refused_command_is_sindris_code_and_detail_and_isnt_retried,
  HealthViewModelTests.A_refusal_that_isnt_a_403_is_unreadable_with_sindris_code_not_nothing_answered.
- 007bf882 merge P2 @c1a95108 (clean); 36b2ec1d merge main @345955f8 (clean, no C#).
- Gates on 36b2ec1d: build 0/0; UI unit 475/475, integration 180/180; arch 114/114; Sindri unit 3 red = P2's ListServices/
  GetProfile/RebuildViews only; Sindri integration 199/199; format clean solution-wide; deps/rules/sync ok; fixture 31 ok.
- Proof run 1: proof/sindri-ui-u1/{manual-test.md,evidence.md,shots/,raw/}. PASS: J1 (live header proxy), 2, 3, 4, 5, 9 (pending + cli-only),
  11, and Unreadable on a refusal. BLOCKED on P2: J6–8, 10, and 9's ready button. At c1a95108: /services, /profile and
  /views/rebuild answer 500 SND-API-005 (red stubs), no projector or views.db, status views/health null.
  Findings for the lead: F1 the palette opens scrolled to the first enabled entry (the Actions heading is hidden);
  F2 a command run from the palette has no visible outcome (the control renders only in the stalled banner).
- Proof recipe notes: the host needs DOTNET_ROOT=~/.dotnet (the system dotnet is 9). Tools are in proof/sindri-ui-u1/raw/tools
  (ev.swift with a frontmost guard and a key allowlist, win.swift, ax.swift, hproxy.py, shot.sh).

## Left
1. Merge origin/feat/sindri-ui-u0 again once #184's review-run-2 fixes are pushed (87339d5c+; still caa56a51 at 2026-10-12 end).
2. Re-run J6–10 once P2 unit B (the projector in serve) and the ListServices/GetProfile/RebuildViews slices are green.
3. Lead decisions on F1 and F2.

## Session 2026-10-12 (F1/F2 + U0 run-2 merge), branch pushed @00e251f0
- c9e21be4 red, then 5a731c1f fix for F1 and F2.
  - F1: `CommandPaletteViewModel.FollowsSelection` is false after open and every refilter; the view scrolls to the top
    and brings the selection into view only after ↑/↓.
  - F2: `CommandPaletteViewModel.RunPolicy` (`PaletteRunPolicy.StayOpen` default, `CloseFirst`) is the one decision
    point, provisional until the owner decides. The `PaletteEntry` lifecycle is: spinner + "Sending…" (disabled), then
    "<label>: done", or Sindri's code + the detail under it. Esc closes and the command keeps running.
  - Template parts use the classes entry-spinner and entry-error-detail (NamedControls).
  - Frames: palette-top, palette-sending, palette-done, palette-refused (copies in proof/sindri-ui-u1/frames/).
- 00e251f0 merges U0 @87339d5c (#184 review run 2).
  - Conflicts:
    - deps notes: U1's kept.
    - ShellWindow.axaml: live regions + palette layers.
    - Escape order: palette → ? → IsSheetShown.
    - FakeSindriHandler: #184's golden refusals/public writers + U1 routes; status always writes views/health null.
    - GoldenContractTests: #184's file + U1's routes, spec-shaped goldens and catalog test.
  - ShellViewModel.Overlays.cs split (AW1005).
- Gates @00e251f0: build 0/0; UI unit 485/485, integration 187/187; arch 114/114; Sindri unit 3 red (P2's three only),
  integration 199/199; format clean; deps/rules/sync ok; fixture 31 ok.
- Real-app run 2 @5a731c1f (manual-test.md "Run 2"): F1 PASS at 1280/900; F2 refusal (SND-API-005) shows in the open
  palette, Esc closes. The live sending→done phase waits on P2's RebuildViews accepting.

## Left
1. Re-run J6–10 and the F2 accepted path live once P2 unit B and the ListServices/GetProfile/RebuildViews slices are green.
2. Owner decision on F2's policy (flip `RunPolicy` if CloseFirst or another design is chosen).
