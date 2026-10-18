using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
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

    /// <summary>Starts a live session's silence clock now: a restart adopts the sessions it finds, whose wraps may be alive or dead.</summary>
    /// <param name="launch">The launch.</param>
    public void Adopt(LaunchId launch) => liveness.Beat(launch, clock.GetUtcNow());

    /// <summary>The viewer part of 'launch.started'.</summary>
    /// <param name="outcome">What the viewer did.</param>
    /// <param name="attended">Whether a person opened it; an unattended launch skips the viewer and records kind 'none'.</param>
    /// <param name="cancellationToken">Cancels the viewer probe.</param>
    /// <returns>The recorded form, with the kind and label the viewer's own probe reports (no label for 'print').</returns>
    public async ValueTask<LaunchViewer> ViewerOf(ViewerOutcome outcome, bool attended, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (!attended)
        {
            return ViewerFrom(new TerminalFacts("none", "none", Present: false, "none", ConfigPath: null, Problem: null, Detail: null), outcome, attended);   // an unattended launch skips the viewer (launcher L-1)
        }

        var facts = await viewer.Probe(cancellationToken).ConfigureAwait(false);   // the viewer that opened the window names itself: 'configured' is a wrapper, not a kind
        return ViewerFrom(facts, outcome, attended);
    }

    /// <summary>The viewer part of 'launch.started' from what the viewer's probe said about itself: its kind and label (none for 'print'), or kind 'none' for an unattended launch.</summary>
    /// <param name="facts">The viewer's own probe.</param>
    /// <param name="outcome">What the viewer did.</param>
    /// <param name="attended">Whether a person opened it.</param>
    /// <returns>The recorded form.</returns>
    public static LaunchViewer ViewerFrom(TerminalFacts facts, ViewerOutcome outcome, bool attended)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(outcome);
        return attended
            ? new LaunchViewer(facts.Kind, string.Equals(facts.Kind, "print", StringComparison.Ordinal) ? null : facts.Label, outcome.Kind, outcome.Code, outcome.Detail)
            : new LaunchViewer("none", Label: null, outcome.Kind, outcome.Code, outcome.Detail);
    }

    /// <summary>Records the session <paramref name="started"/> as the outcome of <paramref name="request"/>.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="request">The launch.</param>
    /// <param name="started">What the launcher started.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The append; 'AlreadyRecorded' when another outcome got there first.</returns>
    public async ValueTask<Result<LedgerAppend>> Started(LeaseGrant grant, LaunchRequest request, StartedLaunch started, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(started);
        var shown = await ViewerOf(started.Viewer, request.Attended, cancellationToken).ConfigureAwait(false);
        return await Recorded(request.Launch, AppendStarted(grant,
            new LaunchStarted(request.Launch, request.Item, request.Mode, request.Agent, SessionIdOf(started), TabTitle(request.Item), shown, started.Attach, started.CatalogSource, started.ArgvHash),
            cancellationToken)).ConfigureAwait(false);
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
    public ValueTask<Result<LedgerAppend>> AppendStarted(LeaseGrant grant, LaunchStarted started, CancellationToken cancellationToken)
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
