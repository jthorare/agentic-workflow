namespace Aw.Sindri.Testing;

/// <summary>
/// A TimeProvider whose wall clock and monotonic clock move apart on demand: <see cref="Advance"/> moves both, <see cref="JumpWall"/>
/// only the wall clock (what a sleeping laptop does on macOS).
/// </summary>
/// <param name="start">The first wall time.</param>
public sealed class SplitClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _wall = start;
    private long _ticks;

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Moves the wall clock and the monotonic clock by the same amount.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by)
    {
        _wall += by;
        _ticks += by.Ticks;
    }

    /// <summary>Moves only the wall clock.</summary>
    /// <param name="by">How far.</param>
    public void JumpWall(TimeSpan by) => _wall += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _wall;

    /// <inheritdoc />
    public override long GetTimestamp() => _ticks;
}
