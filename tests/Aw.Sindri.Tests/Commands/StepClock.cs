namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// A clock that moves only when a task is waiting on it: <see cref="Drive"/> fires the earliest pending timer at its due time, so a retry loop with backoff runs to its end at once and every
/// pause is exactly what the code asked for.
/// </summary>
internal sealed class StepClock : TimeProvider
{
    private readonly List<Timer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _now.Ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        _ = timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Runs <paramref name="task"/> to its end, firing each timer it waits on at its due time.</summary>
    /// <param name="task">The task.</param>
    /// <typeparam name="T">Its result.</typeparam>
    /// <returns>The task's result.</returns>
    public async Task<T> Drive<T>(Task<T> task)
    {
        while (!task.IsCompleted)
        {
            Timer? next;
            lock (_timers)
            {
                next = _timers.Where(static t => t.Due is not null).OrderBy(static t => t.Due).FirstOrDefault();
            }

            if (next is null)
            {
                await Task.Yield();   // the task has not reached its next wait yet
                continue;
            }

            _now = next.Due!.Value > _now ? next.Due.Value : _now;
            next.Fire();
            await Task.Yield();
        }

        return await task;
    }

    private sealed class Timer(StepClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._timers)
            {
                if (!clock._timers.Contains(this))
                {
                    clock._timers.Add(this);
                }

                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
            }

            return true;
        }

        public void Fire()
        {
            lock (clock._timers)
            {
                Due = null;
            }

            callback(state);
        }

        public void Dispose()
        {
            lock (clock._timers)
            {
                _ = clock._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (clock._timers)
            {
                _ = clock._timers.Remove(this);
            }

            return ValueTask.CompletedTask;
        }
    }
}
