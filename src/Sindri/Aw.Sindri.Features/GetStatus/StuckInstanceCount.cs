using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>
/// The engine's stuck instances for GET /status (API spec §3.8, E-30), read from <see cref="IEngineReader"/> and kept for ten seconds, since status is polled and describing every
/// instance costs a Temporal call each. A server that doesn't answer counts none: status is where that is reported, so it must not fail with it.
/// </summary>
/// <param name="engine">The Temporal engine's read side.</param>
/// <param name="clock">The clock the stuck rule and the ten seconds read.</param>
internal sealed class StuckInstanceCount(IEngineReader engine, TimeProvider clock)
{
    private static readonly TimeSpan _kept = TimeSpan.FromSeconds(10);

    // A connect to a Temporal that isn't there can retry for seconds, and status is the page a CLI waits on: past this the engine counts as not answering.
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private volatile Counted? _last;
    private Task<int>? _refresh;

    /// <summary>How many instances are stuck now.</summary>
    /// <param name="cancellationToken">Cancels the wait for the count (not the read, which concurrent callers share).</param>
    /// <returns>The count, 0 when the engine doesn't answer.</returns>
    public async ValueTask<int> Current(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_last is { } last && now - last.At < _kept)
        {
            return last.Count;
        }

        Task<int> refresh;
        lock (_gate)
        {
            _refresh ??= Task.Run(() => Refresh(now), CancellationToken.None);   // one read in flight, shared by every miss meanwhile
            refresh = _refresh;
        }

        return await refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> Refresh(DateTimeOffset now)
    {
        try
        {
            var count = await Count(now).ConfigureAwait(false);
            _last = new Counted(now, count);
            return count;
        }
        finally
        {
            lock (_gate)
            {
                _refresh = null;
            }
        }
    }

    private async Task<int> Count(DateTimeOffset now)
    {
        using var limit = new CancellationTokenSource(_bound, clock);
        try
        {
            var running = await engine.Instances(limit.Token).AsTask().WaitAsync(limit.Token).ConfigureAwait(false);
            return running.IsSuccess ? EngineProjection.Stuck(running.Value, now) : 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException) // boundary: whatever stops the read (a deadline, a closed connection, a bug) counts none; status is where an engine that is down is reported
        {
            return 0;
        }
    }

    private sealed record Counted(DateTimeOffset At, int Count);
}
