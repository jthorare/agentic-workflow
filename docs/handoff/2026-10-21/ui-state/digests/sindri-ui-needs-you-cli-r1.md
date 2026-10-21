# Needs-you: each action shows its own command, review run 1 fixes

Branch fix/needs-you-action-cli (worktree ~/repos/aw-wt/sindri-ui-nycli), head 4fcb88330, pushed. PR #281 untouched.

Merge: origin/main (7c142e9, #265) merged as 3e7f7f9bd; one conflict in NeedsYouMapper.cs (kept subject-passing Actions, took `row.PrimaryAction ?? string.Empty`). #275 move: none needed. No #281 test captures pixels (all headless), so nothing moved to Aw.Sindri.Ui.Rendering.Tests; the gate runs both projects.

r-0: the verbs exist on main (Host/Commands/ServiceVerbs.cs, ServiceCommandLine: `services run|pause|resume <name>`). The catalog golden now has RunService and PauseService at `ui: ready`, and the failure row's served cli is `sindri services run heartbeat`. So Pause showing `sindri services pause heartbeat` is correct; no "Arrives in" label added. Consequence: Pause is a Ready action, so it has no tooltip; its command shows on the sheet line (the old view test expected a tooltip and failed after the merge; reworked).

Commits (red then fix, each pushed):
- 4c22b06fa test: table of ActionCommand rules on catalog rows (r-7; CloseItem:done, Decide, StartInstance green guards)
- 541709bec red / 06f7e8cf4 fix: r-1 only `<itemId>` maps to the item (`<id>`, `<item>` stay unresolved; comment fixed); r-2 an optional `[...]` group is filled whole (brackets dropped) or left exactly as served, never half-filled
- 2a6645a61 red / 4fcb88330 fix: r-3 command and hint are separate (NeedsYouAction.FillInHint, SheetDetail.Hint drawn in its own caption TextBlock; tooltip/HelpText join them via Labels.WithHint; Copy and the sheet Value carry the command alone). Correction to the first handoff: Copy of a non-primary command is still not wired (y copies row.Cli); the hint is now ready for when it is. r-4 hint beside a protected inline command (IsProtectedWithHint). r-5 hint names open placeholders, deduped, in order ("fill in decisionId, choice"; Labels.FillInHint). r-6 view tests with an unfillable placeholder (pending: tooltip, help text, sheet Value + Hint; protected: hint text visible). r-8 sheet check uses Equal; the two guard tests (primary keeps row cli, catalog-less keeps row cli) were green from the start, say so in the PR body. r-9 `catalogCli` non-nullable, tests use NeedsYouKinds.Failure.
- r-10: no change (as the review said).

Gate on 4fcb88330: build -warnaserror 0 warnings; Aw.Sindri.Ui.Tests 1900 total, 0 failed (1890 passed, 10 skipped; all lanes); Aw.Sindri.Ui.Rendering.Tests 21/21; Aw.Architecture.Tests 117/117; dotnet format exit 0; check-deps ok; sync-rules ok.
Not done: the combined re-review.
