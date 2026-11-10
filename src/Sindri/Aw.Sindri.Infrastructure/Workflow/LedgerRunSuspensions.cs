using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Whether a launch's run is suspended, read from the ledger (sandbox spec §7, SB16): never a Temporal query per watchdog beat and never views.db, whose
/// projector may lag. The launch's 'launch.started' row names the instance it ran for; that instance's mirrored 'workflow.*' rows, folded oldest first
/// the way the items projection folds them, say who opened the run that is open now and whether a park, escalation or takeover holds it. The launch is
/// suspended when it is the actor that opened the open run and that run is held. A failed or attended (click) launch, a replaced run and an ended
/// instance are not suspended. A graph 'run_agent' node's launch (it names a run and a node, no instance) is suspended while its 'workflow.node.taken-over' row is the latest of the pair with 'workflow.node.handed-back'.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerRunSuspensions(ILedgerReader ledger, EventFactory events) : IRunSuspensions
{
    private const int Page = 500;

    private static readonly TypeFilter _engineRows = TypeFilter.Parse(
        $"{EventTypes.WorkflowStarted},{EventTypes.WorkflowTransition},{EventTypes.WorkflowRefused},{EventTypes.WorkflowNotified}").Value;

    private static readonly TypeFilter _nodeRows = TypeFilter.Parse($"{EventTypes.WorkflowNodeTakenOver},{EventTypes.WorkflowNodeHandedBack}").Value;

    public async ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken)
    {
        var outcome = await ledger.ByKey($"launch.outcome:{launch.Value}", cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        if (outcome.Value is not { } recorded || !string.Equals(recorded.Envelope.Type, EventTypes.LaunchStarted, StringComparison.Ordinal))
        {
            return false;
        }

        var started = events.Payload<LaunchStarted>(recorded);
        if (started.IsError)
        {
            return started.Error;
        }

        if (started.Value.Instance is { } instance)
        {
            return await Held(launch, instance, cancellationToken).ConfigureAwait(false);
        }

        // why: a graph 'run_agent' node's launch has no instance; a person holding the node pauses its timeout, so the wall pauses too (#2471). An attended (click) launch has neither and is never suspended
        return started.Value is { Run: { } run, Node: { } node }
            ? await NodeHeld(started.Value.Item, run, node, cancellationToken).ConfigureAwait(false)
            : false;
    }

    /// <summary>Whether the 'run_agent' node (item 0: a loop holds no agent node) is taken over now: its 'workflow.node.taken-over' and 'workflow.node.handed-back' rows (#2497), folded oldest first, are about the run's item.</summary>
    private async ValueTask<Result<bool>> NodeHeld(ItemId item, string run, string node, CancellationToken cancellationToken)
    {
        var filter = new LedgerFilter(_nodeRows, item);
        var held = false;
        var cursor = 0L;
        while (true)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var row in scan.Value.Events)
            {
                var change = NodeChange(row, run, node);
                if (change.IsError)
                {
                    return change.Error;
                }

                held = change.Value ?? held;
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                return held;
            }

            cursor = scan.Value.ScannedTo;
        }
    }

    /// <summary>True for a takeover of the node, false for a handback of it, null for any other row.</summary>
    private Result<bool?> NodeChange(LedgerEvent row, string run, string node)
    {
        if (string.Equals(row.Envelope.Type, EventTypes.WorkflowNodeTakenOver, StringComparison.Ordinal))
        {
            var taken = events.Payload<WorkflowNodeTakenOverPayload>(row);
            return taken.IsError ? taken.Error : Mine(taken.Value.RunId.Value, taken.Value.NodeId.Value, taken.Value.ItemIndex, run, node) ? (bool?)true : null;
        }

        var back = events.Payload<WorkflowNodeHandedBackPayload>(row);
        return back.IsError ? back.Error : Mine(back.Value.RunId.Value, back.Value.NodeId.Value, back.Value.ItemIndex, run, node) ? (bool?)false : null;
    }

    private static bool Mine(string rowRun, string rowNode, int itemIndex, string run, string node) =>
        itemIndex == 0 && string.Equals(rowRun, run, StringComparison.Ordinal) && string.Equals(rowNode, node, StringComparison.Ordinal);

    /// <summary>Folds the instance's rows oldest first: who opened the run that is open now, and whether a suspension holds it.</summary>
    private async ValueTask<Result<bool>> Held(LaunchId launch, InstanceAddress instance, CancellationToken cancellationToken)
    {
        var filter = new LedgerFilter(_engineRows, Item: null, Instance: instance);
        var state = default(RunState);
        var cursor = 0L;
        while (true)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var row in scan.Value.Events)
            {
                var payload = events.Payload<WorkflowEventPayload>(row);
                if (payload.IsError)
                {
                    return payload.Error;
                }

                state = state.After(payload.Value);
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return state.Suspended && string.Equals(state.Open, launch.Value, StringComparison.Ordinal);
    }

    /// <summary>The run open now (who opened it) and whether a park, escalation or takeover holds it.</summary>
    private readonly record struct RunState(string? Open, bool Suspended)
    {
        /// <summary>The state after one row; a row without detail keeps what was before, as EngineItemStates does.</summary>
        internal RunState After(WorkflowEventPayload row)
        {
            if (row.Detail?.Delta is not { } delta)
            {
                return this;
            }

            var open = delta.Frame is { } frame ? (frame.Run is null ? null : frame.Actor) : Open;
            return new RunState(delta.Outcome is null ? open : null, Suspended: delta.Suspension is not null);   // the suspension is always the state after the row
        }
    }
}
