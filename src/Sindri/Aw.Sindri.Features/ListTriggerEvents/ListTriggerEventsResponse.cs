using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.ListTriggerEvents;

/// <summary>The last events of a kind, newest first.</summary>
/// <param name="Kind">The kind asked for.</param>
/// <param name="Events">The events, at most the last 20 per connection.</param>
/// <param name="Example">The kind's sample payload, for when nothing was seen; null when the kind has none.</param>
internal sealed record ListTriggerEventsResponse(string Kind, IReadOnlyList<TriggerEventView> Events, JsonNode? Example);
