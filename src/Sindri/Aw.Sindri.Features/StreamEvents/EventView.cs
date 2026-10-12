namespace Aw.Sindri.Features.StreamEvents;

/// <summary>An event as clients see it: position, envelope fields and the payload JSON text.</summary>
/// <param name="Sequence">The ledger position (the consumer's cursor).</param>
/// <param name="Type">The event type.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Actor">Who acted.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="Ring">The ring.</param>
/// <param name="Epoch">The writer's lease epoch.</param>
/// <param name="Payload">The payload as JSON text.</param>
public sealed record EventView(long Sequence, string Type, DateTimeOffset OccurredAt, string Actor, string Tenant, int Ring, long Epoch, string Payload);
