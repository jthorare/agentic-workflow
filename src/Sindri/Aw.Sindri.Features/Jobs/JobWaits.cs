using System.Collections.Concurrent;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The signals the jobs of this process wait on (setup-once spec §5.6): a job registers when it is parked (after 'job.waiting' is recorded), a command wakes it by (job, signal). It starts empty at every
/// process start, so a waiting job never survives a restart (recovery ends it as failed). A signal that no job waits on changes nothing.
/// </summary>
/// <param name="clock">The clock a wait's time limit runs on (a test's fake one trips it by advancing).</param>
public sealed class JobWaits(TimeProvider clock) : IJobSignals
{
    private readonly ConcurrentDictionary<string, Wait> _waits = new(StringComparer.Ordinal);

    /// <summary>Gets the clock a wait's time limit runs on.</summary>
    internal TimeProvider Clock { get; } = clock;

    /// <summary>How many jobs are parked now (they hold no job slot).</summary>
    /// <returns>The count.</returns>
    public int Count() => _waits.Count;

    /// <inheritdoc/>
    public bool IsWaiting(JobId job, string signal) => _waits.TryGetValue(job.Value, out var wait) && string.Equals(wait.Signal, signal, StringComparison.Ordinal);

    /// <inheritdoc/>
    public bool Signal(JobId job, string signal) => IsWaiting(job, signal) && _waits.TryRemove(job.Value, out var wait) && wait.Came.TrySetResult();

    /// <summary>Parks <paramref name="job"/> on <paramref name="signal"/>.</summary>
    /// <param name="job">The job.</param>
    /// <param name="signal">The signal.</param>
    /// <returns>The wait, which completes when the signal comes; disposing it unregisters it.</returns>
    internal Wait Park(JobId job, string signal)
    {
        var wait = new Wait(signal);
        _waits[job.Value] = wait;
        return wait;
    }

    /// <summary>Takes the wait of <paramref name="job"/> off the table (it timed out or was cancelled).</summary>
    /// <param name="job">The job.</param>
    internal void Unpark(JobId job) => _ = _waits.TryRemove(job.Value, out _);

    /// <summary>One parked job's signal.</summary>
    /// <param name="signal">The signal's name.</param>
    internal sealed class Wait(string signal)
    {
        internal string Signal { get; } = signal;

        internal TaskCompletionSource Came { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
