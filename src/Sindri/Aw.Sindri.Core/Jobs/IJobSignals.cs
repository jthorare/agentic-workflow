using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The command side of <see cref="IJobWaiting"/>: asks whether a job of this process waits on a signal, and wakes it.</summary>
public interface IJobSignals
{
    /// <summary>Whether <paramref name="job"/> is parked on <paramref name="signal"/> now.</summary>
    /// <param name="job">The job.</param>
    /// <param name="signal">The signal's name.</param>
    /// <returns>True when it is.</returns>
    public bool IsWaiting(JobId job, string signal);

    /// <summary>Wakes <paramref name="job"/> if it is parked on <paramref name="signal"/>.</summary>
    /// <param name="job">The job.</param>
    /// <param name="signal">The signal's name.</param>
    /// <returns>True when it was waiting and now runs on; false when it was not waiting on that signal (nothing changed).</returns>
    public bool Signal(JobId job, string signal);
}
