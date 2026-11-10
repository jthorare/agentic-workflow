using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>Records a changed takeover or handback of a 'run_agent' node (#2497): one event about the run's item, by the caller's actor, so the item's timeline says who and when.</summary>
/// <param name="runs">The runs (the item the run works for).</param>
/// <param name="callers">The caller.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class AgentNodeLedger(IWorkflowRunStore runs, ICallerContextAccessor callers, ILedgerStore ledger, HeldLease held, EventFactory events)
{
    /// <summary>The lease to append under, checked before the workflow is touched so a refusal leaves the node as it was and a retry works.</summary>
    /// <returns>The grant; SindriErrors.NotRunning when the control plane holds no lease.</returns>
    internal Result<LeaseGrant> Lease() =>
        held.Current is { } grant ? grant : SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");

    /// <summary>Appends 'workflow.node.taken-over' or 'workflow.node.handed-back' for a change that happened.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="type">The event type.</param>
    /// <param name="ask">The run, the node and the item's position.</param>
    /// <param name="payload">Builds the payload from the actor.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>Done, or why the ledger could not record it (the workflow already did the change).</returns>
    internal async ValueTask<Result<Unit>> Append<TPayload>(LeaseGrant grant, string type, AgentNodeAsk ask, Func<string, TPayload> payload, CancellationToken cancellationToken)
    {
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("recording a takeover or a handback needs a caller");
        }

        var run = await runs.Get(ask.Run, cancellationToken).ConfigureAwait(false);
        var item = run.IsError ? null : run.Value.ItemId?.Value;

        // why: the key carries the command, so a node taken over, handed back and taken over again records each change once
        var key = string.Create(CultureInfo.InvariantCulture, $"{type}:{ask.Run.Value}:{ask.Node.Value}:{ask.ItemIndex}:{caller.CommandId.Value}");
        var appended = await ledger.Append(events.Entry(type, payload(caller.Actor), key, item), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? SindriErrors.ServiceFailed.With($"the workflow took the change but the ledger could not record it: {appended.Error.Message}")
            : Unit.Value;
    }
}
