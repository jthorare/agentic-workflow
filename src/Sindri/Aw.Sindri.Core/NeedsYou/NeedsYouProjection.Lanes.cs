using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

internal sealed partial class NeedsYouProjection
{
    // 'home.lane-row.raised' puts the row '<kind>:<key>'; 'home.lane-row.cleared' removes it (Home spec §3.3). A second raise keeps the first row and its 'since'.
    private async ValueTask<Result<Unit>> LaneRow(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        if (recorded.Envelope.Type == EventTypes.HomeLaneRowCleared)
        {
            var cleared = events.Payload<HomeLaneRowCleared>(recorded);
            return cleared.IsError ? cleared.Error : await rows.Delete(ViewNames.NeedsYou, cleared.Value.Kind + ":" + cleared.Value.Key, cancellationToken).ConfigureAwait(false);
        }

        var raised = events.Payload<HomeLaneRowRaised>(recorded);
        if (raised.IsError)
        {
            return raised.Error;
        }

        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, raised.Value.Kind + ":" + raised.Value.Key, cancellationToken).ConfigureAwait(false);
        if (existing.IsError || existing.Value is not null)
        {
            return existing.IsError ? existing.Error : Unit.Value;
        }

        return await Put(rows, NeedsYouRows.ForLane(raised.Value, scrubber), recorded, cancellationToken).ConfigureAwait(false);
    }
}
