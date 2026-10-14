using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.StreamEvents;

// P5 contract unit: the S1 read with the new envelope shape; unit D adds the public payload, the filters and the session scope.
internal sealed class StreamEventsService(ILedgerReader ledger) : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        var after = Math.Max(request.After, 0);
        var page = await ledger.ReadAfter(after, Math.Clamp(request.Max, 1, 500), cancellationToken).ConfigureAwait(false);
        return page.IsError
            ? page.Error
            : new StreamEventsResponse([.. page.Value.Select(Envelope)], page.Value.Count == 0 ? after : page.Value[^1].Sequence);
    }

    private static PublicEvent Envelope(LedgerEvent row)
    {
        var e = row.Envelope;
        using var payload = JsonDocument.Parse(e.Payload);
        return new PublicEvent(row.Sequence, EventId.From(e.Id), e.Type, e.SchemaVersion, e.OccurredAt, e.Actor, e.Tenant, e.Ring,
            ItemId.TryFrom(row.Item ?? string.Empty, out var item) ? item : null, CorrelationId.From(e.CorrelationId),
            e.CausationId is { } cause ? EventId.From(cause) : null, TraceId.TryFrom(e.TraceId ?? string.Empty, out var trace) ? trace : null,
            payload.RootElement.Clone());
    }
}
