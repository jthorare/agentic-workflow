using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Opens the map's event recorder under the lease the control plane holds: one dependency for the jobs instead of three.</summary>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
internal sealed class MapEventSource(HeldLease held, ILedgerStore ledger, EventFactory events)
{
    /// <summary>The recorder, or SND-API-001 when the control plane doesn't hold the lease.</summary>
    internal Result<ContextMapEvents> Open() =>
        held.Current is { } grant
            ? new ContextMapEvents(ledger, events, grant)
            : SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
}
