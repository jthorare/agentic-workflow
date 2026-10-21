# Sindri UI U8 review run 2 fixes (PR #274)

Branch feat/sindri-ui-u8, worktree ~/repos/aw-wt/sindri-ui-u8, head 9170b4f96 (pushed). Main merged twice (7c142e9, then 7b47e49 incl. #290).

## Per item (test commit then fix commit in git log, unless noted)
- W1 r2-0: ExpandedState signature carries Labels.ChildLine(child).
- W2 r2-1: ChildRow signature carries each action's Label=Name (late catalog rebuilds the rows).
- W3 r2-2/r2-3/a11y-r2-5: FrameIntent.OpenInitial (stacked frame gets FocusInitial once read), FrameIntent.FocusPointer / FrameFocus.ChildPointer (FrameOpenEventArgs.FromPointer; trail holds (link, viaPointer)); undrawn child falls back to InitialFocus (InstanceFrameView.GiveUp); a frame that fails to load with an intent focuses Back; an item sheet stacked from a frame focuses SheetTitle (ShellViewModel.SheetStacked). Headless: child frame holds focus before Back; Back after ExpandedChild lands on ExpandedChild (InstanceFramesTests.u8_frame_project_build).
- W4 r2-4/r2-5: LauncherContract (Aw.Sindri.Ui.Testing) bound to FakeLauncher (unit) and AvaloniaLauncher (integration, headless: only refusals checked, CanLaunch=false). FakeLauncher.AddFile; relative or unknown paths are false. AvaloniaLauncher catches platform exceptions -> false; ILauncher doc fixed. NOT taken: making ILauncher internal (the public contract base and its test classes need it public; NullLauncher is already internal). Note: the fake fix and the contract went in the same commit, so there is no separate red for the fake.
- W5: Border wrappers (Control/Text, TextBlock Raw) for non-Cancel expanded commands (CommandLabelName), legend (WorkflowsLegendName), stage cells (WorkflowStageCell), input cells (WorkflowInputCell). Peer-level asserts in WorkflowsFramesTests.u8_workflows_open and InstanceFramesTests.u8_frame_triage_desk. Missing stage cells are IsVisible=false (out of the automation tree).
- W6: Labels.OpenInstanceName(subject, address) -> "<subject>, open instance <address>"; peer asserted.
- W7: covering sheet + row open stays on SheetTitle (EnterOpenedFrame only when NoOpener); 1000 px test.
- r2-6 _doneReads bumped at Refresh start (WorkflowsFinishedReadTests). r2-7 OpenerWorkflow set before the Enter-again return. r2-8 distinct context figure plus a shape assertion (test-only, no red). r2-9 four ShellInstanceFocusTests windows folded into InstanceFramesTests (project-build, incident-loop, not-found). r2-10 split: WorkflowsReselectTests, ExpandedStateVoiceTests, WorkflowsLegendTests.
- a11y-r2-4 CopyAction restingLabel (Copy status). a11y-r2-6 WorkflowsPage tracks whether focus was last on the page. a11y-r2-7 PayloadFactViewer awaits Task.Yield before the not-running Say. a11y-r2-8 Labels.KeyNotesResting "Key notes", PendingNotesResting "Cancel notes".

## Gate (head 9170b4f96)
- dotnet build -warnaserror whole solution: 0 warnings, 0 errors; dotnet format --verify-no-changes exit 0
- Ui.Tests unit 1558/0 failed; integration 576 (566 passed, 10 skipped, 0 failed)
- Rendering.Tests 21/21; Architecture.Tests 117/117
- check-deps ok; sync-rules --check up to date
- Release --no-build Ui integration wall: 60.6 s and 62.3 s (debug run was 97 s)
