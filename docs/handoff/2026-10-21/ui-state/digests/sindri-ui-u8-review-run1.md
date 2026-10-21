# Sindri UI U8 review run 1 fixes (PR #274)

Branch feat/sindri-ui-u8, worktree ~/repos/aw-wt/sindri-ui-u8. main merged three times (88806a0, 12eb359, 09006a9), no U8 test reads pixels, so nothing moved to Aw.Sindri.Ui.Rendering.Tests; FrameHost.Frame now goes through FrameFile.Save.

## Per item (red commit then green; "test:" then "fix:" in git log)
- V1 re-read keeps the cancel confirm / children rows: ExpandedState.Update, ChildRow.Update, Children.Update; signatures leave out times and ages.
- V2 focus survives reorder; selected row going selects the next (WorkflowFocusEventArgs, OnlyIfLost in WorkflowsPage).
- V3 OpenerWorkflow kept while stacking; Back focuses the child entry (FrameIntent.FocusChild/FocusChildItem).
- V4 Enter again / click on the open row goes into the frame; first read action is focused on open (FocusInitial); Back maps to InstanceBack (SheetBack when covering).
- V5 Esc during the cancel confirm answers Keep and refocuses CommandAsk.
- V6 FramePhase.Refused shows code and detail with Back.
- V7 read counters on the list and on the finished line; FakeSindriApi.InstancesGates.
- V8 CopyAction in the frame's two Copy buttons. V9 LiveAnnouncer for the key note and the pending note.
- V10 chips/loops/branches in names; one Labels.Arrives wording; dead ArrivesWorkflows gone; anchor text fixed.
- V11 legend (WorkflowsLegend). V12 catalog decides protected and chip for child actions.
- V13 ILauncher port in Platform with FakeLauncher. NOT as asked: the adapter AvaloniaLauncher lives in Aw.Sindri.Ui/Views next to AvaloniaClipboard, because ARCH-UI-001 forbids Avalonia types in a head (Aw.Sindri.Ui.Desktop). Not taken: a SindriPlatform.Launcher member.
- V14 spec wording. V15 refresh tests wait on FakeSindriApi.WorkflowsListReads and scroll first. V16 ShellStaleTests. V17 real gap fixed (Open all in Work now clears the Work filter) with a unit test. V18 proved red (commit message). V19 done.
- Cheap list: test-8 (marks and head from Labels; Mark throws on unknown), test-9 partly (760 and the item id and the absent address; the other typed literals are NOT taken), test-10 (nav count, item stack, Esc trail, Open all moved to unit; project-build frame, tabs, lock button folded), test-11, test-12 (Back focus is asserted in ShellInstanceFocusTests; lock button checked on the Locked row), test-13, corr-5, corr-7, corr-8, corr-10 (fixed without its own test), corr-11, a11y-10, a11y-11, a11y-14, a11y-16, arch-3 (HasView test, docs), arch-4 (InstanceRules), arch-7 (docs).
- P1 title trimmed with tooltip; P2 note cleared on selection change, page change and sheet open; P3 B always shows its note.
- Not taken (as briefed): corr-9, arch-6.

## Gate (head = last commit on origin)
- dotnet build -warnaserror whole solution: 0 warnings, 0 errors; dotnet format --verify-no-changes: exit 0
- Aw.Sindri.Ui.Tests unit 1542/0 failed; integration 573 (563 passed, 10 skipped)
- Aw.Sindri.Ui.Rendering.Tests 21/21; Aw.Architecture.Tests 117/117
- check-deps ok; sync-rules --check up to date; check-rules ok
- Release --no-build integration wall: Ui.Tests 80.6 s and 87.8 s; Rendering.Tests 9.6 s and 14.0 s
