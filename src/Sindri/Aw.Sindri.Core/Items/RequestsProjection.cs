using Aw.Kernel;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// Folds 'request.filed', 'decision.recorded' and the closing 'item.state-changed' into the 'requests' and 'decisions' views (API spec §3.4,
/// P4 plan G3): a filed request is an open request with a pending decision, a recorded decision settles both, and closing an item voids
/// what it still waits on. Every text is scrubbed and escaped.
/// </summary>
/// <remarks>
/// Why this is in Items and not Views: it folds the item-closing 'item.state-changed' (voiding what the item still waits on) and
/// its decisions drive the item's needs-decision state in ItemsProjection, so the two change together; Views holds the row types
/// and the rules they share (DecisionRules), which this projection uses.
/// </remarks>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
/// <param name="scrubber">Scrubs secrets out of the text a row stores.</param>
internal sealed class RequestsProjection(EventFactory events, ITextScrubber scrubber) : IProjection
{
    private const int TextMax = RequestRules.TextMax;
    private const int NoteMax = DecisionRules.NoteMax;
    private const int ClearedMax = 300;

    public IReadOnlyList<string> Views => [ViewNames.Requests, ViewNames.Decisions];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.RequestFiled, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.DecisionRecorded, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ItemStateChanged, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.RequestFiled => await Filed(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.DecisionRecorded => await Recorded(recorded, rows, cancellationToken).ConfigureAwait(false),
            _ => await Closed(recorded, rows, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>The rows a filed request makes: the request, open, and its decision, pending.</summary>
    /// <param name="filed">The payload.</param>
    /// <param name="recorded">The recorded event (its time, actor and sequence).</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <param name="itemVersion">The item's version now (0 without an item).</param>
    /// <returns>The two rows.</returns>
    public static FiledRows Fold(RequestFiled filed, LedgerEvent recorded, ITextScrubber scrubber, long itemVersion)
    {
        ArgumentNullException.ThrowIfNull(filed);
        ArgumentNullException.ThrowIfNull(recorded);
        var text = SafeText.Clean(scrubber, filed.Text, TextMax);
        var (actor, at) = (recorded.Envelope.Actor, recorded.Envelope.OccurredAt);
        var request = new RequestRow(filed.Request, filed.Kind, filed.Item, filed.Repo, text, actor, RequestState.Open, filed.Decision, at, recorded.Sequence);
        var decision = new HumanDecisionRow(filed.Decision, filed.Request, DecisionKind.AcceptRequest, filed.Kind, filed.Item, itemVersion, filed.Repo, text,
            DecisionState.Pending, actor, at, DecidedBy: null, DecidedAt: null, Choice: null, Note: null, Cleared: null, recorded.Sequence);
        return new FiledRows(request, decision);
    }

    /// <summary>The rows after a decision is recorded: the decision decided, the request accepted or declined by the choice.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="decision">The decision so far.</param>
    /// <param name="request">Its request so far.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <param name="scrubber">Scrubs the note.</param>
    /// <returns>The two rows, or SND-VIEW-001 when the choice is neither accept nor decline (the fold can't say what it did to the request).</returns>
    public static Result<DecidedRows> Fold(DecisionRecorded payload, HumanDecisionRow decision, RequestRow request, LedgerEvent recorded, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recorded);
        var state = payload.Choice.Value switch
        {
            "accept" => RequestState.Accepted,
            "decline" => RequestState.Declined,
            _ => (RequestState?)null,
        };
        if (state is not { } settled)
        {
            return SindriErrors.ViewsStalled.With($"decision.recorded chose '{SafeText.Escape(payload.Choice.Value)}' for {decision.Id.Value}, which no request state follows");
        }

        var at = recorded.Envelope.OccurredAt;
        var line = SafeText.Clean(scrubber, TimelineLine.DecisionLine(payload), ClearedMax);
        var note = payload.Note is null ? null : SafeText.Clean(scrubber, payload.Note, NoteMax);
        return new DecidedRows(
            request with { State = settled, Sequence = recorded.Sequence },
            decision with { State = DecisionState.Decided, DecidedBy = SafeText.Clean(scrubber, payload.By, ClearedMax), DecidedAt = at, Choice = payload.Choice, Note = note, Cleared = line, Sequence = recorded.Sequence });
    }

    /// <summary>A pending decision voided because its item was closed.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="recorded">The 'item.state-changed' event.</param>
    /// <returns>The voided decision.</returns>
    public static HumanDecisionRow Void(HumanDecisionRow decision, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(recorded);
        return decision with { State = DecisionState.Voided, DecidedAt = recorded.Envelope.OccurredAt, Cleared = EmptyText.Voided(), Sequence = recorded.Sequence };
    }

    private async ValueTask<Result<Unit>> Filed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var filed = events.Payload<RequestFiled>(recorded);
        if (filed.IsError)
        {
            return filed.Error;
        }

        var version = 0L;
        if (filed.Value.Item is { } item)
        {
            var row = await rows.Get<ItemRow>(ViewNames.Items, item.Value, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            version = row.Value?.Version ?? 0;
        }

        var made = Fold(filed.Value, recorded, scrubber, version);
        var request = await rows.Put(ViewNames.Requests, made.Request.Id.Value, made.Request, cancellationToken).ConfigureAwait(false);
        return request.IsError ? request : await rows.Put(ViewNames.Decisions, made.Decision.Id.Value, made.Decision, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Recorded(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<DecisionRecorded>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var decision = await rows.Require<HumanDecisionRow>(ViewNames.Decisions, payload.Value.Decision.Value, recorded.Envelope.Type, cancellationToken).ConfigureAwait(false);
        if (decision.IsSuccess && decision.Value.State != DecisionState.Pending)
        {
            return Unit.Value;   // already settled (voided by its item closing, or decided): a late record changes nothing
        }

        var request = decision.IsError
            ? decision.Error
            : await rows.Require<RequestRow>(ViewNames.Requests, decision.Value.Request.Value, recorded.Envelope.Type, cancellationToken).ConfigureAwait(false);
        var made = request.IsError ? request.Error : Fold(payload.Value, decision.Value, request.Value, recorded, scrubber);
        if (made.IsError)
        {
            return made.Error;
        }

        var written = await rows.Put(ViewNames.Decisions, made.Value.Decision.Id.Value, made.Value.Decision, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written : await rows.Put(ViewNames.Requests, made.Value.Request.Id.Value, made.Value.Request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Closed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var changed = events.Payload<ItemStateChanged>(recorded);
        if (changed.IsError)
        {
            return changed.Error;
        }

        if (!ItemStates.IsClosed(changed.Value.To))
        {
            return Unit.Value;
        }

        var voided = await VoidDecisions(changed.Value.Item, recorded, rows, cancellationToken).ConfigureAwait(false);
        return voided.IsError ? voided : await VoidRequests(changed.Value.Item, recorded, rows, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> VoidDecisions(ItemId item, LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var decisions = await rows.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.Item, item.Value, cancellationToken).ConfigureAwait(false);
        if (decisions.IsError)
        {
            return decisions.Error;
        }

        foreach (var decision in decisions.Value.Where(static d => d.State == DecisionState.Pending))
        {
            var written = await rows.Put(ViewNames.Decisions, decision.Id.Value, Void(decision, recorded), cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return Unit.Value;
    }

    private static async ValueTask<Result<Unit>> VoidRequests(ItemId item, LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var requests = await rows.Where<RequestRow>(ViewNames.Requests, ViewColumn.Item, item.Value, cancellationToken).ConfigureAwait(false);
        if (requests.IsError)
        {
            return requests.Error;
        }

        foreach (var request in requests.Value.Where(static r => r.State == RequestState.Open))
        {
            var written = await rows.Put(ViewNames.Requests, request.Id.Value, request with { State = RequestState.Voided, Sequence = recorded.Sequence }, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return Unit.Value;
    }
}
