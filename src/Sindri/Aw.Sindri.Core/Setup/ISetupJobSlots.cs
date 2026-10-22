using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>What the setup list may read of the setup service's single-slot job queue: whether a job holds the slot or waits for it.</summary>
public interface ISetupJobSlots
{
    /// <summary>Whether the job <paramref name="kind"/>/<paramref name="key"/> holds the slot or waits in the queue.</summary>
    /// <param name="kind">The job's kind.</param>
    /// <param name="key">The job's key.</param>
    /// <returns>True when it holds the slot or waits.</returns>
    public bool Holds(JobKind kind, JobKey key);
}
