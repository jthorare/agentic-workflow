using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The launcher service (UI spec E-1, E-6; API P10a U9): which requests it acts on, when it claims, the takeover wait, and the one outcome.</summary>
[Lane(TestLane.Unit)]
public sealed class LauncherServiceTests
{
    private static readonly LaunchId _a = LaunchId.From("LCH-" + new string('0', 31) + "1");
    private static readonly LaunchId _b = LaunchId.From("LCH-" + new string('0', 31) + "2");
    private static readonly LaunchId _c = LaunchId.From("LCH-" + new string('0', 31) + "3");
    private static readonly TimeSpan _beyondDeadline = TimeSpan.FromSeconds(31);

    private readonly LauncherRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<PendingLaunch> Filed(LaunchId launch, LaunchMode mode = LaunchMode.Open)
    {
        await _rig.Request(launch, mode);
        return Assert.Single(await _rig.Tick(), p => p.Request.Launch == launch);
    }

    private async Task Boot()
    {
        await _rig.Boot();
        await _rig.Item();
        _ = await _rig.Tick();   // the start-up sweep, over a ledger with nothing in flight
    }

    [Fact]
    public async Task Acts_only_on_requests_under_30s_with_no_outcome()
    {
        await Boot();
        using var caller = _rig.Caller();
        await _rig.Request(_a);   // never ticked, so it is 31 s old when the service first sees it
        _rig.Slice.Clock.Advance(_beyondDeadline);
        await _rig.Request(_b);
        await _rig.Request(_c);
        await _rig.Append(EventTypes.LaunchFailed, new LaunchFailed(_c, A.FirstItemId, "SND-LAUNCH-003", "gone"), "launch.outcome:" + _c.Value, A.FirstItemId);

        var due = await _rig.Tick();

        Expect.Equal(_b, Assert.Single(due).Request.Launch);   // the fresh one only
        Expect.Equal(SindriErrors.LaunchInterrupted.Code, Assert.Single(_rig.Failures, f => f.Launch == _a).Code);   // the stale one is swept
        Assert.DoesNotContain(_rig.Failures, f => f.Launch == _b);
        Assert.Empty(_rig.Launcher.Requests);
        Assert.True(await _rig.Launch(due[0]));
        Expect.Equal(_b, Assert.Single(_rig.Launcher.Requests).Launch);   // not the one that has an outcome, not the stale one
    }

    [Fact]
    public async Task Claims_only_with_10s_left()
    {
        await Boot();
        using var caller = _rig.Caller();
        var pending = await Filed(_a);
        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(21));   // 9 s of the deadline remain

        var launched = await _rig.Launch(pending);

