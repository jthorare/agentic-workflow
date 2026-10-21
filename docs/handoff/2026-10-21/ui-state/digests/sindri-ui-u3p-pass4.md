# #224 U3' pass 4 handoff

Branch feat/sindri-ui-u3. origin/main (e79bfc2, #232) merged at cc2ab112 before the work.

## Tasks
- T10 key frames: 77852745 (FrameHost.cs harness, NeedsYouFramesTests: fixture, empty, stalled, rebuilding, pulse, seen, reduced motion, show-all, home-large), 38141e9a red + 529f8293 fix (DecisionsFramesTests: sheet, decided, 409 decided-elsewhere, voided, cleared, requests, failure, profile). Frames pass on first run (existing behaviour), so no red for them.
- 409 focus assertion: red 38141e9a, fix 529f8293. Real bug: Raise() rebuilt the option buttons so focus fell to a Border; DecisionSheet.RestoreFocus puts it back on the same option's new button.
- T11: 56bc0f90 WindowsDockBadge (ITaskbarList3 overlay via vtable, red dot), WindowsDockBadgeContractTests (skipped off Windows), CompositionTests.Composition_builds_with_a_dock_badge. Not red-first (the stub cannot pass MA0182); never run on Windows. Mac badge and AvaloniaTray rows already existed. Follow-up fix (last commit): Composition cannot use Avalonia.Application (ARCH-UI-001), so Aw.Sindri.Ui.Views.MainWindowHandle (public, PublicAPI entry) supplies the handle.
- T12: red cd075e06, green d2203e07. Verbs request, decide, fail, unfail, items --large (+12 requests across <repo> and <repo>-two). Tests use stub SINDRI and fake curl. Waits on #253 for a real run: sindri decide and POST /sindri/requests are Pending P4 rows on main; `fail` assumes profile validate accepts an unknown service (unverified).
- T13: 7a54c983 docs/csharp/sindri-ui.md U3' section. CliText backstop: CliText does not exist in this tree, not done.

## Gate (final commit)
build -warnaserror 0 warnings; Ui unit 1243/1243; integration 454 total, 444 passed, 0 failed, 10 skipped (4 new are Windows-only skips); no "error", no exit 134 (unit/integration counts taken before the last small commit, which only touched Composition; frames, Composition and arch re-run green: 44/44 and 117/117); format exit 0; check-deps ok; sync-rules ok; sindri-ui-fixture.test.sh exit 0 (79 ok).

## Left
CliText backstop, T7 (if still open), T14-T20, u3-file-request frame (no filing form exists), live P4 run of fixture verbs after #253, WindowsDockBadge on a Windows box.
