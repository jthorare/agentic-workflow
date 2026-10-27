using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>
/// The agent runs that are idle past <see cref="LaunchRules.IdleAfter"/> and still not ended (issue #1000): the same sessions <see cref="IdleSessions"/> ends, read for health and Needs you, so a
/// run that makes no progress shows there as soon as it is idle, not only at the Step's timeout. A Step a person took over is not stalled (the takeover pauses its timer); a session whose
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

            var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
            if (row.IsError || row.Value is not { Live: true, Step.Instance: { } address } known)
            {
                continue;
            }

            var instance = await instances.Get(address, cancellationToken).ConfigureAwait(false);
            if (instance.IsError || instance.Value.TakenOverBy is null)
            {
                found.Add(new StalledAgentRun(launch, known.Item, address, turn));
            }
        }

        return found;
    }
}
