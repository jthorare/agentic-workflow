namespace Aw.Sindri.Core.Events;

/// <summary>The 'service.heartbeat' payload: the slot it filled and when it ran.</summary>
/// <param name="Slot">The scheduled time.</param>
/// <param name="RanAt">When it ran.</param>
public sealed record Heartbeat(DateTimeOffset Slot, DateTimeOffset RanAt);
