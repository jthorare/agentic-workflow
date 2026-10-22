namespace Aw.Sindri.Core.Jobs;

/// <summary>The pure rules about which job states are still alive.</summary>
public static class JobStates
{
    /// <summary>Whether a job in <paramref name="state"/> has not ended: it runs, or waits on a signal. Only these take progress, an end or a cancel.</summary>
    /// <param name="state">The state.</param>
    /// <returns>True when the job has not ended.</returns>
    public static bool IsActive(JobState state) => state is JobState.Running or JobState.Waiting;
}
