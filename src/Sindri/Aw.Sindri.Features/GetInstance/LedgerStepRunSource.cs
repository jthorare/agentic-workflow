using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// Reads the ledger to its head and keeps the instance's workflow events (<see cref="InstanceEvents.Belongs(LedgerEvent, InstanceAddress)"/>),
/// the same rows 'workflow events' serves. A row whose payload does not read as a workflow payload is left out.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
internal sealed class LedgerStepRunSource(ILedgerReader ledger, EventFactory events) : IStepRunSource
{
    private const int Page = 500;

    public async ValueTask<Result<IReadOnlyList<RecordedWorkflowEvent>>> EventsOf(InstanceAddress address, CancellationToken cancellationToken)
    {
        var found = new List<RecordedWorkflowEvent>();
        long cursor = 0;
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value.Where(e => InstanceEvents.Belongs(e, address)))
            {
                var payload = events.Payload<WorkflowEventPayload>(recorded);
                if (payload.IsSuccess)
                {
                    found.Add(new RecordedWorkflowEvent(recorded.Envelope.Type, recorded.Envelope.OccurredAt, recorded.Envelope.Actor, payload.Value));
                }
            }

            if (page.Value.Count < Page)
            {
                return found;
            }

            cursor = page.Value[^1].Sequence;
        }
    }
}
