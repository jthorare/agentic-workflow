using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Whether a launch's run is suspended, read from the ledger (sandbox spec §7, SB16): never a Temporal query per watchdog beat and never views.db, whose
/// projector may lag. The launch's 'launch.started' row names the instance it ran for; that instance's mirrored 'workflow.*' rows, folded oldest first
/// the way the items projection folds them, say who opened the run that is open now and whether a park, escalation or takeover holds it. The launch is
/// suspended when it is the actor that opened the open run and that run is held. A failed or attended (click) launch, a replaced run and an ended
/// instance are not suspended.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerRunSuspensions(ILedgerReader ledger, EventFactory events) : IRunSuspensions
{
    private const int Page = 500;

    private static readonly TypeFilter _engineRows = TypeFilter.Parse(
        $"{EventTypes.WorkflowStarted},{EventTypes.WorkflowTransition},{EventTypes.WorkflowRefused},{EventTypes.WorkflowNotified}").Value;

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

        return started.Value.Instance is { } instance
            ? await Held(launch, instance, cancellationToken).ConfigureAwait(false)
            : false;   // an attended (click) launch has no instance to hold it
    }

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
