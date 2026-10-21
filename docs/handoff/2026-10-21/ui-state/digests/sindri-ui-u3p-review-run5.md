# Sindri UI U3' review run 5 fixes (PR #224, branch fix/u3-r1 -> feat/sindri-ui-u3)

Final head: c3ebd01b2 (pushed to origin/feat/sindri-ui-u3). origin/main had nothing new to merge.

- R1 (r5-0, blocking): DecisionSheetViewModel.Raise() now raises DecideCli, CanCopy, CopyName and DecideLineName.
  Test: tests/Aw.Sindri.Ui.Tests/Views/NeedsYouDecisionLineTests.cs (pending Decide rows; DEC-1 then DEC-2: Copy visible,
  line name starts with the id and follows the second decision). Verified red with the fix reverted. The first red commit
  failed for a setup reason (ready actions have no line), corrected in the fix commit. The item sheet's ItemDecisionRow
  is a rebuilt record and was untouched.
- R2 (r5-1): xUnit v3 collection "Headless windows" (DisableParallelization = true, Views/HeadlessWindows.cs) on the
  focus-moving classes: FocusRingTests, PrimaryRowFocusTests, QueueFocusTests, NeedsYouFocusTests, ChordFocusTests,
  SheetEnterFocusTests, DecisionFocusTests, StripFocusReturnTests, SheetTabWalkTests, LockButtonWalkTests. They run alone
  after the parallel collections. FocusRingTests.AssertRing now calls window.Activate() and asserts the control is focused.
  Tried first: every headless window class in the collection; that hung the run (tray contract tests, then a ShellWindowTests
  theory), so it was narrowed. No before/after wall time for the lane (the box is loaded and the heavy slot queues).
- R3 (r5-2): QueueStyles.axaml comment says 4.42:1.

Gate (final head): both builds -warnaserror 0 warnings; Ui.Tests unit 1387/0 failed; integration run twice 511/0 failed
(97 s and 93 s wall on a loaded box, lane-budgets script not run); Aw.Architecture.Tests 117/0; check-deps ok;
sync-rules --check ok; sindri-ui-fixture.test.sh rc 0; dotnet format --verify-no-changes exit 0.
