namespace Aw.Meta.Core.Events;

/// <summary>A page of the event log plus the log's id bounds, read together so <paramref name="Latest"/> is never below the page's last id.</summary>
/// <param name="Events">The events, ascending by id.</param>
/// <param name="Oldest">The lowest id still in the log, or Latest + 1 when the log is empty.</param>
/// <param name="Latest">The highest id ever assigned (it survives a prune that empties the log); 0 when no event was ever appended.</param>
public sealed record MetaEventPage(IReadOnlyList<MetaEvent> Events, long Oldest, long Latest);
