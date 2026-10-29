namespace Aw.Context.Core.Records;

/// <summary>The backoff rules of a record source (owned-index spec §5.8): pure, so the store and its fake apply the same ones.</summary>
public static class SourceBackoff
{
    /// <summary>The wait after the first failure; it doubles with each further one.</summary>
    public static readonly TimeSpan BaseDelay = TimeSpan.FromMinutes(5);

    /// <summary>The longest wait.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromHours(6);

    /// <summary>From this many failures in a row, a source that is not in backoff is 'failed' rather than 'idle'.</summary>
    public const int FailedAfter = 5;

    /// <summary>The wait after <paramref name="failures"/> failures in a row.</summary>
    /// <param name="failures">One or more.</param>
    /// <returns>5 minutes for the first, doubling, never more than 6 hours.</returns>
    public static TimeSpan Delay(int failures)
    {
        // why: 2^20 already dwarfs the cap, and a bigger shift would overflow the multiplication
        var doubled = BaseDelay * Math.Pow(2, Math.Clamp(failures - 1, 0, 20));
        return doubled < MaxDelay ? doubled : MaxDelay;
    }

    /// <summary>What the attempt makes of the failure count and the retry time. A success resets both; the last commit is the attempt's time on a success and unknown (null) on a failure.</summary>
    /// <param name="previousFailures">Failed ticks in a row before this one.</param>
    /// <param name="attempt">How the tick ended.</param>
    /// <returns>The new state; its <see cref="SourceBackoffState.LastCommit"/> is null when the attempt failed, meaning "keep the stored one" (see <see cref="Apply"/>).</returns>
    public static SourceBackoffState Next(int previousFailures, SyncAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (!attempt.Failed)
        {
            return new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: attempt.At, LastErrorText: null);
        }

        var failures = previousFailures + 1;
        return new SourceBackoffState(failures, attempt.At + Delay(failures), LastCommit: null, attempt.ErrorText);
    }

    /// <summary>The state after an attempt, keeping the previous last commit through a failure.</summary>
    /// <param name="previous">The stored state.</param>
    /// <param name="attempt">How the tick ended.</param>
    /// <returns>The state to store.</returns>
    public static SourceBackoffState Apply(SourceBackoffState previous, SyncAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var next = Next(previous.Failures, attempt);
        return next with { LastCommit = next.LastCommit ?? previous.LastCommit };
    }

    /// <summary>The word the status row shows for a source's pull.</summary>
    /// <param name="state">The stored state.</param>
    /// <param name="running">True while the source's sync lock is held.</param>
    /// <param name="now">The time now.</param>
    /// <returns>'running', 'backoff' (failing and before its next try), 'failed' (five or more failures in a row and past its next try) or 'idle'.</returns>
    public static string Pull(SourceBackoffState state, bool running, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        return running ? "running"
            : state.Failures > 0 && state.NextTry is { } next && now < next ? "backoff"
            : state.Failures >= FailedAfter ? "failed"
            : "idle";
    }
}
