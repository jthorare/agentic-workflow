using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>
/// Ends as idle the sessions whose Stop hook says the turn ended <see cref="LaunchRules.IdleAfter"/> ago: the agent has nothing more to do and never reported, and its wrap still
/// beats, so the silence sweep never sees it. A prompt or a tool call deletes the hook's file, so a session that works again starts the clock over. A Step a person took over (the
/// takeover pauses its timer), or one whose instance can't be read right now, is left alone.
/// </summary>
/// <param name="liveness">The launches this process tracks.</param>
/// <param name="session">The session events.</param>
/// <param name="clock">The time.</param>
/// <param name="files">The per-launch files: the turn-ended file.</param>
/// <param name="views">The 'launches' view: the instance a launch belongs to.</param>
/// <param name="hosted">The sessions a host keeps without a wrap.</param>
/// <param name="instances">Reads the instance, to see whether a person took it over.</param>
internal sealed class IdleSessions(LaunchLiveness liveness, LaunchSessionEvents session, TimeProvider clock, ILaunchFiles files, ILaunchViews views, IHostedSessions hosted, IInstanceReader instances) : IIdleSessions
{
    public async ValueTask<int> Sweep(CancellationToken cancellationToken)
    {
        var ended = 0;
        var now = clock.GetUtcNow();
        foreach (var launch in liveness.Active())
        {
            if (files.TurnEndedAt(launch) is { } turn && LaunchRules.IdleTurn(turn, now) && !await TakenOver(launch, cancellationToken).ConfigureAwait(false)
                && (await session.End(launch, SessionEndReason.Idle, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                hosted.Forget(launch);
                ended++;
            }
        }

        return ended;
    }

    // Whether a person holds the Step's instance, or it can't be read now: either way the session is left alone this sweep. A launch with no instance (attended) has no turn hooks.
    private async ValueTask<bool> TakenOver(LaunchId launch, CancellationToken cancellationToken)
    {
        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return true;
        }

        if (row.Value?.Step.Instance is not { } address)
        {
            return false;
        }

        var instance = await instances.Get(address, cancellationToken).ConfigureAwait(false);
        return instance.IsError || instance.Value.TakenOverBy is not null;
    }
}
