using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>Ends the sessions with no heartbeat for <see cref="LaunchRules.LostAfter"/> as 'session.ended' with reason lost (API spec §8.1).</summary>
/// <param name="liveness">The heartbeats.</param>
/// <param name="session">The session events.</param>
/// <param name="clock">The time.</param>
/// <param name="files">The per-launch files.</param>
/// <param name="views">The 'launches' view: which launches have a live session.</param>
/// <param name="status">How fresh the views are.</param>
internal sealed class LostSessions(LaunchLiveness liveness, LaunchSessionEvents session, TimeProvider clock, ILaunchFiles files, ILaunchViews views, IProjectorStatus status) : ILostSessions
{
    public async ValueTask<int> Sweep(CancellationToken cancellationToken)
    {
        var ended = 0;
        foreach (var launch in liveness.SilentAfterSweep(clock.GetUtcNow(), LaunchRules.LostAfter, LaunchRules.SweepGap))
        {
            if ((await session.End(launch, SessionEndReason.Lost, exitCode: null, cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                ended++;
            }
        }

        return ended;
    }

    public async ValueTask<int?> SweepFiles(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        var freshness = status.Current.AsOf;
        if (freshness.Rebuilding || freshness.Stalled || freshness.Cursor < freshness.LedgerHead)
        {
            return null;
        }

        var live = new HashSet<LaunchId>();
        foreach (var launch in files.Launches())
        {
            var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { Live: true })
            {
                _ = live.Add(launch);
            }
        }

        return files.SweepStale(clock.GetUtcNow(), olderThan, live.Contains);
    }
}
