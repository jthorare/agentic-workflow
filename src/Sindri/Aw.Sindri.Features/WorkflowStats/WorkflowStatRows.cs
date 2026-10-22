using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.WorkflowStats;

/// <summary>
/// Reads the ledger's workflow rows (every 'workflow.*' event) in ledger order as the plain <see cref="StatEvent"/>s the recipe stats count. The
/// instance's address is the second part of the row's idempotency key ('workflow:{address}:…'); a row that has no such key or whose payload
/// does not read as a workflow payload is left out. A run that began before the rows carried the recipe's id has no recipe and is not counted.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
internal sealed class WorkflowStatRows(ILedgerReader ledger, EventFactory events) : IWorkflowStatRows
{
    private const int Page = 500;
    private static readonly LedgerFilter _filter = new(TypeFilter.Parse("workflow.*").Value, Item: null);

    /// <summary>Every workflow row in the ledger, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows.</returns>
    public async ValueTask<Result<IReadOnlyList<StatEvent>>> Read(CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var rows = new List<StatEvent>();
        for (var cursor = 0L; cursor < head.Value;)
        {
            var scan = await ledger.Scan(cursor, Page, _filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var recorded in scan.Value.Events)
            {
                var payload = events.Payload<WorkflowEventPayload>(recorded);
                if (payload.IsSuccess && StatEventMap.AddressOf(recorded.IdempotencyKey) is { } address)
                {
                    rows.Add(StatEventMap.Of(address, recorded.Envelope.OccurredAt, payload.Value));
                }
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return rows;
    }
}
