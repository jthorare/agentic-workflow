using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A fake clock that knows when a waiter has caught up with it: it counts the timers it handed out, and which of them fired and were not yet disposed. The gate's waiter reacts to a fired 5-min slice on a pool thread, so a test that moves the clock again before that reaction starts the next slice late.</summary>
internal sealed class SliceClock : TimeProvider
{
    private readonly FakeTimeProvider _inner = new();
    private readonly Lock _gate = new();
    private int _armed;
    private int _fired;

    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    public override long GetTimestamp() => _inner.GetTimestamp();

    public override long TimestampFrequency => _inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => _inner.LocalTimeZone;

    public void Advance(TimeSpan by) => _inner.Advance(by);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new CountedTimer(this);
        lock (_gate)
        {
            _armed++;
        }

        timer.Inner = _inner.CreateTimer(
            s =>
            {
                timer.Fire();
                callback(s);
            },
            state,
            dueTime,
            period);
        return timer;
    }

    /// <summary>Waits in real time, up to 10 s, until the task is done or every fired timer was disposed and a new one is armed: the waiter has read the clock and is waiting again.</summary>
    public async Task Settle(Task task, CancellationToken cancellationToken)
    {
        var until = RealTime.NowOffset.AddSeconds(10);
        while (!task.IsCompleted && !Waiting() && RealTime.NowOffset < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), System, cancellationToken);
        }
    }

    private bool Waiting()
    {
        lock (_gate)
        {
            return _fired == 0 && _armed > 0;
        }
    }

    private sealed class CountedTimer(SliceClock clock) : ITimer
    {
        private bool _fired;
        private bool _disposed;

        public ITimer? Inner { get; set; }

        public void Fire()
        {
            lock (clock._gate)
            {
                if (!_fired && !_disposed)
                {
                    (_fired, clock._fired) = (true, clock._fired + 1);
                }
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => Inner!.Change(dueTime, period);

        public void Dispose()
        {
            if (Release())
            {
                Inner?.Dispose();
            }
        }

        public ValueTask DisposeAsync() => Release() && Inner is not null ? Inner.DisposeAsync() : default;

        // Counts this timer out once; false when it was already disposed.
        private bool Release()
        {
            lock (clock._gate)
            {
                if (_disposed)
                {
                    return false;
                }

                _disposed = true;
                clock._armed--;
                if (_fired)
                {
                    clock._fired--;
                }

                return true;
            }
        }
    }
}