        Assert.False(launched);
        Assert.Empty(_rig.Launcher.Requests);
        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(10));
        _ = await _rig.Tick();
        Expect.Equal(SindriErrors.LaunchInterrupted.Code, Assert.Single(_rig.Failures).Code);   // the sweep fails it instead
    }

    [Fact]
    public async Task Expiry_racing_a_start_leaves_one_outcome_started()
    {
        _rig.Timings = _rig.Timings with { LaunchLimit = TimeSpan.FromMinutes(5) };   // so the limit does not fire before the expiry does
        await Boot();
        using var caller = _rig.Caller();
        var pending = await Filed(_a);
        var gate = new TaskCompletionSource();
        _rig.Launcher.Hold = gate.Task;
        var running = _rig.Launch(pending);
        await LauncherRig.Until(() => _rig.Launcher.Requests.Count == 1);

        _rig.Slice.Clock.Advance(_beyondDeadline);
        _ = await _rig.Tick();   // the expiry runs while Launch is still running: the request is claimed, so it skips it
        gate.SetResult();
        var launched = await running;

        Assert.True(launched);
        Assert.Empty(_rig.Failures);
        _ = Assert.Single(_rig.Recorded(EventTypes.LaunchStarted));
    }

    [Fact]
    public async Task Crash_lost_claim_is_swept_as_007()
    {
        await _rig.Boot();
        await _rig.Item();
        using var caller = _rig.Caller();
        await _rig.Request(_a);   // claimed by a process that died before it recorded an outcome

        var due = await _rig.Tick();   // the new process's first tick

        Assert.Empty(due);
        Expect.Equal(SindriErrors.LaunchInterrupted.Code, Assert.Single(_rig.Failures).Code);
        Assert.Empty(_rig.Launcher.Requests);
    }

    [Fact]
    public async Task Never_retries()
    {
        await Boot();
        using var caller = _rig.Caller();
        var pending = await Filed(_a);
        _rig.Launcher.FailNextWith(SindriErrors.LaunchAgentMissing.With("gone"));

        Assert.False(await _rig.Launch(pending));
        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Empty(await _rig.Tick());
        Assert.Empty(await _rig.Tick());
        _rig.Restart();
        Assert.Empty(await _rig.Tick());   // nor after a restart: the failure is the outcome

        _ = Assert.Single(_rig.Launcher.Requests);
        Expect.Equal(SindriErrors.LaunchAgentMissing.Code, Assert.Single(_rig.Failures).Code);
    }

    [Fact]
    public async Task Waits_for_open_request_result_before_launching_on_an_active_instance()
    {
        await Boot();
        using var caller = _rig.Caller();
        await _rig.Instance(LauncherRig.InstanceAddress);
        var pending = await Filed(_a);
        var gate = new TaskCompletionSource();
        _rig.Runtime.Hold = gate.Task;

        var running = _rig.Launch(pending);
        await LauncherRig.Until(() => _rig.Runtime.Requests.Count == 1);

        Assert.Empty(_rig.Launcher.Requests);   // the takeover has not answered
        gate.SetResult();
        Assert.True(await running);
        var request = _rig.Runtime.Requests[0];
        Expect.Equal((LauncherRig.InstanceAddress, _a), (request.Address.Value, request.Launch));
        _ = Assert.Single(_rig.Launcher.Requests);
    }

    [Fact]
    public async Task Refused_takeover_fails_with_the_engine_code()
    {
        await Boot();
        using var caller = _rig.Caller();
        await _rig.Instance(LauncherRig.InstanceAddress);
        var pending = await Filed(_a);
        _rig.Runtime.Refuse = WorkflowErrors.InstanceNotFound.With("the instance is gone");

        Assert.False(await _rig.Launch(pending));

        Expect.Equal(WorkflowErrors.InstanceNotFound.Code, Assert.Single(_rig.Failures).Code);
        Assert.Empty(_rig.Launcher.Requests);
    }

    [Fact]
    public async Task Interleaved_StartInstance_and_OpenItem_never_launch_beside_running_automation()
    {
        await Boot();
        using var caller = _rig.Caller();
        var guarded = new GuardedLauncher(_rig.Launcher, Safe);
        _rig.Port = guarded;
        var pending = await Filed(_a);
        _rig.Status.Then(step: null);   // the wait for the request's own sequence
        _rig.Status.Then(() => _rig.Instance(LauncherRig.InstanceAddress));   // an instance starts before the launcher's second look, as StartInstance would have

        Assert.True(await _rig.Launch(pending));

        Expect.Equal(0, guarded.Violations);
        Expect.Equal(LauncherRig.InstanceAddress, Assert.Single(_rig.Runtime.Requests).Address.Value);   // it was taken over before anything opened
        _ = Assert.Single(_rig.Launcher.Requests);
    }

    private bool Safe() => !_rig.Recorded(EventTypes.WorkflowStarted).Any() || _rig.Runtime.IsTakenOver(LauncherRig.InstanceAddress);

    [Fact]
    public async Task Late_attach_poll_appends_viewer_attached_once_after_restart()
    {
        await Boot();
        using var caller = _rig.Caller();
        _rig.Launcher.Viewer = ViewerOutcome.Unconfirmed;
        var pending = await Filed(_a);
        Assert.True(await _rig.Launch(pending));
        var session = new HostRef(HostKind.Tmux, "snd-" + _a.Value);
        _ = await _rig.Host.Start(new HostSession(session.Name, "/work/demo", ["claude"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(1), Token);
        _rig.Restart();   // the control plane restarts while the viewer is unconfirmed

        _ = await _rig.Tick();
        Assert.Empty(_rig.Recorded(EventTypes.LaunchViewerAttached));   // no client yet
        _rig.Host.AttachClient(session);
        _ = await _rig.Tick();
        _ = await _rig.Tick();

        var attached = Assert.Single(_rig.Recorded(EventTypes.LaunchViewerAttached));
        Expect.Equal(_a, _rig.Payload<LaunchViewerAttached>(attached).Launch);
    }

    [Fact]
    public async Task A_launch_over_its_limit_is_009()
    {
        await Boot();
        using var caller = _rig.Caller();
        var pending = await Filed(_a);
        _rig.Launcher.Hold = new TaskCompletionSource().Task;   // never answers
        var running = _rig.Launch(pending);
        await LauncherRig.Until(() => _rig.Launcher.Requests.Count == 1);

        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(11));

        Assert.False(await running);
        var failure = Assert.Single(_rig.Failures);
        Expect.Equal((SindriErrors.LaunchTimedOut.Code, LaunchStage.Backstop), (failure.Code, failure.Stage));   // a window may have opened: the copy needs to tell this from a host-stage -009
        Assert.Contains(_a, _rig.Launcher.Stops);   // a launch that finished late must leave no session
    }

    [Fact]
    public async Task Attach_opens_the_viewer_on_the_live_session_and_starts_no_process()
    {
        await Boot();
        using var caller = _rig.Caller();
        Assert.True(await _rig.Launch(await Filed(_a)));
        var attach = await Filed(_b, LaunchMode.Attach);

        Assert.True(await _rig.Launch(attach));

        _ = Assert.Single(_rig.Launcher.Requests);   // only the first launch started a process
        var viewing = Assert.Single(_rig.Viewer.Requests);
        Expect.Equal("snd-" + _a.Value, viewing.Host.Name);
        var started = _rig.Recorded(EventTypes.LaunchStarted).Select(_rig.Payload<LaunchStarted>).Single(s => s.Launch == _b);
        Expect.Equal(LaunchMode.Attach, started.Mode);
    }

    [Fact]
    public async Task Attach_with_no_live_session_is_the_launchers_006()
    {
        await Boot();
        using var caller = _rig.Caller();
        _rig.Launcher.FailNextWith(SindriErrors.LaunchNoSession.With("no session"));

        Assert.False(await _rig.Launch(await Filed(_a, LaunchMode.Attach)));

        Expect.Equal(SindriErrors.LaunchNoSession.Code, Assert.Single(_rig.Failures).Code);
        Assert.Empty(_rig.Viewer.Requests);
        Assert.Empty(_rig.Launcher.Requests);   // the answer is known from the view: the launcher is not asked, so it can't hang the request
    }

    [Fact]
    public async Task Attach_is_held_to_the_claim_reserve_like_a_start()
    {
        await Boot();
        using var caller = _rig.Caller();
        Assert.True(await _rig.Launch(await Filed(_a)));
        var attach = await Filed(_b, LaunchMode.Attach);
        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(21));   // 9 s of the deadline remain

        Assert.False(await _rig.Launch(attach));

        Assert.Empty(_rig.Viewer.Requests);   // unclaimed, the sweep fails it instead of a window opening late
    }

    [Fact]
    public async Task An_unconfirmed_attach_viewer_is_watched_and_turns_opened_when_a_client_attaches()
    {
        await Boot();
        using var caller = _rig.Caller();
        Assert.True(await _rig.Launch(await Filed(_a)));
        var session = new HostRef(HostKind.Tmux, "snd-" + _a.Value);
        _ = await _rig.Host.Start(new HostSession(session.Name, "/work/demo", ["claude"], new Dictionary<string, string>(StringComparer.Ordinal)), TimeSpan.FromSeconds(1), Token);
        _rig.Viewer.Script(ViewerOutcome.Unconfirmed);
        Assert.True(await _rig.Launch(await Filed(_b, LaunchMode.Attach)));

        _rig.Host.AttachClient(session);
        _ = await _rig.Tick();

        Expect.Equal(_b, Assert.Single(_rig.Recorded(EventTypes.LaunchViewerAttached).Select(_rig.Payload<LaunchViewerAttached>)).Launch);
    }

    [Fact]
    public async Task A_restart_adopts_the_live_sessions_and_one_whose_wrap_died_ends_as_lost()
    {
        await Boot();
        using var caller = _rig.Caller();
        Assert.True(await _rig.Launch(await Filed(_a)));
        _rig.Restart();   // Sindri restarts: nothing remembers a heartbeat

        _ = await _rig.Tick();

        Expect.Equal(_a, Assert.Single(_rig.Liveness.Active()));
        Assert.Empty(_rig.Liveness.Silent(_rig.Slice.Clock.GetUtcNow() + TimeSpan.FromSeconds(119), LaunchRules.LostAfter));
        Expect.Equal(_a, Assert.Single(_rig.Liveness.Silent(_rig.Slice.Clock.GetUtcNow() + LaunchRules.LostAfter, LaunchRules.LostAfter)));   // no beat ever comes: it ends as lost
    }

    [Fact]
    public async Task A_request_swept_after_a_crash_has_its_session_stopped()
    {
        await _rig.Boot();
        await _rig.Item();
        using var caller = _rig.Caller();
        await _rig.Request(_a);   // the process died after the host started the session and before the outcome was written

        _ = await _rig.Tick();

        Expect.Equal(SindriErrors.LaunchInterrupted.Code, Assert.Single(_rig.Failures).Code);
        Assert.Contains(_a, _rig.Launcher.Stops);   // not left running invisibly under a failed row
    }

    [Fact]
    public async Task A_shutdown_in_the_middle_of_a_launch_stops_the_session_it_started()
    {
        await Boot();
        using var caller = _rig.Caller();
        var pending = await Filed(_a);
        _rig.Launcher.Hold = new TaskCompletionSource().Task;
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var running = _rig.LaunchRaw(pending, shutdown.Token);
        await LauncherRig.Until(() => _rig.Launcher.Requests.Count == 1);

        await shutdown.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Contains(_a, _rig.Launcher.Stops);
        Assert.Empty(_rig.Failures);   // not written at shutdown: the next start's sweep gives the request its 007
    }

    [Fact]
    public async Task Launch_started_names_the_viewers_own_kind_and_label_and_print_has_none()
    {
        await Boot();
        using var caller = _rig.Caller();
        Assert.True(await _rig.Launch(await Filed(_a)));
        _rig.Viewer = new FakeTerminalViewer("print", "Print");
        Assert.True(await _rig.Launch(await Filed(_b)));

        var viewers = _rig.Recorded(EventTypes.LaunchStarted).Select(_rig.Payload<LaunchStarted>).ToDictionary(static s => s.Launch, static s => s.Viewer);

        Expect.Equal(("warp", "Warp"), (viewers[_a].Kind, viewers[_a].Label));
        Expect.Equal(("print", null), (viewers[_b].Kind, viewers[_b].Label));
    }

    [Fact]
    public async Task Launch_time_recheck_writes_launch_failed_010()
    {
        await Boot();
        var claude = AgentId.From("claude-code");
        await _rig.Approve(new AgentSettings(SoftLimit: null, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [claude.Value] = new(DailyBudget: 10m) }));
        _rig.Spend.Spent(claude, 1m);
        using var caller = _rig.Caller();
        var pending = await Filed(_a);   // the open saw 1 of 10 dollars
        _rig.Spend.Spent(claude, 10m);   // and the agent spent the rest in the 30 s before the launch

        var launched = await _rig.Launch(pending);

        Assert.False(launched);
        var failed = Assert.Single(_rig.Failures);
        Expect.Equal((SindriErrors.LaunchBudgetReached.Code, _a), (failed.Code, failed.Launch));
        Assert.Empty(_rig.Launcher.Requests);
    }

    [Fact]
    public async Task A_launch_under_its_budget_goes_ahead()
    {
        await Boot();
        var claude = AgentId.From("claude-code");
        await _rig.Approve(new AgentSettings(SoftLimit: null, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [claude.Value] = new(DailyBudget: 10m) }));
        _rig.Spend.Spent(claude, 8m);   // soft: a warning at the open, no refusal at the launch
        using var caller = _rig.Caller();

        Assert.True(await _rig.Launch(await Filed(_a)));
    }

    [Fact]
    public async Task A_failed_launch_probes_the_targets_again()
    {
        await Boot();
        using var caller = _rig.Caller();
        _rig.Launcher.FailNextWith(SindriErrors.LaunchViewerFailed.With("no window"));

        _ = await _rig.Launch(await Filed(_a));
        _rig.Slice.Clock.Advance(TimeSpan.FromSeconds(1));
        _rig.Launcher.Probed = _rig.Launcher.Probed with { Host = new HostFacts(HostKind.Tmux, Present: false, Version: null, Problem: "not-installed") };
        _ = await _rig.Tick();

        Expect.Equal(2, _rig.Recorded(EventTypes.LaunchProbed).Count);   // the start-up probe, then the one a failed launch asks for
    }

    [Fact]
    public async Task A_sandbox_refusal_records_its_reason_and_source_on_launch_failed()
    {
        await Boot();
        using var caller = _rig.Caller();
        _rig.Launcher.FailNextWith(SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.Expired, source: ModelLoginSource.SeparateLogin));

        _ = await _rig.Launch(await Filed(_a));

        var failed = Assert.Single(_rig.Failures);
        Expect.Equal(("SND-LAUNCH-017", "expired", "separate-login"), (failed.Code, failed.Reason, failed.Source));
    }

    [Fact]
    public async Task A_refusal_that_is_not_a_sandbox_one_records_no_reason()
    {
        await Boot();
        using var caller = _rig.Caller();
        _rig.Launcher.FailNextWith(SindriErrors.LaunchViewerFailed.With("no window"));

        _ = await _rig.Launch(await Filed(_a));

        var failed = Assert.Single(_rig.Failures);
        Expect.Equal((null, null), (failed.Reason, failed.Source));
    }

    [Fact]
    public async Task A_pre_chosen_session_id_is_recorded_on_launch_started()
    {
        await Boot();
        using var caller = _rig.Caller();
        var chosen = Core.Ids.SessionId.From("0198f2a1-0000-7000-8000-000000000001");
        _rig.Launcher.PreChosenSession = chosen;

        Assert.True(await _rig.Launch(await Filed(_a)));

        var started = _rig.Payload<LaunchStarted>(Assert.Single(_rig.Recorded(EventTypes.LaunchStarted)));
        Expect.Equal(chosen, started.SessionId);
    }

    [Fact]
    public async Task Probes_at_start_then_every_ten_minutes_and_only_on_change()
    {
        await Boot();   // the first tick probes
        Expect.Equal(1, _rig.Recorded(EventTypes.LaunchProbed).Count);

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(9));
        _rig.Launcher.Probed = _rig.Launcher.Probed with { Host = new HostFacts(HostKind.Tmux, Present: false, Version: null, Problem: "not-installed") };
        _ = await _rig.Tick();
        Expect.Equal(1, _rig.Recorded(EventTypes.LaunchProbed).Count);   // not yet due

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(2));
        _ = await _rig.Tick();
        Expect.Equal(2, _rig.Recorded(EventTypes.LaunchProbed).Count);

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(10));
        _ = await _rig.Tick();
        Expect.Equal(2, _rig.Recorded(EventTypes.LaunchProbed).Count);   // due, nothing changed
    }

    [Fact]
    public async Task A_started_launch_starts_the_lost_timer_before_wrap_ever_beats()
    {
        await Boot();
        using var caller = _rig.Caller();

        Assert.True(await _rig.Launch(await Filed(_a)));
        Assert.Empty(_rig.Liveness.Silent(_rig.Slice.Clock.GetUtcNow() + TimeSpan.FromSeconds(119), LaunchRules.LostAfter));
        var silent = _rig.Liveness.Silent(_rig.Slice.Clock.GetUtcNow() + LaunchRules.LostAfter, LaunchRules.LostAfter);

        Expect.Equal(_a, Assert.Single(silent));
    }
}
