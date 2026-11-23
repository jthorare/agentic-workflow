using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'services' view.</summary>
/// <param name="Name">The service.</param>
/// <param name="LastRun">Its last run, or null before the first.</param>
/// <param name="Paused">True when paused (P6).</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
/// <param name="ScheduledSlot">The slot of the last scheduled run (a manual run moves <paramref name="LastRun"/> but not this, so the next slot stays the scheduler's), or null before one.</param>
/// <param name="PausedBy">The actor of the pause while paused, or null (the pause records no reason).</param>
/// <param name="PausedAt">When it was paused while paused, or null.</param>
/// <param name="ResumedAt">When a human last resumed it, or null before a resume (#3909: the source look's first-pass limit counts from it).</param>
public sealed record ServiceRow(ServiceName Name, LastRun? LastRun, bool Paused, long Sequence, DateTimeOffset? ScheduledSlot = null, string? PausedBy = null, DateTimeOffset? PausedAt = null,
    DateTimeOffset? ResumedAt = null);
