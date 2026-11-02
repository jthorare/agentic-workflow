namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The wall clock of one run (sandbox spec §3.3): time counts only while the run was not suspended. An interval between two samples counts
/// when the run was unsuspended at both ends, so a park costs nothing and the tick that ends one does not count the park.
/// </summary>
/// <param name="wall">The unsuspended time allowed.</param>
public sealed class WallBudget(TimeSpan wall)
{
    private DateTimeOffset? _lastAt;
    private bool _lastSuspended;

    /// <summary>Gets the wall.</summary>
    public TimeSpan Wall { get; } = wall;

    /// <summary>Gets the unsuspended time used so far.</summary>
    public TimeSpan Used { get; private set; }

    /// <summary>Gets the unsuspended time left (never below zero).</summary>
    public TimeSpan Left => Used >= Wall ? TimeSpan.Zero : Wall - Used;

    /// <summary>Gets a value indicating whether the wall is used up.</summary>
    public bool Exhausted => Used >= Wall;

    /// <summary>Records a sample.</summary>
    /// <param name="at">When the sample was taken.</param>
    /// <param name="suspended">Whether the run was suspended then.</param>
    /// <returns>True when this sample ends a suspension (the watchdog then moves the ceiling).</returns>
    public bool Tick(DateTimeOffset at, bool suspended)
    {
        var resumed = _lastAt is not null && _lastSuspended && !suspended;
        if (_lastAt is { } before && !_lastSuspended && !suspended && at > before)
        {
            Used += at - before;
        }

        _lastAt = at;
        _lastSuspended = suspended;
        return resumed;
    }
}
