using System.Collections.Concurrent;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>One job's limited harnesses, its once-at-a-time pause, and a weak handle on the job's context so the entry goes when the job does.</summary>
/// <param name="owner">The job's context.</param>
internal sealed class JobLimitEntry(JobContext owner)
{
    public WeakReference<JobContext> Owner { get; } = new(owner);

    public ConcurrentDictionary<ModelHarness, byte> Harnesses { get; } = new();

    public SemaphoreSlim Pause { get; } = new(1, 1);
}
