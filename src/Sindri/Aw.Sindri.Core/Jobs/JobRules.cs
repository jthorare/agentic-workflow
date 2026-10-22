using Aw.Kernel;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The pure rules for jobs.</summary>
public static class JobRules
{
    /// <summary>The detail of SND-JOB-404, for every route that finds no such job.</summary>
    public const string NoSuchJob = "No such job (fix: run `sindri jobs` to list them)";

    /// <summary>Whether a job may be cancelled now.</summary>
    /// <param name="row">The job's row, or null when there is none (or the caller may not see it).</param>
    /// <returns>Success, SND-JOB-404 for no row, or SND-JOB-409 when the job already ended.</returns>
    public static Result<Unit> CanCancel(JobRow? row) => row switch
    {
        null => SindriErrors.JobNotFound.With(NoSuchJob),
        { State: JobState.Running or JobState.Waiting } => Unit.Value,
        _ => SindriErrors.JobConflict.With($"The job already ended (fix: run `sindri jobs show {row.Id.Value}` to see how)"),
    };
}
