using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StreamEvents;

internal sealed class StreamEventsService(ILedgerStore ledger) : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var page = await ledger.ReadAfter(Math.Max(request.After, 0), Math.Clamp(request.Max, 1, 500), cancellationToken).ConfigureAwait(false);
        return page.IsError
            ? page.Error
            : new StreamEventsResponse([.. page.Value.Select(e => new EventView(e.Sequence, e.Envelope.Type, e.Envelope.OccurredAt,
                e.Envelope.Actor, e.Envelope.Tenant, e.Envelope.Ring, e.Epoch, e.Envelope.Payload))]);
    }
}
