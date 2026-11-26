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
/// <param name="liveness">Tells a live waiter from a record a killed one left behind.</param>
public sealed class MetaSweep(InboxSync sync, LaneRefresher refresher, ObligationReader reader, LaneContextReader context, ILaneFiles files, IProcessLiveness liveness)
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
        var waiting = await RecycleWaiterMayRun(config.MetaLane, cancellationToken).ConfigureAwait(false);
        return await reader.Open(config.MetaLane, now, cancellationToken, measured.IsSuccess ? measured.Value : null, waiting).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a live recycle waiter is recorded for the lane, read FAIL-CLOSED: a liveness read that fails counts as NOT running. The
    /// stop-check lets a stop through only for a waiter it saw running (#4311); a waiter it can't confirm must not let the meta stop with
    /// obligations open. The waiter clears its record in a finally, which a SIGKILL, an OOM kill or a reboot never runs: a dead pid does
    /// not count, as in RecycleWaiter's own check.
    /// </summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True only while a recorded waiter's pid is confirmed running.</returns>
    public async ValueTask<bool> RecycleWaiterRunning(string lane, CancellationToken cancellationToken) =>
        await ReadWaiter(lane, failOpen: false, cancellationToken).ConfigureAwait(false);

    // FAIL-OPEN read for the obligation reader: a liveness read that fails counts as running, so a figure that can't be read
    // never adds an obligation.
    private async ValueTask<bool> RecycleWaiterMayRun(string lane, CancellationToken cancellationToken) =>
        await ReadWaiter(lane, failOpen: true, cancellationToken).ConfigureAwait(false);

    private async ValueTask<bool> ReadWaiter(string lane, bool failOpen, CancellationToken cancellationToken)
    {
        var waiter = await files.Waiter(lane, cancellationToken).ConfigureAwait(false);
        if (!waiter.IsSuccess || waiter.Value is not { } recorded)
        {
            return false;
        }

        var running = await liveness.IsRunning(recorded.Pid, recorded.StartedAt, cancellationToken).ConfigureAwait(false);
        return running.IsError ? failOpen : running.Value;
    }
}
