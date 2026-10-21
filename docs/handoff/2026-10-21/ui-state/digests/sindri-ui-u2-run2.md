# Sindri UI U2 (PR #197) review-run-2 fix pass, part 2: handoff

Worktree: /home/thor/repos/agentic-workflow/.claude/worktrees/sindri-ui-u2, branch feat/sindri-ui-u2. Pushed head: 0bf08eb8 (clean tree).
Fix list: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/u2-review-run2-fixes.md

## Done (test commit then fix commit, each item's UI tests green when run alone)
- M5 a11y-1: 6508865f (red), 6aef8097 (fix). Keep raises FocusRequested(onlyIfLost) when a kept row changes group/panel.
- M6 a11y-2: a3c263cb (red), 60c5aa8a. Neighbour(): opener row, _sheetOpener, SelectedRowFrame(), page.
- M7 a11y-3: efb01914 (red), 7e293b8d. ShowHeld(scope) focuses first new row; ShellWindow.OnQueueChanged posts KeepFocus when HasHeld goes false.
- M8 a11y-4: a23e0b07 (red), e76a104e. CommandControl Focus(NavigationMethod.Tab).
- M10 testing-1/arch-1 (+testing-3, testing-4): c62186c4. Fake timeline keyed by (item, after) (ItemTimelinePages, TimelineGates), IgnoreCancellation. Guard tests are NOT red-first; mutation-checked by hand (deleting either generation check fails them).
- M11 testing-2/corr-5/a11y-6: same test commit c62186c4 (red), aa0e5d80 (fix). LoadMore returns bool; failed page Say()'d.
- a11y-7: 65ddf279 (red), 349a2a57. a11y-8: e6a197e7 (red), 0bf08eb8.
- testing-3 (finally + WaitAsync on gated ItemSheetViewModelTests) and testing-4 (always-true Assert.Same dropped): in c62186c4.

## Left
1. corr-6: "N new" and "cleared" in the same refresh, only the last is spoken. Hold(QueueView) does Say(HeldText) before Apply, Keep() does Say(Labels.Cleared). Make one sentence (collect both, Say once at end of Refresh). Labels in ViewModels/Labels.Items.cs (Cleared, NewRows). Tests in tests/Aw.Sindri.Ui.Tests/Queue/QueueKeysTests.cs / QueueViewModelTests.cs.
2. arch-0: exclude destructive entries from the palette's Runs() (src/Sindri/Aw.Sindri.Ui/ViewModels/Palette/CommandPaletteViewModel.Runs.cs:~89). Test: a destructive ready entry is not a runnable stop and Enter does not press it (tests/Aw.Sindri.Ui.Tests/Palette/CommandPaletteViewModelTests*.cs).
3. git fetch origin; merge origin/main (>= 386126e4), message `merge: origin/main (<sha>) into feat/sindri-ui-u2`; keep both sides on add-only files; report conflicts.
4. Full gate via ~/.agentic-workflow/bin-r1b/heavy under `timeout 600`: Ui.Tests build -warnaserror + unit + integration lanes; Aw.Architecture.Tests; Aw.Sindri.Tests build + unit + integration; `heavy dotnet format --verify-no-changes` (report exit); node scripts/check-deps.mjs; node scripts/check-rules.mjs; scripts/check-env-example.sh; LC_ALL=C scripts/sync-rules.sh --check; bash scripts/tests/sindri-ui-fixture.test.sh; whole-solution `heavy dotnet build -warnaserror`; push; `gh pr view 197 --repo jthorare/agentic-workflow --json mergeable`.
   No full-project run of Ui.Tests has been done since part 1: only filtered runs.

Helpers (in /tmp/claude-1000, may vanish): ut2.sh '<method glob>' (one glob, timeout 600 via heavy, log /tmp/claude-1000/u2c-run.log), c.sh "<msg>" paths... (commit with trailer).
