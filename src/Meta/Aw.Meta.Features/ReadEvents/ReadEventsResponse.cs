namespace Aw.Meta.Features.ReadEvents;

/// <summary>The ReadEvents response.</summary>
/// <param name="Events">The events, oldest first.</param>
/// <param name="Next">The id of the last event returned, or the request's After when there are none: the reader passes it back as the next After.</param>
public sealed record ReadEventsResponse(IReadOnlyList<EventLine> Events, long Next);
