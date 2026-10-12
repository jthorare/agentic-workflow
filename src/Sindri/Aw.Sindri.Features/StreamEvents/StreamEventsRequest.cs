namespace Aw.Sindri.Features.StreamEvents;

/// <summary>Events after a cursor, for the event stream and any consumer that keeps its own cursor.</summary>
/// <param name="After">The last sequence the consumer saw (0 for the start).</param>
/// <param name="Max">The most events to return, 1 to 500.</param>
public sealed record StreamEventsRequest(long After, int Max = 100);
