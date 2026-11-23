namespace Aw.Sindri.Core.Events;

/// <summary>The owner looked at Home, which moves the 'since you last looked' mark (home spec §6.1).</summary>
/// <param name="LookedAt">When the owner last looked.</param>
public sealed record HomeCursorMoved(DateTimeOffset LookedAt);
