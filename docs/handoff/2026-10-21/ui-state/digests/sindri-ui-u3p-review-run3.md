# #224 review run 3 fixes: PARTIAL handoff (M1 done; builder stopped at context guard)

Worktree ~/repos/aw-wt/sindri-ui-u3-r1 (branch fix/u3-r1). Head 5124baf6, pushed to feat/sindri-ui-u3 (origin/main 6b958edf already merged). Brief: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/u3p-review-run3-fixes.md.
Helpers (may be gone): /tmp/claude-1000/s/t.sh (heavy dotnet test --project tests/Aw.Sindri.Ui.Tests ARGS; HEADN=n) and c.sh "msg" (commit all + push with trailers).

## M1 confirmed and fixed
Confirmed with a throwaway console project (headless Avalonia 12.1.3): inside the AppBuilder.Configure factory SynchronizationContext.Current is NULL; it is AvaloniaSynchronizationContext only after SetupWithoutStarting. AvaloniaSynchronizationContext.InstallIfNeeded() (public) called in the factory installs it.
- red 97b5d634: tests/Aw.Sindri.Ui.Tests/Views/UiContextStartupTests.cs (3 tests; also renamed UiContext.Post -> PostOrInline, arch-r3-5).
- fix 5124baf6: SindriApp.Create calls InstallIfNeeded first; UiContext throws InvalidOperationException when it has no context and is posted to from a thread other than the one that built it (same thread: inline, for plain unit tests). Tests with pool-thread timers and no UI thread now install tests/Aw.Sindri.Ui.Tests/InlineUiContext.cs (HealthViewModelTests, FreshnessTests, ShellStateTests).
- Ui.Tests unit+integration after the fix: 1880 total, 0 failed, 10 skipped.

## Not started: M2, M3, M4, M5, the cheap items, final gate
M2 plan (read already): add Reason to DecisionOption (record in ViewModels/NeedsYou/DecisionOption.cs); DecisionOptions.From x2 fill it (pending -> served Cli, cli-only -> Labels.ProtectedTooltip, controls overload -> c.Tooltip); both templates (Views/NeedsYou/DecisionSheet.axaml, Views/Item/ItemSheet.axaml) bind ToolTip.Tip, ToolTip.ShowOnDisabled, AutomationProperties.HelpText; add a focusable InkSubtle line (Labels.Arrives(stage) + " · " + cli) once under the decision. The item sheet's Func<ActionReadiness> (ItemSheetViewModel.cs:32) has no stage: widen it to carry the stage (CatalogReadiness.Parse gives it). Re-read on catalog arrival: CatalogViewModel is an ObservableObject whose Controls changes in Show(); subscribe in ItemSheetViewModel and have ItemNeedsViewModel rebuild from its last DecisionList.
Notes: analyzers IDE0058 (use `_ =` on Assert.IsType), IDE0053/IDE0021, CA1031, MA0219 (no <c> without language), MA0002 (StringComparer.Ordinal), 300-line limit.
Final gate still to run: build -warnaserror + Desktop head build, Ui.Tests unit+integration, Aw.Architecture.Tests, dotnet format, check-deps, LC_ALL=C scripts/sync-rules.sh --check, sindri-ui-fixture.test.sh. PR-body not-taken: arch-r3-1, test-r3-4 (plus the earlier run-2 cheap items listed in sindri-ui-u3p-review-run2.md).

## NB2 (coordinator addition): done
Red de37dd92 (Views/PrimaryButtonStyleTests: a primary command button's template fill differs from a secondary one and is the primary token; measured on the ContentPresenter background rather than a captured frame). Fix: Button.command-control.primary styles (fill Brush.Primary, text Brush.OnPrimary, hover Brush.PrimaryHoverFill, pressed Brush.PrimaryPressed, not when disabled) in Views/Controls/ControlStyles.axaml. Ui.Tests after: 1881 total, 0 failed, 10 skipped.
M1's required red test is UiContextStartupTests (builds SindriApp.Create on a thread with no context).

## Continuation (Sonnet builder, 2026-10-16): M2-M5 done, gate green, head 189af5b0 (pushed to feat/sindri-ui-u3)
- M3 (corr-1): red 7525e17f, fix 727f4d90 (HasNewWord in ReportLook's before tuple).
- M4 (test-r3-2): ce7f91e7 (NB1 tests press Decline, assert "decline", wait on DecideCalls).
- M2 (a11y-r3-0): red e0166655, fix ec0aa0dd. DecisionOption.Reason, DecideState (reason + line), NeedsYouAction.Stage, ItemDecisionRow.DecideLine, DecisionSheetViewModel.DecideLine, ItemNeedsViewModel.Create/Rebuild (rebuilds on catalog Controls change), both templates bind ToolTip/ShowOnDisabled/HelpText, focusable pending-line Border under the decision. View test in NeedsYouHomeTests.
- M5 (docs): 8cbb7118 (new word, item-sheet readiness, PublicAPI list, presentation bullets moved into the U3' section, StripLargeShare summary).
- Cheap: corr-2 red cc08d0f8 + fix b43cc688 (A needs an OptionId on a decision row); a11y-r3-1 (NewWord Raw, b43cc688); test-r3-5 (b43cc688); arch-r3-6 (b43cc688). NOT taken: test-r3-3 (L4 focus-while-inactive), test-r3-6 (focused selected outlined row ring).
- Merged origin/main twice (69396b8f #268, 189af5b0 #270).
- Gate on 189af5b0: Ui.Tests unit+integration 1888 total, 0 failed, 10 skipped; format exit 0; Ui.Tests and Desktop head build -warnaserror 0 warnings; Aw.Architecture.Tests 117/117 (run before the last two merges and cheap items); sindri-ui-fixture.test.sh 117 ok, 0 not ok, exit 0; check-deps ok (20 packages, 63 edges); sync-rules up to date (those three run before the final merges).
