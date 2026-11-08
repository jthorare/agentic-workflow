namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// Notices that the machine slept. After each scheduler tick the caller asks <see cref="Observe"/>: the wall clock moved further
/// than the monotonic clock (which stands still in sleep on macOS) by more than 3 x tick means the machine woke.
/// </summary>
/// <param name="clock">The clock: wall time from GetUtcNow, monotonic time from GetTimestamp.</param>
/// <param name="tick">The scheduler tick (30 s).</param>
public sealed class WakeDetector(TimeProvider clock, TimeSpan tick)
{
    private DateTimeOffset? _wall;
    private long _stamp;

    /// <summary>Compares the wall and monotonic time that passed since the last call. The first call only sets the baseline.</summary>
    /// <returns>True when the gap between the two is over 3 x tick; the baseline is reset either way.</returns>
    public bool Observe()
    {
        var wall = clock.GetUtcNow();
        var stamp = clock.GetTimestamp();
        var previousWall = _wall;
        var previousStamp = _stamp;
        _wall = wall;
        _stamp = stamp;
        if (previousWall is null)
        {
            return false;
        }

        var monotonic = clock.GetElapsedTime(previousStamp, stamp);
        return wall - previousWall.Value - monotonic > tick * 3;
    }
}
