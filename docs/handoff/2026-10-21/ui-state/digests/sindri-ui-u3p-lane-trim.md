# sindri-ui U3' lane trim (2026-10-16): no commit; target not reachable by test-only levers

Head unchanged: c3ebd01b2 (nothing pushed). Worktree ~/repos/aw-wt/sindri-ui-u3-r1 is clean.
Box load average was 7-14 during every run, so numbers are noisy (+-10 s).

Release --no-build Lane=integration wall, 511 tests: 93 s, 110 s (baseline, c3ebd01b2).

## Levers tried
1. Serial collection reduced to FocusRingTests + PrimaryRowFocusTests (dropped 8 classes): 104 s, 110 s. No gain (the single UI
   thread is the bottleneck either way, so serial vs parallel classes costs nothing). Reverted.
2. Real-time waits: Task.Delay loops are awaited (they yield the UI thread, other classes run meanwhile) and in the sampled
   classes the polling loops run 0 iterations (condition already true). Nothing to gain. ShellWait swap measured the same.
3. Tray contract tests: the ~2 s per trivial test (It_shows_nothing_before_the_first_view) is queueing on the one UI thread,
   not work. Icons are cached per state/count. Nothing to share.
4. Wrong lane: every Integration test in the new files builds Avalonia controls or windows; the rest (Desktop/File*, Socket*)
   are on main already. Nothing to move.

## Where the time is
Serial run (--max-threads 1): wall 91 s, per-test sum 72 s, so the UI thread is the bottleneck; parallelism gains nothing.
Median test 167 ms. Per-test fixed cost is a fresh ShellWindow: ctor 60-180 ms, then the first layout with data 60-250 ms
(UpdateLayout), then ~40 ms for the render job. ~250 tests build one (Connected in FrameHost and in each class). 115 new tests
x ~150-250 ms = the 15-25 s #224 adds.

## Experiments (not applied)
- TestAppBuilder UseHeadlessDrawing=true: lane 70-75 s (-15 to -20 s), but 10 tests fail (5 AvaloniaTrayTests mark pixels,
  5 FocusRingTests, SheetTabWalkTests ring) and every FrameHost.Frame()/CaptureRenderedFrame evidence PNG (u3-*.png) would
  be skipped. Would need those ~11 tests in a second assembly with drawing on. Owner/meta decision.
- Removing WithInterFont: 363 tests fail (fonts needed). Not viable.

## Options that would reach 78 s
A. Split the pixel tests (AvaloniaTrayTests mark tests, FocusRingTests, SheetTabWalkTests, evidence Frame() callers) into a
   second test project with drawing on; main project uses headless drawing. About -15 to -20 s. Changes where evidence frames come from.
B. Share one ShellWindow per class (IClassFixture) for read-only draw tests (NeedsYouHome, NeedsYouFrames, DecisionsFrames,
   WorkflowsStripFrames, QueueFrames). Large edit, isolation risk.
C. Make the shell cheaper to build/lay out (src change, not a test trim): all pages are built eagerly in ShellWindow.axaml.
D. Raise the lane budget for this project.
