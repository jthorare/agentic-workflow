using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Models;

// why: registered with the rest of the workflow piece; until then only tests build it.
#pragma warning disable MA0182
/// <summary>Posts a 'notify' node's note as a 'workflow.notify' event, which the needs-you view shows until the run ends.</summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller, when there is one.</param>
internal sealed class LedgerNotifyPort(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers) : INotifyPort
{
    public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
    {
        _ = (ledger, held, events, callers);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
