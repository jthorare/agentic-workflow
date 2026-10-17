using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Testing;

/// <summary>A fake clock that can move itself: with <see cref="AdvanceOnDelay"/> on, a timer (a Task.Delay) that is created fires at once by moving time to its due time, so a wait runs to the end deterministically with no real sleeping.</summary>
public sealed class RigClock : FakeTimeProvider
{
    /// <summary>Gets or sets a value indicating whether a delay moves the clock by its length and completes at once.</summary>
    public bool AdvanceOnDelay { get; set; }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        if (AdvanceOnDelay && dueTime > TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
        {
            Advance(dueTime);
        }

        return timer;
    }
}
