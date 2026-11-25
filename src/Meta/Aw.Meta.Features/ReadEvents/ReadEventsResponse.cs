namespace Aw.Meta.Features.ReadEvents;

/// <summary>The ReadEvents response.</summary>
/// <param name="Events">The events, oldest first.</param>
/// <param name="Next">The id of the last event returned, or the request's After when there are none: the reader passes it back as the next After.</param>
/// <param name="Oldest">The lowest event id still in the log, or Latest + 1 when the log is empty.</param>
/// <param name="Latest">The highest event id ever assigned; 0 when none was. A reader that re-snapshots after a gap puts its cursor here.</param>
/// <param name="Gap">True when the request's After + 1 is below Oldest: events between the cursor and Oldest were dropped by the retention.</param>
public sealed record ReadEventsResponse(IReadOnlyList<EventLine> Events, long Next, long Oldest, long Latest, bool Gap);
