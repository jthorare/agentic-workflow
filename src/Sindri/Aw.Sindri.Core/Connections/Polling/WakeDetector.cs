namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// Notices that the machine slept. After each scheduler tick the caller asks <see cref="Observe"/>: the wall clock moved further
/// than the monotonic clock (which stands still in sleep on macOS) by more than 3 x tick means the machine woke.
/// </summary>
/// <param name="clock">The clock: wall time from GetUtcNow, monotonic time from GetTimestamp.</param>
/// <param name="tick">The scheduler tick (30 s).</param>
public sealed class WakeDetector(TimeProvider clock, TimeSpan tick)
{
    private readonly TimeProvider _clock = clock;
    private readonly TimeSpan _tick = tick;

    /// <summary>Compares the wall and monotonic time that passed since the last call. The first call only sets the baseline.</summary>
    /// <returns>True when the gap between the two is over 3 x tick; the baseline is reset either way.</returns>
    public bool Observe()
    {
        _ = (_clock, _tick);
        throw new NotSupportedException("red stub: PR 11 part 1");
    }
}
