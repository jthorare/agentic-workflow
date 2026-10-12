using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The fake clock as a background loop sees it, counting the waits it arms. A test steps only once the loop has armed
/// its next wait, so no step is lost to a loop that hasn't reached its delay yet (BackgroundService starts the loop on
/// another thread) and nothing depends on real-time sleeps.
/// </summary>
internal sealed class SteppedClock(FakeTimeProvider fake) : TimeProvider
{
    private int _timers;
    private int _seen;

    public override TimeZoneInfo LocalTimeZone => fake.LocalTimeZone;

    public override long TimestampFrequency => fake.TimestampFrequency;

    public override DateTimeOffset GetUtcNow() => fake.GetUtcNow();

    public override long GetTimestamp() => fake.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = fake.CreateTimer(callback, state, dueTime, period);
        _ = Interlocked.Increment(ref _timers);
        return timer;
    }

    /// <summary>Waits until the loop armed a wait it hadn't when the last step began, then advances the clock by <paramref name="by"/>.</summary>
    public async Task Step(TimeSpan by)
    {
        await Armed();
        _seen = Volatile.Read(ref _timers);
        fake.Advance(by);
    }

    /// <summary>
    /// Waits until the loop has armed its next wait (it finished the iteration the last step started), or <paramref name="orElse"/> holds.
    /// A zero-length wait arms no timer (Task.Delay with TimeSpan.Zero completes at once), so a loop that waits zero
    /// goes round again and is seen only at its next non-zero wait.
    /// </summary>
    public async Task Armed(Func<bool>? orElse = null)
    {
        for (var waited = 0; Volatile.Read(ref _timers) <= _seen && orElse?.Invoke() != true; waited += 2)
        {
            Assert.True(waited < 5000, "the loop didn't arm its next wait within 5 s");
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }
    }
}
