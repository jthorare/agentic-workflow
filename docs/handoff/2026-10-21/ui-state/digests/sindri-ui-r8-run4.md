# sindri-ui-r8 review run 4: handoff

Worktree: /home/thor/repos/agentic-workflow/.claude/worktrees/sindri-ui-r8 (branch feat/sindri-ui-r8-contract). Head pushed: 7ef0078c.
Fix list: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/r8-review-run4-fixes.md

## Done
- origin/main (439193ce) merged as df6cd5ca. One conflict, tests/Aw.Sindri.Ui.Tests/Api/GoldenContractTests.cs `_notRead`:
  kept main's `queue, items, items-id, items-id-timeline, items-id-close` line, dropped the workflow-* names (this PR reads them).
- W7 done, 7ef0078c (single commit, no red-first split because the merge itself was the red):
  - tests/Aw.Sindri.Ui.Testing/WorkflowAnswers.cs: recipes keyed by the body's `id`; `NamedRecipeGoldens` is derived from disk (scenario -> id); HashKnown accepts the golden's own hash or an instance-reported one; unknown id or hash -> same 404 ("X@hash" only when a hash was given).
  - src/Sindri/Aw.Sindri.Ui/Api/Workflow/RecipeView.cs: gained Id, Hash, Version (the round-trip test demanded them).
  - SindriApiClientWorkflowTests / WorkflowGoldenTests read the recipe id from the project-build instance golden on disk; no test names a recipe file. New tests: golden named by its id + hash shape; wrong-hash 404.
  - UI unit lane on 7ef0078c: 914 passed, 0 failed. Nothing else of the gate has run on this commit.

## Not started (W1-W6, all cheap items, full gate, PR body)
Orientation already gathered:
- W1 files: tests/Aw.Sindri.Ui.Tests/Shell/ShellSheetKindTests.cs (lines ~65-76 XAML test, ~122-133 switch test, marker test ~165), tests/Aw.Sindri.Ui.Tests/Shell/LaneAnchorTests.cs (drop `u3 - recipe <= 4`), markers at ShellViewModel.Sheet.cs:36-37, ShellWindow.axaml:252-253 (inside `<Panel x:Name="SheetContent">`), SindriApiClient.cs:66-67.
- W2: marker text in ShellViewModel.Sheet.cs, ShellWindow.axaml, docs/csharp/sindri-ui.md "Lane anchors" (line ~396-412); DeepLinkKind has HomeNeedsYou, NeedsYou, Item, Decision, Service, Job, Workflow, Recipe, Node, Session, System. Screens spec section 8 is docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md:635.
- W4: ShellWindow.axaml SheetTitle (TextBlock at ~line 236); FocusOpenedSheet in the Shell view code-behind. W3 peers: follow ChordKeysTests:353.
- Heavy runs queue behind the box-wide slot; a single test run took several minutes of queue wait.

Gate to run at the end (each via ~/.agentic-workflow/bin-r1b/heavy under timeout 600): UI unit+integration lanes, Aw.Architecture.Tests, Aw.Sindri.Tests unit lane, dotnet format --verify-no-changes (exit 0 on final commit), check-deps, check-rules, LC_ALL=C scripts/sync-rules.sh --check, whole-solution dotnet build -warnaserror. Then gh pr view 215 mergeable and a "Review run 4" PR body section (keep the last line).
No Temporal servers were started or killed by this run.
