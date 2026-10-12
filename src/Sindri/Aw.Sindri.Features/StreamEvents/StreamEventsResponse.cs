namespace Aw.Sindri.Features.StreamEvents;

/// <summary>A page of events, oldest first.</summary>
/// <param name="Events">The events.</param>
public sealed record StreamEventsResponse(IReadOnlyList<EventView> Events);
