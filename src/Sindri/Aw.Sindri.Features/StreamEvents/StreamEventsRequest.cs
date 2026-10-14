using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>Public events after a cursor, for the event stream and any consumer that keeps its own cursor (API spec §5.2).</summary>
/// <param name="After">The last sequence the consumer saw (0 for the start).</param>
/// <param name="Item">Only this item's events, or null for every event.</param>
/// <param name="Types">Only these types, or null for every public type.</param>
/// <param name="Max">The most events to return, 1 to 500.</param>
public sealed record StreamEventsRequest(long After, ItemRef? Item = null, TypeFilter? Types = null, int Max = 100) : IQuery<StreamEventsResponse>;
