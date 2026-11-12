using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Resources;

/// <summary>Records 'resource.purged' (#2941) when a purge deleted something, so the ledger says who purged what.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry; its actor is the caller's.</param>
/// <param name="clock">The time.</param>
#pragma warning disable CS9113 // why: red-check stub (#2941); the fix reads these
public sealed class ResourceEvents(HeldLease held, ILedgerStore ledger, EventFactory events, TimeProvider clock)
{
    /// <summary>Appends 'resource.purged'.</summary>
    /// <param name="payload">What was purged.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public ValueTask Purged(ResourcePurged payload, CancellationToken cancellationToken) => throw new NotSupportedException();
}
