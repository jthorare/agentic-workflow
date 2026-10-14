using System.Text.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// An event as the stream and ledger reads send it (API spec §5.3): the ledger row's envelope, flattened, with the payload
/// the catalog's public record carries, sanitized. It replaces S1's EventView. Shared by StreamEvents and ReadLedger, so it lives in Core.
/// </summary>
/// <param name="Sequence">The ledger position (the consumer's cursor and the SSE id).</param>
/// <param name="Id">The event id.</param>
/// <param name="Type">The event type.</param>
/// <param name="SchemaVersion">The payload's schema version.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Actor">Who acted, an open string.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="Ring">The ring.</param>
/// <param name="Item">The work item, if any.</param>
/// <param name="CorrelationId">The correlation id.</param>
/// <param name="CausationId">The event or command that caused it, if any.</param>
/// <param name="TraceId">The trace id, if any.</param>
/// <param name="Payload">The public payload as a JSON object, or JSON null when the row's payload can't be read.</param>
public sealed record PublicEvent(
    long Sequence,
    EventId Id,
    string Type,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    string Actor,
    string Tenant,
    int Ring,
    ItemId? Item,
    CorrelationId CorrelationId,
    EventId? CausationId,
    TraceId? TraceId,
    JsonElement Payload);
