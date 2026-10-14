using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>A page of public events, oldest first.</summary>
/// <param name="Events">The events.</param>
/// <param name="ScannedTo">The last sequence the read looked at, sent or not: the stream's next cursor.</param>
public sealed record StreamEventsResponse(IReadOnlyList<PublicEvent> Events, long ScannedTo);
