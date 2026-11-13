using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>One seen event, as the trigger step lists it. Its text is provider text: untrusted.</summary>
/// <param name="DeliveryId">The stable delivery id ('github:1001').</param>
/// <param name="Connection">The connection it came from ('github:work').</param>
/// <param name="OccurredAt">When the provider says it happened.</param>
/// <param name="Summary">One line for the row ('acme/app#42 Fix login (ada)').</param>
/// <param name="Payload">The trigger JSON a run started by this event gets.</param>
/// <param name="WouldMatch">Whether a trigger with the asked filter takes it (true when no filter was given).</param>
internal sealed record TriggerEventView(string DeliveryId, string Connection, DateTimeOffset OccurredAt, string Summary, JsonObject Payload, bool WouldMatch);
