#pragma warning disable MA0182, MA0041, CA1822 // red commit: the stub is registered in the commit that implements it
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Models;

/// <summary>Writes the router's 'context.model.called' event to the ledger as 'service:context' (model routing spec §4 "Writing events"); best effort.</summary>
internal sealed class LedgerModelCallLog : IModelCallLog
{
    /// <summary>Creates the log.</summary>
    /// <param name="held">The lease the event is fenced by.</param>
    /// <param name="ledger">Appends the event.</param>
    /// <param name="events">Builds the entry.</param>
    /// <param name="logger">Where a failed append is noted.</param>
    public LedgerModelCallLog(HeldLease held, ILedgerStore ledger, EventFactory events, ILogger<LedgerModelCallLog> logger)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(logger);
    }

    public ValueTask Record(ContextModelCalled called, CancellationToken cancellationToken) => throw new NotSupportedException();
}
