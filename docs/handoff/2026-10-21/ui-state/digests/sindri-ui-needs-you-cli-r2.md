# Needs-you: each action shows its own command, review run 2 fixes

Branch fix/needs-you-action-cli (worktree ~/repos/aw-wt/sindri-ui-nycli), head 809ecae91, pushed. PR #281 untouched.
Merged origin/main twice (7b47e49 era, then f740a70 with #285) before the tests.

- r2-0 done: SheetDetail gained `Command` (bare command a copy takes); `Value` is documented as the displayed text (keeps "Not in the app yet · " prefix). FailureDetails fills Command from Action.Cli. Correction to the r1 handoff: "Value carries the command alone" was wrong for pending actions; copy must use Command.
- r2-1 done: two sheet view tests (hint caption visible with peer GetName() == hint; none when the command is complete). Green on arrival (guards), since the axaml already drew it.
- r2-2 done: unknown-service golden pins Pause.Cli = catalog form with <name> -> subject id, empty FillInHint (green guard).
- r2-3 done: dropped the no-op AutomationProperties.Name on the hint TextBlock in NeedsYouRow.axaml; protected tooltip/HelpText = Labels.WithHint(ProtectedTooltip, hint) (red first).
- r2-4 done: ApproveProfile expectation derived from CatalogCli.

Commits: 9ccc4fd7b test (red: 3 failing), 809ecae91 fix.
Gate on 809ecae91: build -warnaserror 0 warnings; Aw.Sindri.Ui.Tests 1903/0 failed (all lanes); Rendering.Tests 21/21; Architecture.Tests 117/117; format 0; check-deps ok; sync-rules ok.
