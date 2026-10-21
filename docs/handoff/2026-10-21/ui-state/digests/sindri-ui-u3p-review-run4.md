# #224 review run 4 fixes: done (Sonnet builder, 2026-10-16)

Worktree ~/repos/aw-wt/sindri-ui-u3-r1, final head fb576f9f9, pushed to feat/sindri-ui-u3. origin/main had not moved.

- P1: red cfd19aca (Views/PrimaryRowFocusTests), fix d92dbde4: Border.item-row Button.command-control.primary:focus-visible rings in Brush.Ink (QueueStyles.axaml).
- P2: red (Item/DecideUnlistedTests), fix 1f8d1a02: DecideState.Read; unread catalog -> "Checking what can run" (Labels.CheckingCatalog); read but no Decide -> served command, else "Decide is not in the command catalog"; NeedsYouAction.CatalogRead set by the mapper (empty catalog = unread).
- P3: red 70c10942 (Item/DecideLineCopyTests), fix 5897287a: ItemDecisionRow.DecideCli/CanCopy/CopyName/LineName/CopyLabel, ItemNeedsViewModel.CopyDecide (clipboard from CatalogViewModel.Clipboard), DecisionSheetViewModel.CopyDecide/DecideCli/DecideLineName (optional IClipboard ctor arg, ShellViewModel passes it); Copy buttons in ItemSheet.axaml and DecisionSheet.axaml named "Copy: Decide <id>", visible only with a command.
- Cheap: line accessible name carries the decision id; cli-only line shows sentence + " · " + served per-option CLI (item rows use the decision's first option CLI); r4-2 test (UiContextStartupTests, SindriApp.Shell accessor); r4-3 docs sentence in docs/csharp/sindri-ui.md. NeedsYouHomeTests line 214 now matches Content as string (Copy button has a TextBlock content).
- Not taken (PR body): focus restore when the catalog flips Decide while the line is focused (a11y suggestion 1).
- Gate on fb576f9f9: Ui.Tests + Desktop build -warnaserror ok; unit 1387/0 failed; integration 510 total, 0 failed, 10 skipped; Aw.Architecture.Tests 117/117; format exit 0; check-deps ok (20 packages, 63 edges); sync-rules up to date; sindri-ui-fixture.test.sh all ok.
- Note: one earlier gate run showed two integration failures (UiContextStartupTests motion test, FocusRingTests top-bar Tab ring) that did not reproduce on rerun; the motion test was made robust (reads on the UI thread) in fb576f9f. The FocusRing one was not reproduced and is untouched.
