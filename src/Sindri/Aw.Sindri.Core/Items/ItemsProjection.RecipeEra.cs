using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Items;

// The end of a recipe-era root instance, folded into its item's state only (#2258, v2 PR 4b).
internal sealed partial class ItemsProjection
{
    /// <summary>
    /// The row after a recipe-era root instance's 'workflow.started' or 'workflow.transition' (#2258, v2 PR 4b): the recipe engine never wrote
    /// 'item.closed', so an instance that ended is what closed its item. The state only: an ended root reads Done (Cancelled when a cancel ended it),
    /// a root started again on a closed item reopens it as New, and an instance still open when the engine went leaves the item as it is (New). A
    /// child's row, a row that ends nothing and the retire pass's note change nothing. When two roots of one item overlapped, the earlier one's end
    /// closes the item too (the engine part that told them apart is gone).
    /// </summary>
    /// <param name="row">The row so far.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row; the same row when the event leaves the item alone.</returns>
    public static ItemRow Fold(ItemRow row, string type, WorkflowEventPayload payload, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(recorded);
        if (payload.Instance.AsSpan().Count('/') != 2)
        {
            return row;   // a child instance's row: its root's rows carry the item
        }

        var closed = row.State is ItemState.Done or ItemState.Cancelled;
        ItemState? state = string.Equals(type, EventTypes.WorkflowStarted, StringComparison.Ordinal)
            ? (closed ? ItemState.New : null)
            : closed || !Ends(payload) ? null
            : string.Equals(payload.Trigger, TriggerNames.Cancel, StringComparison.Ordinal) ? ItemState.Cancelled : ItemState.Done;
        if (state is not { } next)
        {
            return row;
        }

        var at = recorded.Envelope.OccurredAt;
        return row with { State = next, Since = at, WaitingOn = null, UpdatedAt = at, Sequence = recorded.Sequence };
    }

    // A row ended the instance when its delta names an outcome, or, stored without its detail (the scrub's fallback), when its trigger is the cancel.
    // The retire pass's note is not an end: an instance open when the engine went leaves its item open.
    private static bool Ends(WorkflowEventPayload payload) =>
        payload.Detail is { } detail ? detail.Delta.Outcome is not null : string.Equals(payload.Trigger, TriggerNames.Cancel, StringComparison.Ordinal);

    private async ValueTask<Result<Unit>> RecipeEra(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);
        if (payload.IsError || recorded.Item is not { } item)
        {
            return Unit.Value;   // history only: an old row this build can't read, or one about no item, leaves the views alone
        }

        var row = await rows.Get<ItemRow>(ViewNames.Items, item, cancellationToken).ConfigureAwait(false);
        if (row.IsError || row.Value is null)
        {
            return row.IsError ? row.Error : Unit.Value;
        }

        var next = Fold(row.Value, recorded.Envelope.Type, payload.Value, recorded);
        return ReferenceEquals(next, row.Value) ? Unit.Value : await rows.Put(ViewNames.Items, item, next, cancellationToken).ConfigureAwait(false);
    }
}
