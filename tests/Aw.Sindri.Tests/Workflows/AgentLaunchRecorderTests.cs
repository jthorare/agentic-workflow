using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>An engine launch's outcome as the ledger holds it (engine P8 D12, sandbox spec §6, §8): the sandbox summary on 'launch.started', the reason and source on 'launch.failed'.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchRecorderTests
{
    private static readonly SandboxSummary _sandbox = new(
        SandboxMechanism.PassThrough, "plan-hash", ["/work/app/.claude/worktrees/w"], ["github"], NetworkMode.Enforce, InnerState.Off,
        new SandboxLimitsSummary("4G", "200%", 512), new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy);

    private readonly SliceHarness _slice = new();

    private static LaunchRequest Request() => new(
        A.FirstLaunchId, A.FirstItemId, AgentId.From("claude-code"), LaunchMode.Open, Attended: false, "/work/app", Instance: null, "/work/app/.claude/worktrees/w", Step: null, Run: null);

    private async Task<AgentLaunchRecorder> Recorder()
    {
        _ = await _slice.HoldLease();
        return new AgentLaunchRecorder(_slice.Store, _slice.Held, _slice.Events, new LaunchLiveness(), _slice.Clock);
    }

    [Fact]
    public async Task A_started_launch_records_its_sandbox_summary()
    {
        var recorder = await Recorder();
        var started = new StartedLaunch(new HostRef(HostKind.Tmux, "sindri-x"), ViewerOutcome.Skipped, ["tmux", "attach"], CatalogSource.Builtin, "argv-hash") { Sandbox = _sandbox };

        await recorder.Started(Request(), started, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.LaunchStarted);
        Assert.Equal(_sandbox, _slice.Events.Payload<LaunchStarted>(recorded).Value.Sandbox);
    }

    [Fact]
    public async Task A_started_launch_without_a_sandbox_records_none()
    {
        var recorder = await Recorder();
        var started = new StartedLaunch(new HostRef(HostKind.Tmux, "sindri-x"), ViewerOutcome.Skipped, ["tmux", "attach"], CatalogSource.Builtin, "argv-hash");

        await recorder.Started(Request(), started, TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.LaunchStarted);
        Assert.Null(_slice.Events.Payload<LaunchStarted>(recorded).Value.Sandbox);
    }

    [Fact]
    public async Task A_refused_launch_records_the_reason_and_the_source()
    {
        var recorder = await Recorder();
        var error = SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.Expired, source: ModelLoginSource.SeparateLogin);

        await recorder.Failed(Request(), new FailedLaunch(error, LaunchStage.Host), TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.LaunchFailed);
        var failed = _slice.Events.Payload<LaunchFailed>(recorded).Value;
        Assert.Equal(("SND-LAUNCH-017", "expired", "separate-login"), (failed.Code, failed.Reason, failed.Source));
    }
}
