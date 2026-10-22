namespace Aw.Sindri.Core.Jobs;

/// <summary>The wire names of the job states, for the text a person reads and the column the views filter on (the JSON converter writes the same names).</summary>
public static class JobStateNames
{
    /// <summary>The lower-case name of <paramref name="state"/>.</summary>
    /// <param name="state">The state.</param>
    /// <returns>For example 'succeeded'.</returns>
    public static string Of(JobState state) => state switch
    {
        JobState.Running => "running",
        JobState.Succeeded => "succeeded",
        JobState.Failed => "failed",
        JobState.Cancelled => "cancelled",
        JobState.Waiting => "waiting",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a job state"),
    };
}
