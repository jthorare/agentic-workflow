# sindri-ui setup slice 2 (builder digest; REPLACES the Home-card design)

Worktree /home/thor/repos/aw-wt/sindri-ui-setup, branch feat/sindri-ui-setup. PR title: "feat(ui): setup checklist in Settings (F3 setup slice 2)".
Spec: ~/.agentic-workflow/mockups/f3/specs/setup.md (section 8 slice 2). NOT in this PR: slice 1 (TypedConfirmBox, HoldButton, AttentionFill), slices 3 and 4.

## Done (all pushed unless noted)
- Merged origin/main 8ca563e3a.
- API: POST /context/map/refresh client (ISindriApi.ContextJobs.cs, JobStarted, JobRoutes.RefreshContextMap), fake + handler arm + contract + spec-shaped goldens Golden/SpecShaped/context-map-refresh/{started,refused-running}.json. ui-f3-context was told the names (SendMessage) so they reuse them.
- Red commit d8a7c8a56: SetupViewModelTests, SetupActionTests, ShellSetupTests (28 of 40 failed, stubs compile).
- Green (uncommitted until gate): SetupViewModel/StepViewModel/ActionViewModel rewritten (checklist, 5 state words + Skipped, default-open rule, SegmentStrip kinds, Home line, map Retry = RefreshContextMap, 409 SND-JOB-409 shows as working), Labels.Setup.cs rewritten (UI-owned titles and why lines), DeepLinkKind.Setup + "setup" link, PageId.Setup removed, G U and every entry to Settings reveal the checklist (ShowPage -> Setup.RevealChecklist), SettingsViewModel.Setup (optional ctor arg), SettingsView = checklist first + "This app" below, Home line (HomePage SetupHomeLink), Views/Setup: SetupChecklist, SetupStepRow, SegmentStrip, SetupActionView (+ProtectedActionButton), SetupStyles. Old SetupCard/SetupPage deleted. Palette no longer lists Setup (Settings ⌘, only).
- New Setup tests all green in the Setup namespace (40) before SetupFramesTests was added.

## Decisions to report
- Row action sits in the row body (opens by itself for needs-you/failed), not in the collapsed header (a Button inside an Expander header toggles it).
- why lines are UI-owned for known step/state pairs (spec section 6), served `why` otherwise; done rows show served `detail`.
- Done panel with "Open Workflows" and palette entry NOT built (not in the brief).

## Left
- SetupFramesTests (written, not yet run), full gate, docs/csharp/sindri-ui.md update, PR.
## Learned
- Stubs trip CA1822/MA0041: use auto-properties. CA1502 limit 10. MA0002 needs StringComparer on string collections. IDE0031 wants `?.Event -= x`.
- Run tests: build via scratchpad build.sh, then heavy artifacts/bin/Aw.Sindri.Ui.Tests/debug/Aw.Sindri.Ui.Tests -namespace ...

## FINAL (2026-10-20)
PR #540 "feat(ui): setup checklist in Settings (F3 setup slice 2)", head e06475971 (pushed, merged with main). Gate: unit 2421, integration 760 (10 skipped), rendering 30, arch 130, scoped format + IDE0001 clean.
Bug found: Border.seg collided with the top bar's seg class (nav tabs shrank) -> setup-seg.
ui-f3-context: use ISindriApi.RefreshContextMap, DTO Aw.Sindri.Ui.Api.JobStarted (namespace Api, not Api.Jobs).
Left: nothing in slice 2; slices 1, 3, 4 and the done panel are other work.
