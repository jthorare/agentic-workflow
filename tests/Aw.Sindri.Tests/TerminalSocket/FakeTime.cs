using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>
/// A fake clock for the socket's timers (hello deadline, ping, pong deadline). It counts the timers the session made, so a test
/// advances only after the timer it means to fire exists: an advance before that is lost and the timer never fires (#3597).
/// </summary>
internal sealed class FakeTime
{
    private readonly CountingProvider _provider = new();

    public TimeProvider Provider => _provider;

    public void Advance(TimeSpan by) => _provider.Advance(by);

    /// <summary>Waits until the session has made <paramref name="count"/> timers in all.</summary>
    public async Task WaitForTimers(int count, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (_provider.Timers < count)
        {
            await Task.Delay(5, limit.Token);
        }
    }

    private sealed class CountingProvider : FakeTimeProvider
    {
        private int _timers;

        public int Timers => Volatile.Read(ref _timers);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _ = Interlocked.Increment(ref _timers);
            return timer;
        }
    }
}
