using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>The events of a kind seen on this box, newest first, and the kind's example payload.</summary>
/// <param name="Kind">The catalog id.</param>
/// <param name="Events">The events; none when nothing of the kind was seen since serve started.</param>
/// <param name="Example">The kind's example payload from the event catalog, or null while the catalog has none.</param>
internal sealed record ListTriggerEventsResponse(string Kind, IReadOnlyList<TriggerEventView> Events, JsonObject? Example);
