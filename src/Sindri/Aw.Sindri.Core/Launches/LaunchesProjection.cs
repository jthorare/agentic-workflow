using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// Folds the launch events into the 'launches' view (UI spec E-1, launcher L-9 and L-10): 'item.open-requested' makes a row, and the outcome,
/// 'launch.viewer-attached', 'launch.session-reported' and 'session.ended' change it. A launch the engine started without a request (the
/// agent runtime, P8) makes its row at 'launch.started'. An event for a launch with no row is a fold that can't continue (SND-VIEW-001): the
/// ledger holds events the view can't explain. Free text from the launcher (a failure message, the viewer's stderr) is escaped again on read.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class LaunchesProjection(EventFactory events) : IProjection
{
    private const string EngineChoice = "engine";

    public IReadOnlyList<string> Views => [ViewNames.Launches];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ItemOpenRequested, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.LaunchStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.LaunchFailed, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.LaunchViewerAttached, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.LaunchSessionReported, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.SessionEnded, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.ItemOpenRequested => await Requested(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.LaunchStarted => await Started(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.LaunchFailed => await Change<LaunchFailed>(recorded, rows, static f => f.Launch, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.LaunchViewerAttached => await Change<LaunchViewerAttached>(recorded, rows, static a => a.Launch, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.LaunchSessionReported => await Change<LaunchSessionReported>(recorded, rows, static r => r.Launch, Fold, cancellationToken).ConfigureAwait(false),
            _ => await Change<SessionEnded>(recorded, rows, static e => e.Launch, Fold, cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<Result<Unit>> Requested(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var request = events.Payload<ItemOpenRequested>(recorded);
        if (request.IsError)
        {
            return request.Error;
        }

        var key = request.Value.Launch.Value;
        var row = await rows.Get<LaunchRow>(ViewNames.Launches, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        if (row.Value is not null)
        {
            return Unit.Value;   // a replay changes nothing
        }

        var repo = await RepoOf(rows, request.Value.Item, cancellationToken).ConfigureAwait(false);
        return repo.IsError ? repo.Error : await rows.Put(ViewNames.Launches, key, Fold(request.Value, repo.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Started(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var started = events.Payload<LaunchStarted>(recorded);
        if (started.IsError)
        {
            return started.Error;
        }

        var key = started.Value.Launch.Value;
        var row = await rows.Get<LaunchRow>(ViewNames.Launches, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var current = row.Value;
        if (current is null)
        {
            var repo = await RepoOf(rows, started.Value.Item, cancellationToken).ConfigureAwait(false);
            if (repo.IsError)
            {
                return repo.Error;
            }

            current = new LaunchRow(started.Value.Launch, started.Value.Item, repo.Value, started.Value.Mode, started.Value.Target, EngineChoice, LaunchState.Requested,
                recorded.Envelope.OccurredAt, OutcomeAt: null, Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null,
                Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null, Ended: null, recorded.Sequence);
        }

        return await rows.Put(ViewNames.Launches, key, Fold(current, started.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Change<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, LaunchId> launch,
        Func<LaunchRow, TPayload, LedgerEvent, LaunchRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var key = launch(payload.Value).Value;
        var row = await rows.Require<LaunchRow>(ViewNames.Launches, key, recorded.Envelope.Type, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Launches, key, fold(row.Value, payload.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<RepoName?>> RepoOf(IViewRows rows, ItemId item, CancellationToken cancellationToken)
    {
        var found = await rows.Get<ItemRow>(ViewNames.Items, item.Value, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : Results.Success(found.Value?.Repo);
    }

    /// <summary>The row an 'item.open-requested' makes: requested, nothing known of an outcome.</summary>
    /// <param name="request">The payload.</param>
    /// <param name="repo">The item's repo, or null.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(ItemOpenRequested request, RepoName? repo, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recorded);
        return new LaunchRow(request.Launch, request.Item, repo, request.Mode, request.Target, request.ChosenBy, LaunchState.Requested, recorded.Envelope.OccurredAt,
            OutcomeAt: null, Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null,
            CatalogSource: null, ArgvHash: null, Ended: null, recorded.Sequence);
    }

    /// <summary>The row after a 'launch.started'. The first outcome wins (they share one key): a launch that already has one ignores it.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="started">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(LaunchRow row, LaunchStarted started, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.State != LaunchState.Requested
            ? row
            : row with
            {
                State = LaunchState.Started,
                OutcomeAt = recorded.Envelope.OccurredAt,
                SessionId = started.SessionId,
                TabTitle = SafeText.Escape(started.TabTitle),
                Viewer = started.Viewer with { Detail = started.Viewer.Detail is { } detail ? SafeText.Escape(detail) : null },
                Attach = started.Attach,
                CatalogSource = started.CatalogSource,
                ArgvHash = started.ArgvHash,
                Sequence = recorded.Sequence,
            };
    }

    /// <summary>The row after a 'launch.failed'. The first outcome wins: a launch that already has one ignores it.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="failed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(LaunchRow row, LaunchFailed failed, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.State != LaunchState.Requested
            ? row
            : row with
            {
                State = LaunchState.Failed,
                OutcomeAt = recorded.Envelope.OccurredAt,
                Code = failed.Code,
                Message = SafeText.Escape(failed.Message),
                Stage = failed.Stage,
                Sequence = recorded.Sequence,
            };
    }

    /// <summary>The row after a 'launch.viewer-attached': an 'unconfirmed' viewer becomes 'opened', late. Any other viewer outcome stays.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="attached">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(LaunchRow row, LaunchViewerAttached attached, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(attached);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.Viewer is { Outcome: ViewerOutcomeKind.Unconfirmed } viewer
            ? row with { Viewer = viewer with { Outcome = ViewerOutcomeKind.Opened }, Late = true, Sequence = recorded.Sequence }
            : row;
    }

    /// <summary>The row after a 'launch.session-reported': the id is kept (the first one wins).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="reported">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(LaunchRow row, LaunchSessionReported reported, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(reported);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.ReportedSessionId is null ? row with { ReportedSessionId = reported.SessionId, Sequence = recorded.Sequence } : row;
    }

    /// <summary>The row after a 'session.ended': how and when. A second end changes nothing.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="ended">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static LaunchRow Fold(LaunchRow row, SessionEnded ended, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(ended);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.Ended is null ? row with { Ended = new LaunchEnd(ended.Reason, ended.ExitCode, recorded.Envelope.OccurredAt), Sequence = recorded.Sequence } : row;
    }
}
