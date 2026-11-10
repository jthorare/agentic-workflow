using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>
/// The agent runs that are idle past <see cref="LaunchRules.IdleAfter"/> and still not ended (issue #1000): the same sessions <see cref="IdleSessions"/> ends, read for health and Needs you, so a
/// run that makes no progress shows there as soon as it is idle, not only at the Step's timeout. A Step a person took over is not stalled (the takeover pauses its timer), and neither is a graph node's session a person holds (its launch row carries the run and node, PR 7, and the engine marks the launch held); a session whose
/// instance can't be read is listed, since the sweep leaves it alone and so nothing else ends it.
/// </summary>
/// <param name="liveness">The launches this process tracks.</param>
/// <param name="clock">The time.</param>
/// <param name="files">The per-launch files: the turn-ended and turn-failed files and the tool-running marker.</param>
/// <param name="views">The 'launches' view: the instance a launch belongs to.</param>
/// <param name="instances">Reads the instance, to see whether a person took it over.</param>
internal sealed class StalledAgentRuns(LaunchLiveness liveness, TimeProvider clock, ILaunchFiles files, ILaunchViews views, IInstanceReader instances) : IStalledAgentRuns
{
    public async ValueTask<IReadOnlyList<StalledAgentRun>> Find(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var found = new List<StalledAgentRun>();
        foreach (var launch in liveness.Active())
        {
            if (files.ToolRunning(launch) || (files.TurnEndedAt(launch) ?? files.TurnFailedAt(launch)) is not { } turn || !LaunchRules.IdleTurn(turn, now))   // an API-error stop (turn-failed) is idle too
            {
                continue;
            }

            if (await Stalled(launch, turn, cancellationToken).ConfigureAwait(false) is { } stalled)
            {
                found.Add(stalled);
            }
        }

        return found;
    }

    // The launch as a stalled run: a Step of an instance a person has not taken over (or that can't be read), or a graph node's session no person holds. Null when it is neither.
    private async ValueTask<StalledAgentRun?> Stalled(LaunchId launch, DateTimeOffset turn, CancellationToken cancellationToken)
    {
        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError || row.Value is not { Live: true } known)
        {
            return null;
        }

        if (known.Step is { Run: { } run, Node: { } node })
        {
            return files.TakenOver(launch) ? null : new StalledAgentRun(launch, known.Item, Instance: null, turn) { Run = run, Node = node };
        }

        if (known.Step.Instance is not { } address)
        {
            return null;
        }

        var instance = await instances.Get(address, cancellationToken).ConfigureAwait(false);
        return instance.IsError || instance.Value.TakenOverBy is null ? new StalledAgentRun(launch, known.Item, address, turn) : null;
    }
}
