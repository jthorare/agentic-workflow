using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'LaunchAgentStep' does (engine P8 D4, D8, D12): resolves the target, asks the launcher for the unattended session of <see cref="AgentLaunches.RequestFor"/>, records the outcome,
/// and reads the worktree's HEAD as the run's start. The launcher is idempotent per launch id, so a retry or a replay starts no second session.
/// </summary>
/// <param name="launcher">The launcher.</param>
/// <param name="targets">Where the Step runs.</param>
/// <param name="recorder">Records 'launch.started' and 'launch.failed'.</param>
/// <param name="diff">Reads the start commit.</param>
/// <param name="options">How long to wait for a free session slot.</param>
internal sealed class AgentLaunching(ILauncher launcher, AgentTargetResolver targets, AgentLaunchRecorder recorder, IRunDiff diff, AgentRuntimeOptions options)
{
    /// <summary>Launches the run's session.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The worktree and start commit, or the failure.</returns>
    public async Task<AgentLaunched> Launch(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var target = await targets.Resolve(run.Item, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, target.Error.Code, target.Error.Message));   // nothing was asked of the launcher, so there is no outcome to record
        }

        var request = AgentLaunches.RequestFor(run.Run, target.Value);
        var result = await LaunchOrWait(request, cancellationToken).ConfigureAwait(false);
        return result is null
            ? new AgentLaunched(Worktree: null, StartHead: null, Failure: null, Waiting: true)   // the cap is still reached: nothing is recorded, the workflow asks again
            : await Settle(request, result, cancellationToken).ConfigureAwait(false);
    }

    // SND-LAUNCH-020 is a queue condition (sandbox spec §6): ask again every SlotRetry until the slice ends, recording nothing; null says the slice ended.
    private async Task<LaunchResult?> LaunchOrWait(LaunchRequest request, CancellationToken cancellationToken)
    {
        for (var waited = TimeSpan.Zero; ; waited += options.SlotRetry)
        {
            var result = await launcher.Launch(request, cancellationToken).ConfigureAwait(false);
            if (result is not FailedLaunch failed || SandboxCodes.KindOf(failed.Error.Code) != LaunchCodeKind.ResourceWait)
            {
                return result;
            }

            if (waited + options.SlotRetry > options.SlotSlice)
            {
                return null;
            }

            await Task.Delay(options.SlotRetry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AgentLaunched> Settle(LaunchRequest request, LaunchResult result, CancellationToken cancellationToken)
    {
        switch (result)
        {
            case StartedLaunch started:
                await recorder.Started(request, started, cancellationToken).ConfigureAwait(false);
                var head = await diff.Head(request.Worktree!, cancellationToken).ConfigureAwait(false);   // why: RequestFor always names the worktree
                return new AgentLaunched(request.Worktree, head.IsError ? null : head.Value, Failure: null);   // a worktree with no HEAD leaves the diff signal unmeasured
            case FailedLaunch failed:
                await recorder.Failed(request, failed, cancellationToken).ConfigureAwait(false);
                return new AgentLaunched(Worktree: null, StartHead: null, AgentLaunchFailures.Of(failed));
            default:
                throw new InvalidOperationException($"the launcher answered {result.GetType().Name}");
        }
    }
}
