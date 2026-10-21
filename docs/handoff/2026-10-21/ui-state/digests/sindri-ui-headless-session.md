# sindri-ui-headless-session handoff (review run 1 worked; branch fix/ui-headless-session, PR #305)

Test infrastructure + one small production change (InstanceListener, SindriApp). Worktree ~/repos/aw-wt/sindri-ui-headless.

## Cause (corrected after review run 1)
Avalonia.Headless 12.1.3 uses PerTest isolation: every Dispatch resets the dispatcher (s_uiThread = null) and re-runs SetupUnsafe, so
"the first thread to read Dispatcher.UIThread owns it" reopens at the start of EVERY test. An early session start (the first fix,
reverted) changes nothing. The only production reader of the static Dispatcher.UIThread from a pool thread is
InstanceListener.Listen (after ConfigureAwait(false)). SindriApp started it with CancellationToken.None and nothing stopped it, so a
listener left by a start-up test could read the static in the next test's reset window, bind the dispatcher to a pool thread, and the
session thread's compositor/render loop then threw "different thread owns it" (DefaultRenderLoop.Add / Compositor ctor).
Audit: every other Dispatcher.UIThread use in src/Sindri is a Post from view/UI-thread code; view-model timers use UiContext (captured
SynchronizationContext), not the static. Tests touch Avalonia only via Dispatch.

## Red test (deterministic)
InstanceListenerThreadTests: inside one Dispatch, start a listener, reset the dispatcher (reflection on the internal
Dispatcher.ResetBeforeUnitTests, as Dispatch does), deliver a message from the test with no await, sleep 300 ms (RealTime), then assert
Dispatcher.UIThread.CheckAccess(). On the old code it fails 3 of 3 runs in under 1 s with the exact production message ("The
calling thread cannot access this object because a different thread owns it"). An async body variant hung 20 s (my own await
continuation posted to the reset dispatcher); the body is synchronous and the outer wait is bounded at 20 s.

## Fix
- InstanceListener captures Dispatcher.UIThread at construction (UI thread) and posts through it.
- SindriApp owns a CancellationTokenSource for the listener, IDisposable (PublicAPI.Unshipped updated), disposed on desktop.Exit;
  StartupTests disposes the app so no listener outlives its test.
- Fixtures and the 8-thread test were reverted (they could not fail).

## Second offender (test side), found by the gate
UiContextStartupTests built SindriApp (an Avalonia Application; Create also reads Dispatcher.UIThread via
AvaloniaSynchronizationContext.InstallIfNeeded) on a bare new Thread outside Dispatch. When it ran in a reset window it took the
dispatcher: AvaloniaClipboardContractTests.A_refused_write_is_false_and_holds_nothing then failed in SetupUnsafe (Compositor ctor ->
DefaultRenderLoop.Add), 3 of 3 lane runs on the merged head, with the exact flake stack. Fixed by running those two bodies inside
Dispatch with the sync context cleared on the session thread; the lane then passed 3 of 3 (503 tests, 66-70 s).

## Follow-up (not done): PerAssembly isolation
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)] would bind one dispatcher once and close the window for all
off-thread reads, and likely cut lane time. Risk: app-level state leaks between tests (Application.Current.Resources, SettingsTheme
changes in StripFoldTests, static caches such as TrayMarks icons). Needs its own audit and lane timing.

## ThemeResourcesTests flake (#303)
ThemeResourcesTests only reads Application.Current inside Dispatch. A pool-thread Dispatcher.UIThread read in a reset window by a
leaked InstanceListener (StartupTests, never stopped) explains a flake in whichever test runs next, so it is covered by this fix.
