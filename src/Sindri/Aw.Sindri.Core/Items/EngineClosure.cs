using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The one check for "this item ended by its workflow" (API P11, review run 2 I4): an item whose root instance ends closes as a projection
/// only (no 'item.state-changed' row, decision Q6), and what a human close does (void the pending decisions and open requests, clear the
/// decision rows) must follow the item row reaching a closed state. The requests projection folds before the items one, so it asks
/// <see cref="EndsItem"/> with the row as it is; the needs-you projection folds after, so it asks <see cref="IsEnded"/> of the row as written.
/// The void and the clear re-run on every later row of an ended item; that is idempotent by design (they act only on Pending decisions and Open requests).
/// </summary>
internal static class EngineClosure
{
    /// <summary>True when the item row, as the items projection wrote it, is closed with no live root left.</summary>
    /// <param name="row">The item row after the event.</param>
    /// <returns>True when closed.</returns>
    public static bool IsEnded(ItemRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return ItemStates.IsClosed(row.State) && LiveRoot(row) is null;
    }

    /// <summary>The earlier root instance of the item that is still live, if any (the latest may have ended and closed the item): the one condition 'close' refuses on and the item isn't ended by its workflow on.</summary>
    /// <param name="row">The item row.</param>
    /// <returns>The address of a live earlier root, or null when none is.</returns>
    public static string? LiveRoot(ItemRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Engine?.Earlier is { Count: > 0 } earlier ? earlier[0].Address : null;
    }

    /// <summary>True when the item row, once the items projection folds this 'workflow.*' row into it, is closed.</summary>
    /// <param name="row">The item row before the event.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>True when closed.</returns>
    public static bool EndsItem(ItemRow row, string type, WorkflowEventPayload payload, LedgerEvent recorded)
        => IsEnded(ItemsProjection.Fold(row, type, payload, recorded));
}
