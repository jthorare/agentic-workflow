using System.Collections.Concurrent;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The harnesses that hit a usage limit, per job (model routing spec §5.2): in memory, keyed by job id, nothing on disk and nothing shared between jobs. A call outside a job keeps no memory. Resume clears a job's set. The job's context is held
/// weakly, so an entry goes at the next call once the job has ended.
/// </summary>
internal sealed class JobModelLimits
{
    private readonly ConcurrentDictionary<string, JobLimitEntry> _byJob = new(StringComparer.Ordinal);

    /// <summary>How many jobs have an entry now.</summary>
    internal int Count => _byJob.Count;

    internal bool IsLimited(JobContext? job, ModelHarness harness) => job is not null && _byJob.TryGetValue(job.Job.Value, out var entry) && entry.Harnesses.ContainsKey(harness);

    internal void Remember(JobContext? job, ModelHarness harness)
    {
        if (job is not null)
        {
            EntryFor(job).Harnesses[harness] = 0;
        }
    }

    internal JobLimitEntry EntryFor(JobContext job) => _byJob.GetOrAdd(job.Job.Value, static (_, owner) => new JobLimitEntry(owner), job);

    /// <summary>Drops the entries of jobs whose context is gone.</summary>
    internal void Prune()
    {
        foreach (var (key, entry) in _byJob)
        {
            if (!entry.Owner.TryGetTarget(out _))
            {
                _ = _byJob.TryRemove(key, out _);
            }
        }
    }
}
