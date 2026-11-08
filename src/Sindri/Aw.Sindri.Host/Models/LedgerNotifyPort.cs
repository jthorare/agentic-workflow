using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Models;

/// <summary>Posts a 'notify' node's note as a 'workflow.notify' event, which the needs-you view shows until the run ends. The key is the run and node, so posting the same note again adds nothing.</summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller, when there is one.</param>
internal sealed class LedgerNotifyPort(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers) : INotifyPort
{
    public async ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var by = callers.Current?.Actor ?? "service:workflow";
        var entry = events.Entry(EventTypes.WorkflowNotify, new WorkflowNotifyPayload(runId, node, title, body, by), $"{EventTypes.WorkflowNotify}:{runId.Value}:{node.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
