using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Memory;

/// <summary>
/// Records 'memory.facts.forgotten' (#2460) when facts are deleted, so the ledger says who forgot them. Like the connection events it is skipped when this process holds
/// no lease, and a failed append is ignored: the facts are already gone.
/// </summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry; its actor is the caller's.</param>
/// <param name="clock">The time.</param>
public sealed class MemoryEvents(HeldLease held, ILedgerStore ledger, EventFactory events, TimeProvider clock)
{
    /// <summary>Appends 'memory.facts.forgotten'.</summary>
    /// <param name="memory">The memory the facts were deleted from.</param>
    /// <param name="selector">How they were chosen: 'fact', 'tag' or 'expired'.</param>
    /// <param name="count">How many were deleted.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask Forgotten(MemoryName memory, string selector, int count, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return;
        }

        var at = clock.GetUtcNow();
        var entry = events.Entry(EventTypes.MemoryFactsForgotten, new MemoryFactsForgotten(memory.Value, selector, count, at), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.MemoryFactsForgotten}:{memory.Value}:{selector}:{at.UtcTicks}"));
        _ = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
    }
}
