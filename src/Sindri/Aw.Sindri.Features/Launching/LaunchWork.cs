using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// What the launcher service remembers between ticks (UI spec E-1): where it read the ledger to, the requests with no outcome, which
/// of them it has handed out and which it has claimed, and the viewers it still polls. In memory only: a restart sweeps whatever was
/// in flight (SND-LAUNCH-007), so nothing here needs to survive one. A request is claimed at most once, and a claimed request is
/// never swept by the expiry (the claim is the launch's right to finish within its limit).
/// </summary>
internal sealed class LaunchWork
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LaunchId, PendingLaunch> _pending = [];
    private readonly HashSet<LaunchId> _handedOut = [];
    private readonly HashSet<LaunchId> _claimed = [];
    private readonly Dictionary<LaunchId, WatchedViewer> _watched = [];

    private DateTimeOffset _nextProbe = DateTimeOffset.MinValue;

    /// <summary>True once the start-up sweep ran.</summary>
    public bool Recovered { get; set; }

    /// <summary>The last ledger sequence read.</summary>
    public long Cursor { get; set; }

    /// <summary>Whether the launch targets are due for a probe, and if so schedules the next one at <paramref name="next"/>.</summary>
    /// <param name="now">The time.</param>
    /// <param name="next">When the one after this is due.</param>
    /// <returns>True when the caller should probe now.</returns>
    public bool TakeProbe(DateTimeOffset now, DateTimeOffset next)
    {
        lock (_gate)
        {
            if (now < _nextProbe)
            {
                return false;
            }

            _nextProbe = next;
            return true;
        }
    }

    /// <summary>A launch failed: the next tick probes the targets again.</summary>
    public void ProbeSoon()
    {
        lock (_gate)
        {
            _nextProbe = DateTimeOffset.MinValue;
        }
    }

    /// <summary>Notes a request with no outcome yet; a replay of the same request changes nothing.</summary>
    /// <param name="pending">The request.</param>
    public void Add(PendingLaunch pending)
    {
        lock (_gate)
        {
            _ = _pending.TryAdd(pending.Request.Launch, pending);
        }
    }

    /// <summary>A launch got an outcome: it is no longer pending.</summary>
    /// <param name="launch">The launch.</param>
    public void Settle(LaunchId launch)
    {
        lock (_gate)
        {
            _ = _pending.Remove(launch);
            _ = _handedOut.Remove(launch);
        }
    }

    /// <summary>Whether the launch is still waiting for an outcome.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>True when it is.</returns>
    public bool IsPending(LaunchId launch)
    {
        lock (_gate)
        {
            return _pending.ContainsKey(launch);
        }
    }

    /// <summary>The requests past their deadline that nobody has claimed: the expiry fails them.</summary>
    /// <param name="timings">The limits and the clock.</param>
    /// <returns>The expired requests.</returns>
    public IReadOnlyList<PendingLaunch> Expired(LaunchTimings timings)
    {
        var now = timings.Clock.GetUtcNow();
        lock (_gate)
        {
            return [.. _pending.Values.Where(p => now - p.At >= timings.Deadline && !_claimed.Contains(p.Request.Launch))];
        }
    }

    /// <summary>The requests not yet handed out, each marked handed out.</summary>
    /// <returns>The new requests, oldest first.</returns>
    public IReadOnlyList<PendingLaunch> TakeDue()
    {
        lock (_gate)
        {
            var due = _pending.Values.Where(p => _handedOut.Add(p.Request.Launch)).OrderBy(static p => p.Sequence).ToList();
            return due;
        }
    }

    /// <summary>
    /// Claims a request for launching: it must still be pending, unclaimed, and have at least the claim reserve of its deadline left.
    /// </summary>
    /// <param name="pending">The request.</param>
    /// <param name="timings">The limits and the clock.</param>
    /// <returns>True when the caller may launch.</returns>
    public bool TryClaim(PendingLaunch pending, LaunchTimings timings)
    {
        var now = timings.Clock.GetUtcNow();
        lock (_gate)
        {
            return _pending.ContainsKey(pending.Request.Launch)
                && pending.At + timings.Deadline - now >= timings.ClaimReserve
                && _claimed.Add(pending.Request.Launch);
        }
    }

    /// <summary>The launch finished (or gave up): its claim is released.</summary>
    /// <param name="launch">The launch.</param>
    public void Release(LaunchId launch)
    {
        lock (_gate)
        {
            _ = _claimed.Remove(launch);
        }
    }

    /// <summary>Starts polling a launch's viewer.</summary>
    /// <param name="watched">The launch, its session and when to stop.</param>
    public void Watch(WatchedViewer watched)
    {
        lock (_gate)
        {
            _watched[watched.Launch] = watched;
        }
    }

    /// <summary>Stops polling a launch's viewer.</summary>
    /// <param name="launch">The launch.</param>
    public void Unwatch(LaunchId launch)
    {
        lock (_gate)
        {
            _ = _watched.Remove(launch);
        }
    }

    /// <summary>The viewers being polled.</summary>
    /// <returns>A copy.</returns>
    public IReadOnlyList<WatchedViewer> Watched()
    {
        lock (_gate)
        {
            return [.. _watched.Values];
        }
    }
}
