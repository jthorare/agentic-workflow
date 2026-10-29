namespace Aw.Context.Features;

/// <summary>
/// One graphify run at a time per host (a host has one map store and one cluster source): a map build, a refresh and a backtest's as-of cut all take it, and a second caller
/// waits for the first instead of failing, so two refreshes of one map never write over each other. One instance per host, in this process only: a build started by another process is not covered. The box-wide heavy-job lock is separate (a backtest takes its own heavy slot) and does not replace this gate.
/// </summary>
internal sealed class MapBuildGate : IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Lock _holderLock = new();
    private string? _holder;

    /// <summary>Takes the gate at once when it is free; otherwise says who holds it and waits for them. Holds the gate until the returned lease is disposed.</summary>
    /// <param name="job">The job the caller runs for, named to a caller that has to wait behind it; null when it runs for no job.</param>
    /// <param name="progress">Told once, before the wait starts, that the call is waiting and for whom; null to say nothing.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The lease.</returns>
    public async ValueTask<IDisposable> Enter(string? job, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (!await _one.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            // why: the holder is read after the failed take, so a holder that left in between reads as "no one" and the wait below ends at once.
            string? holder;
            lock (_holderLock)
            {
                holder = _holder;
            }

            progress?.Report(holder is null ? "waiting for the running map build" : $"waiting for the map build of job {holder}");
            await _one.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_holderLock)
        {
            _holder = job;
        }

        return new Lease(this);
    }

    /// <summary>Releases the gate's semaphore; no lease may be waiting.</summary>
    public void Dispose() => _one.Dispose();

    private void Release()
    {
        // why: cleared before the release, so the next holder's name is never overwritten by this one's late clear.
        lock (_holderLock)
        {
            _holder = null;
        }

        _ = _one.Release();
    }

    private sealed class Lease(MapBuildGate gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
