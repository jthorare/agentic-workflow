using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launching;

/// <summary>An engine launch says which instance, Step and attempt it belongs to, in 'launch.started' and in 'launch.failed' (API follow-up to P10a).</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchOutcomesStepTests
{
    private static readonly InstanceAddress _instance = InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1");
    private readonly LauncherRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LaunchOutcomes Outcomes => new(_rig.Slice.Store, _rig.Slice.Events, _rig.Viewer, new LaunchLiveness(), _rig.Slice.Clock);

    private static LaunchRequest Engine() =>
        A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).OnInstance(_instance, RunToken.For(2, 3)).Build();

    private static StartedLaunch Started() =>
        new(new HostRef(HostKind.Tmux, "sindri-launch"), ViewerOutcome.Skipped, ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc");

    [Fact]
    public async Task Engine_launch_records_instance_state_and_attempt()
    {
        await _rig.Boot();
        await _rig.Item();

        var started = await Outcomes.Started(_rig.Grant, Engine(), Started(), Token);
        var failed = await Outcomes.Failed(_rig.Grant, Engine() with { Launch = Second }, SindriErrors.LaunchTimedOut.With("late"), LaunchStage.Host, Token);

        Assert.True(started.IsSuccess && failed.IsSuccess);
        var recordedStart = _rig.Payload<LaunchStarted>(Assert.Single(_rig.Recorded(EventTypes.LaunchStarted)));
        Expect.Equal((_instance, "build", 3), (recordedStart.Instance!.Value, recordedStart.State, recordedStart.Attempt));
        var recordedFail = Assert.Single(_rig.Failures);
        Expect.Equal((_instance, "build", 3), (recordedFail.Instance!.Value, recordedFail.State, recordedFail.Attempt));
    }

    [Fact]
    public async Task Attended_launch_records_no_instance_state_or_attempt()
    {
        await _rig.Boot();
        await _rig.Item();

        _ = await Outcomes.Started(_rig.Grant, A.LaunchRequest().Build(), Started(), Token);

        var recorded = _rig.Payload<LaunchStarted>(Assert.Single(_rig.Recorded(EventTypes.LaunchStarted)));
        Expect.Equal((null, null, null), (recorded.Instance?.Value, recorded.State, recorded.Attempt));
    }

    private static LaunchId Second => LaunchId.From("LCH-" + new string('0', 31) + "2");
}
