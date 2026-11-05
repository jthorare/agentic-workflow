using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>
/// One graphify run at a time per host (a host has one map store and one cluster source): a map build, a refresh and a backtest's as-of cut all take it, and a second caller
/// waits for the first instead of failing, so two refreshes of one map never write over each other. A waiter follows the holder's progress (#2106): it keeps waiting while the holder writes lines
/// through <see cref="MapBuildTurn.Progress"/> and gives up only when the holder has written nothing for <see cref="WaitLimit"/>, so a slow build that is still alive is never cut off and one that hung does not hold every later build forever.
/// One instance per host, in this process only: a build started by another process is not covered. The box-wide heavy-job lock is separate (a backtest takes its own heavy slot) and does not replace this gate.
/// </summary>
/// <param name="clock">Times the wait for the slot and the holder's silence (<see cref="WaitLimit"/>).</param>
public sealed class MapBuildGate(TimeProvider clock) : IDisposable
{
    private static readonly TimeSpan _checkEvery = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Lock _holderLock = new();
    private string? _holder;
    private bool _held;
    private DateTimeOffset _lastLine;

    /// <summary>How long a waiter tolerates a holder that writes no line before giving up (#2106: a build that never ends must not hold every later build forever).</summary>
    public static TimeSpan WaitLimit { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Takes the gate at once when it is free; otherwise says who holds it and waits for them: every 5 min it says it is still waiting and how long ago the holder last wrote a line, and it gives up
    /// when that is <see cref="WaitLimit"/>. Holds the gate until the returned lease is disposed.
    /// </summary>
    /// <param name="job">The job the caller runs for, named to a caller that has to wait behind it; null when it runs for no job.</param>
    /// <param name="progress">Told that the call is waiting and for whom, then every 5 min that it still is (which also keeps the caller's own stall watch fed); null to say nothing. The lease's <see cref="MapBuildTurn.Progress"/> forwards to it.</param>
    /// <param name="cancellationToken">Cancels the wait (the call throws, as before).</param>
    /// <returns>The lease, which says whether the call had to wait; <see cref="ContextErrors.MapBuildWaitTimedOut"/> when the holder wrote nothing for <see cref="WaitLimit"/>.</returns>
    public async ValueTask<Result<MapBuildTurn>> Enter(string? job, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var waited = !await _one.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        if (waited)
        {
            var timedOut = await WaitForHolder(progress, cancellationToken).ConfigureAwait(false);
            if (timedOut is { } silent)
            {
                return silent;
            }
        }

        lock (_holderLock)
        {
            (_holder, _held, _lastLine) = (job, true, clock.GetUtcNow());
        }

        return new MapBuildTurn(this, waited, progress);
    }

    // Waits for the semaphore in slices of 5 min; after each slice the holder's silence is read, and the wait ends with the error when it reached WaitLimit.
    private async ValueTask<Error?> WaitForHolder(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var (holder, _) = Holding();
        progress?.Report(holder is null ? "waiting for the running map build" : $"waiting for the map build of job {holder}");
        while (true)
        {
            using var slice = new CancellationTokenSource(_checkEvery, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, slice.Token);
            try
            {
                await _one.WaitAsync(linked.Token).ConfigureAwait(false);
                return null;
            }
            catch (OperationCanceledException) when (slice.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // why: the slice ran out; the holder is looked at again below
            }

            var (job, silence) = Holding();
            if (silence is null)
            {
                continue;   // why: the holder left between the slice and this look, so the next wait takes the gate at once
            }

            if (silence.Value >= WaitLimit)
            {
                return ContextErrors.MapBuildWaitTimedOut.With(string.Create(CultureInfo.InvariantCulture, $"the build ahead (job {job ?? "unknown"}) wrote no line for {(int)WaitLimit.TotalMinutes} min"));
            }

            progress?.Report(string.Create(CultureInfo.InvariantCulture, $"still waiting for the map build of job {job ?? "unknown"} (its last line {(int)silence.Value.TotalMinutes} min ago)"));
        }
    }

    // The holder's job and how long ago it last wrote a line; (null, null) when no one holds the gate.
    private (string? Job, TimeSpan? Silence) Holding()
    {
        lock (_holderLock)
        {
            return _held ? (_holder, clock.GetUtcNow() - _lastLine) : (null, null);
        }
    }

    /// <summary>Releases the gate's semaphore; no lease may be waiting.</summary>
    public void Dispose() => _one.Dispose();

    internal void Stamp()
    {
        lock (_holderLock)
        {
            _lastLine = clock.GetUtcNow();
        }
    }

    internal void Leave()
    {
        // why: cleared before the release, so the next holder's name is never overwritten by this one's late clear.
        lock (_holderLock)
        {
            (_holder, _held) = (null, false);
        }

        _ = _one.Release();
    }
}
