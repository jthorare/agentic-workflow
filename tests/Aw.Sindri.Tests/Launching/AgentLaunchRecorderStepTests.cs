using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The agent runtime records its own launch events: they carry the instance, Step state and attempt of the request (API follow-up to P10a, engine P8).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchRecorderStepTests
{
    private readonly SliceHarness _slice = new();

    private async Task<AgentLaunchRecorder> Recorder()
    {
        _ = await _slice.HoldLease();
        return new AgentLaunchRecorder(_slice.Store, _slice.Held, _slice.Events, new LaunchLiveness(), _slice.Clock);
    }

    private static LaunchRequest Request(LaunchId launch) =>
        A.LaunchRequest().WithLaunch(launch).Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent")))
            .OnInstance(InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1"), RunToken.For(2, 3)).Build();

    private static FailedLaunch Full(int running, int cap) =>
        new(SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions, "full", details: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["running"] = running.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cap"] = cap.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }), LaunchStage.Host);

    [Fact]
    public async Task A_launch_that_waits_for_a_slot_records_how_full_the_cap_is_once()
    {
        var recorder = await Recorder();

        await recorder.Waiting(Request(A.FirstLaunchId), Full(3, 3), TestContext.Current.CancellationToken);
        await recorder.Waiting(Request(A.FirstLaunchId), Full(4, 3), TestContext.Current.CancellationToken);

        var waiting = _slice.Events.Payload<LaunchWaiting>(Assert.Single(_slice.Store.Events, e => e.Envelope.Type == EventTypes.LaunchWaiting)).Value;
        Expect.Equal((3, 3, "build", 3), (waiting.Running, waiting.Cap, waiting.State, waiting.Attempt));
        Expect.Equal(A.FirstLaunchId, waiting.Launch);
    }

    [Fact]
    public async Task A_refusal_that_is_not_a_full_cap_records_no_wait()
    {
        var recorder = await Recorder();

        await recorder.Waiting(Request(A.FirstLaunchId), new FailedLaunch(SindriErrors.LaunchTimedOut.With("late"), LaunchStage.Host), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.LaunchWaiting);
    }

    [Fact]
    public async Task The_launch_activity_records_the_wait_before_it_waits()
    {
        var recorder = await Recorder();
        var launcher = new FullThenStarted(Full(3, 3));
        var waiting = new AgentLaunchWaiting(launcher, new AgentRuntimeOptions(TimeSpan.FromSeconds(5)) { SlotRetry = TimeSpan.FromMilliseconds(1), SlotSlice = TimeSpan.FromSeconds(5) }, recorder);

        var result = await waiting.Launch(Request(A.FirstLaunchId), TestContext.Current.CancellationToken);

        _ = Assert.IsType<StartedLaunch>(result);
        _ = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.LaunchWaiting);
    }

    private sealed class FullThenStarted(FailedLaunch full) : ILauncher
    {
        private int _asked;

        public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<LaunchResult>(_asked++ < 2 ? full : new StartedLaunch(new HostRef(HostKind.Tmux, "s"), ViewerOutcome.Skipped, ["tmux"], CatalogSource.Builtin, "sha256:abc"));

        public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Started_and_failed_engine_launches_record_instance_state_and_attempt()
    {
        var recorder = await Recorder();
        var second = LaunchId.From("LCH-" + new string('0', 31) + "2");

        await recorder.Started(Request(A.FirstLaunchId), new StartedLaunch(new HostRef(HostKind.Tmux, "s"), ViewerOutcome.Skipped, ["tmux"], CatalogSource.Builtin, "sha256:abc"), TestContext.Current.CancellationToken);
        await recorder.Failed(Request(second), new FailedLaunch(SindriErrors.LaunchTimedOut.With("late"), LaunchStage.Host), TestContext.Current.CancellationToken);

        var types = _slice.Store.Events.ToList();
        var started = _slice.Events.Payload<LaunchStarted>(types.Single(e => e.Envelope.Type == EventTypes.LaunchStarted)).Value;
        var failed = _slice.Events.Payload<LaunchFailed>(types.Single(e => e.Envelope.Type == EventTypes.LaunchFailed)).Value;
        Expect.Equal(("ITM-00000000000000000000000000000001/spec/1", "build", 3), (started.Instance!.Value.Value, started.State, started.Attempt));
        Expect.Equal(("ITM-00000000000000000000000000000001/spec/1", "build", 3), (failed.Instance!.Value.Value, failed.State, failed.Attempt));
    }
}
