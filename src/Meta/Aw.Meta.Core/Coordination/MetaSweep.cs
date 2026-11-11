using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>What the stop hook does before judging: sync the transcript, refresh the lane map, evaluate what is owed.</summary>
/// <param name="sync">Reads new peer messages.</param>
/// <param name="refresher">Refreshes the lane map.</param>
/// <param name="reader">Evaluates the open obligations.</param>
/// <param name="context">Measures the meta lane's context.</param>
/// <param name="files">Holds the meta lane's recorded recycle waiter.</param>
public sealed class MetaSweep(InboxSync sync, LaneRefresher refresher, ObligationReader reader, LaneContextReader context, ILaneFiles files)
{
    /// <summary>Runs the sweep.</summary>
    /// <param name="transcript">The meta session's transcript.</param>
    /// <param name="config">The team config.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The open obligations.</returns>
    public async ValueTask<Result<IReadOnlyList<Obligation>>> Run(string transcript, TeamConfig config, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        var synced = await sync.Run(transcript, cancellationToken).ConfigureAwait(false);
        if (synced.IsError)
        {
            return synced.Error;
        }

        var refreshed = await refresher.Run(config, now, cancellationToken).ConfigureAwait(false);
        if (refreshed.IsError)
        {
            return refreshed.Error;
        }

        // A figure that can't be read never blocks a stop: an unmeasured lane owes nothing.
        var measured = await context.ReadLane(config.Team, config.MetaLane, config.Context.RecycleAt, cancellationToken).ConfigureAwait(false);
        var waiter = await files.Waiter(config.MetaLane, cancellationToken).ConfigureAwait(false);
        return await reader.Open(config.MetaLane, now, cancellationToken, measured.IsSuccess ? measured.Value : null, waiter.IsSuccess && waiter.Value is not null).ConfigureAwait(false);
    }
}
