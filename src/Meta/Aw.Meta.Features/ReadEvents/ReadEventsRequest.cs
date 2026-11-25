namespace Aw.Meta.Features.ReadEvents;

/// <summary>The ReadEvents request: the events after a cursor, optionally of one kind or one lane.</summary>
/// <param name="After">The last event id the reader has; 0 for the start.</param>
/// <param name="Kind">Only this kind, or null.</param>
/// <param name="Lane">Only this lane, or null.</param>
/// <param name="Max">At most this many events (1 or more).</param>
public sealed record ReadEventsRequest(long After, string? Kind, string? Lane, int Max);
