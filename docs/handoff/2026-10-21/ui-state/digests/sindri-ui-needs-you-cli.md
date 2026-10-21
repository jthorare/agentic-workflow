# Needs-you: each action shows its own command

Branch fix/needs-you-action-cli (worktree ~/repos/aw-wt/sindri-ui-nycli), head 374eee10b, pushed; no PR opened. Base origin/main e711c3e97 (#265 still open; catalog golden says ui pending:P6 for service actions, cli comes from the row regardless).

Commits: ce4f39adf test (red), 374eee10b fix.
Fix: new ViewModels/NeedsYou/ActionCommand.cs (catalog cli + placeholders from subject: name=service id, item/itemId/id=row item, decisionId, hash=profile; primary action and catalog-less action keep row.Cli; unresolved required placeholder stays as served, NeedsYouAction.FillIn=true). NeedsYouMapper passes the catalog cli through its readiness dictionary. "· fill in" appended in the pending tooltip (NeedsYouRowViewModel.Tooltip) and the failure sheet line (FailureDetails); Labels.FillIn.
Tests: tests/Aw.Sindri.Ui.Tests/NeedsYou/NeedsYouActionCliTests.cs (4, unit), Views/NeedsYouActionButtonTests (Pause button tooltip, HelpText and sheet line; WithRows refactored to WithFeed).
Notes: the sheet detail line is the copyable text for Pause; the `y` key still copies row.Cli (primary). Red commit's view test failed for a wrong button label ("Pause" vs "Pause service"), corrected in the fix commit.
Gate on 374eee10b: Ui and Desktop -warnaserror 0 warnings; Ui.Tests unit 1391/1391, integration 512 total, 0 failed (502 passed, rest skipped); Architecture 117/117; format 0; check-deps ok; sync-rules ok.
Not done: the combined review.
