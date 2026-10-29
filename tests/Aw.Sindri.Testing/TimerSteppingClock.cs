using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Testing;

/// <summary>
/// A fake clock that knows when its timers are due, so a test moves time from one due timer to the next instead of a fixed tick with a real pause after each. Before every step it waits until
/// the code under test has stopped arming timers (a short real quiet period), because a loop re-arms its next Task.Delay on another thread after the last one fires.
/// </summary>
/// <param name="start">The first instant.</param>
public sealed class TimerSteppingClock(DateTimeOffset start) : FakeTimeProvider(start)
{
    // why: a step never passes a timer it knows of, but one the subject has not armed yet is unknown, so no step is longer than this. A timer armed later than the
    // quiet period (a busy box, issue 1705: the wrap's 1 s exit retry) is caught by the next step; at 10 s one step jumped past the whole span and the retry never fired.
    private static readonly TimeSpan _longestStep = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _quiet = TimeSpan.FromMilliseconds(2);

    private readonly Lock _gate = new();
    private readonly List<Tracked> _timers = [];
    private int _created;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Tracked(this, base.CreateTimer(callback, state, dueTime, period), GetUtcNow(), dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        _ = Interlocked.Increment(ref _created);
        return timer;
    }

    /// <summary>Moves the clock by <paramref name="span"/>, stopping at each timer that falls due on the way so the code under test can arm its next one before time moves on.</summary>
    /// <param name="span">How far to move.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends when the clock has moved by <paramref name="span"/>.</returns>
    public async Task RunFor(TimeSpan span, CancellationToken cancellationToken)
    {
        var end = GetUtcNow() + span;
        while (GetUtcNow() < end)
        {
            await Settle(cancellationToken).ConfigureAwait(false);
            var now = GetUtcNow();
            var step = TimeSpan.FromTicks(Math.Min((end - now).Ticks, _longestStep.Ticks));
            if (NextDue(now) is { } due && due > now && due - now < step)
            {
                step = due - now;
            }

            Advance(step);
        }
    }

    private async Task Settle(CancellationToken cancellationToken)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref _created);
            await Task.Delay(_quiet, System, cancellationToken).ConfigureAwait(false);   // why: System is the inherited real clock; the pause is real time
        }
        while (Volatile.Read(ref _created) != seen);
    }

    private DateTimeOffset? NextDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            DateTimeOffset? next = null;
            foreach (var timer in _timers)
            {
                if (timer.DueAfter(now) is { } due && (next is null || due < next))
                {
                    next = due;
                }
            }

            return next;
        }
    }

    private void Forget(Tracked timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    private sealed class Tracked(TimerSteppingClock owner, ITimer inner, DateTimeOffset at, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        private DateTimeOffset _at = at;
        private TimeSpan _dueTime = dueTime;
        private TimeSpan _period = period;

        public bool Change(TimeSpan newDueTime, TimeSpan newPeriod)
        {
            _at = owner.GetUtcNow();
            _dueTime = newDueTime;
            _period = newPeriod;
            return inner.Change(newDueTime, newPeriod);
        }

        public void Dispose()
        {
            owner.Forget(this);
            inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            owner.Forget(this);
            return inner.DisposeAsync();
        }

        /// <summary>The next instant after <paramref name="now"/> this timer fires, or null when it won't.</summary>
        public DateTimeOffset? DueAfter(DateTimeOffset now)
        {
            if (_dueTime == Timeout.InfiniteTimeSpan)
            {
                return null;
            }

            var first = _at + _dueTime;
            if (first > now)
            {
                return first;
            }

            if (_period <= TimeSpan.Zero || _period == Timeout.InfiniteTimeSpan)
            {
                return null;
            }

            var missed = ((now - first).Ticks / _period.Ticks) + 1;
            return first + TimeSpan.FromTicks(missed * _period.Ticks);
        }
    }
}
