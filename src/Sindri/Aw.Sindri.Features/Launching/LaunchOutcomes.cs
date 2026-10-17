using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Appends what the launcher service decides (UI spec E-1). 'launch.started' and 'launch.failed' share one fact key, 'launch.outcome:&lt;launch&gt;',
/// so the first outcome written wins and the second comes back 'AlreadyRecorded'.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="viewer">The configured viewer, whose kind 'launch.started' records.</param>
/// <param name="liveness">The heartbeats: a started launch starts its clock here, so a wrap that dies before its first heartbeat is still ended as lost (API spec §8.1).</param>
/// <param name="clock">The time the clock starts at.</param>
internal sealed class LaunchOutcomes(ILedgerStore ledger, EventFactory events, ITerminalViewer viewer, LaunchLiveness liveness, TimeProvider clock)
{
    /// <summary>The viewer's tab title for an item (launcher L-10: the same for every viewer).</summary>
    /// <param name="item">The item.</param>
    /// <returns>The title.</returns>
    public static string TabTitle(ItemId item) => "sindri " + item.Value;

    /// <summary>The viewer part of 'launch.started'.</summary>
    /// <param name="outcome">What the viewer did.</param>
    /// <returns>The recorded form.</returns>
    public LaunchViewer ViewerOf(ViewerOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new LaunchViewer(viewer.Kind, Label: null, outcome.Kind, outcome.Code, outcome.Detail);
    }

    /// <summary>Records the session <paramref name="started"/> as the outcome of <paramref name="request"/>.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="request">The launch.</param>
    /// <param name="started">What the launcher started.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public ValueTask<Result<LedgerAppend>> Started(LeaseGrant grant, LaunchRequest request, StartedLaunch started, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(started);
        return Recorded(request.Launch, Started(grant,
            new LaunchStarted(request.Launch, request.Item, request.Mode, request.Agent, SessionIdOf(started), TabTitle(request.Item), ViewerOf(started.Viewer), started.Attach, started.CatalogSource, started.ArgvHash),
            cancellationToken));
    }

    private async ValueTask<Result<LedgerAppend>> Recorded(LaunchId launch, ValueTask<Result<LedgerAppend>> appending)
    {
        var appended = await appending.ConfigureAwait(false);
        if (appended.IsSuccess && !appended.Value.AlreadyRecorded)
        {
            liveness.Beat(launch, clock.GetUtcNow());   // the clock starts now, not at wrap's first heartbeat
        }

        return appended;
    }

    /// <summary>Records the session as started.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="started">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public ValueTask<Result<LedgerAppend>> Started(LeaseGrant grant, LaunchStarted started, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(started);
        return ledger.Append(events.Entry(EventTypes.LaunchStarted, started, OutcomeKey(started.Launch), started.Item.Value), grant, cancellationToken);
    }

    /// <summary>Records the launch as failed.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="failed">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public ValueTask<Result<LedgerAppend>> Failed(LeaseGrant grant, LaunchFailed failed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failed);
        return ledger.Append(events.Entry(EventTypes.LaunchFailed, failed, OutcomeKey(failed.Launch), failed.Item.Value), grant, cancellationToken);
    }

    /// <summary>Records that a client attached to an 'unconfirmed' launch's session.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="watched">The launch.</param>
    /// <param name="at">When the client was seen.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append.</returns>
    public ValueTask<Result<LedgerAppend>> ViewerAttached(LeaseGrant grant, WatchedViewer watched, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(watched);
        return ledger.Append(events.Entry(EventTypes.LaunchViewerAttached, new LaunchViewerAttached(watched.Launch, at), $"{EventTypes.LaunchViewerAttached}:{watched.Launch.Value}", watched.Item.Value), grant, cancellationToken);
    }

    /// <summary>The id the launcher chose before it started the agent (launcher L-9); an agent whose id is reported later has none here.</summary>
    private static SessionId? SessionIdOf(StartedLaunch started) => started.ChosenSession is { } chosen && SessionId.TryFrom(chosen, out var id) ? id : null;

    private static string OutcomeKey(LaunchId launch) => "launch.outcome:" + launch.Value;
}
