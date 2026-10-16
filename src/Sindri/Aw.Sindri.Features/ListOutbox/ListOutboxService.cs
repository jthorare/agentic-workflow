using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Features.ListOutbox;

/// <summary>
/// Folds the ledger's effect rows ('outbox.pending', 'outbox.dispatched', 'outbox.refused', 'outbox.cancelled') by command, the latest row
/// winning, and keeps those in the requested state (engine spec §6.3; engine P6 unit (c)).
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each row's payload.</param>
internal sealed class ListOutboxService(ILedgerReader ledger, EventFactory events) : IService<ListOutboxRequest, ListOutboxResponse>
{
    private const int Page = 500;
    private const string Prefix = "outbox.";

    public async ValueTask<Result<ListOutboxResponse>> Handle(ListOutboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var latest = new Dictionary<string, OutboxCommandView>(StringComparer.Ordinal);
        var order = new List<string>();
        long cursor = 0;
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value.Where(static e => e.Envelope.Type.StartsWith(Prefix, StringComparison.Ordinal)))
            {
                Fold(recorded, latest, order);
            }

            if (page.Value.Count < Page)
            {
                break;
            }

            cursor = page.Value[^1].Sequence;
        }

        return new ListOutboxResponse([.. order.Select(c => latest[c]).Where(c => request.State is not { } state || c.State == state)]);
    }

    /// <summary>Keeps <paramref name="recorded"/> as its command's latest row; a row that isn't an effect row is skipped.</summary>
    private void Fold(LedgerEvent recorded, Dictionary<string, OutboxCommandView> latest, List<string> order)
    {
        var read = events.Payload<EffectRow>(recorded);
        if (read.IsError)
        {
            return;
        }

        var row = read.Value;
        if (!latest.ContainsKey(row.Command))
        {
            order.Add(row.Command);
        }

        latest[row.Command] = new OutboxCommandView(recorded.Sequence, recorded.Envelope.OccurredAt, row.Identity, row.Command, row.Kind,
            row.Origin, row.State, row.Instance, row.Code);
    }
}
