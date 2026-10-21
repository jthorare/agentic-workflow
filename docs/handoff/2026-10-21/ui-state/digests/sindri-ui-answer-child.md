# sindri-ui-answer-child: PR #345 run-1 fixes, handoff
Branch feat/sindri-ui-answer-child (worktree ~/repos/aw-wt/sindri-ui-answer-child). Pushed head: 55a0919a1 (== PR head reviewed; nothing new committed).
Review file: ~/.agentic-workflow/joi-fairshare-agentic-workflow/reviews/345.json (bodies under reviewers[].issues[].body).
Commit trailer: Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com> + Claude-Session line from the session reminder. Push after each commit.

## State of the 9 problems (all CODE edits below are UNCOMMITTED and UNBUILT/UNTESTED; no tests written yet for any)
1. NeedsYouMapper.cs:59 (correctness-0/architecture-0/accessibility-0): code edited (new Unanswerable(row) drops PrimaryAction/Actions/Cli for via=child, called at top of Entry()). Last build before that edit failed CA1502 (complexity 12); the Unanswerable refactor is meant to fix it, not yet built. LEFT: tests (mapper: no controls; peer tree has one control, Heard.Tree in tests/.../Views/Heard.cs).
2. InstanceFrameView.axaml.cs:77-87 (julik-0): edited as ChildrenFocus (InstanceFrameViewModel.Panels.cs: Children?.HasGroups ? Children : InitialFocus), used in FocusFor and the ChildrenRequested handler in InstanceFrameViewModel.cs. LEFT: test (no children -> WantedFocus is InitialFocus at once). Verify the Children panel VM property name `Children` exists on InstanceFrameViewModel.
3. ChildAnswerButton.axaml.cs:20 (architecture-1): edited: button raises ChildAnswerButton.FollowEvent; handlers added in HomePage.axaml.cs (OnChildAnswer -> shell.FollowChildAnswerFromStrip/FromNeedsYou) and WorkflowsPage.axaml.cs (-> WorkflowsViewModel.FollowChildAnswer -> ChildAnswerRequested -> ShellViewModel.Workflows sets OpenerWorkflow). New ChildAnswerRequestedEventArgs.cs. LEFT: build; test Esc returns focus to the button (OpenerStripFrame/OpenerWorkflowFrame focus the row's title/StripFrame, check what Esc focuses; may need the frame to focus the button instead).
4. ChildAnswerViewModel.cs:25 (accessibility-1): edited: Labels.AnswerInChildParentName ("Answer in the child, opens <parent> children") when no child. LEFT: update ChildAnswerTests/ChildAnswerButtonTests/ChildAnswerFramesTests expectations that used AnswerInChildName(parent).
5. ShellViewModel.Instance.cs:62 (testing-0): NOT DONE: shell-level test of ParentOfNeedsYouRow (match on address, on WFI id, fallback to row's own sheet).
6. InstanceRules.cs:16 (testing-1): NOT DONE: test with in-memory copy of a pointer golden with class "protected"; show red without the !IsChildPointer guard.
7. Satisfy detach fix (testing-2): NOT DONE: regression test (frame satisfies focus after window closes, queue idle, CancelAfter).
8. ChildAnswerButtonTests.cs:28 (testing-5): NOT DONE: use Heard.Tree (walks GetChildren) from the row down.
9. ChildAnswerFramesTests.cs:23 (testing-6): NOT DONE: fold the 5 full-shell windows into fewer; integration lane was 100.6 s, target < 90 s.
Also: resolutions/resolution_note for suggestions and nits in the state file (reviews/345.json) NOT DONE; counts folded/follow-up/declined not computed. origin/main was at 1d6bac7: merge it (no rebase) before the gate.

## Uncommitted files
M src/Sindri/Aw.Sindri.Ui/ViewModels/{Instance/InstanceFrameViewModel.Panels.cs, Instance/InstanceFrameViewModel.cs, Labels.Strip.cs, NeedsYou/NeedsYouMapper.cs, Shell/ShellViewModel.Instance.cs, Shell/ShellViewModel.Workflows.cs, Workflows/ChildAnswerViewModel.cs, Workflows/WorkflowsViewModel.cs}
M src/Sindri/Aw.Sindri.Ui/Views/{Shell/HomePage.axaml.cs, Workflows/ChildAnswerButton.axaml.cs, Workflows/WorkflowsPage.axaml.cs}
?? src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/ChildAnswerRequestedEventArgs.cs
Not committed because they have not been built; first step: build src/Sindri/Aw.Sindri.Ui -warnaserror via heavy, fix errors, then write the red tests (test-first was skipped for this batch).

## Last gate result (on pushed 55a0919a1 era, before this batch)
Ui.Tests unit+integration 2282 total/0 failed/10 skipped (integration 100.6 s: over the 90 s budget); Rendering 21/0; Architecture 121/0; build 0 warnings; format clean. Proof evidence in ~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-answer-child/ (home-1 deleted).
