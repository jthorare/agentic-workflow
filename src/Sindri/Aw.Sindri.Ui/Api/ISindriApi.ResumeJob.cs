using Aw.Kernel;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Api;

/// <summary>The job resume (<see cref="ISindriApi"/>, continued): the Resume button of a job paused on 'model-limit'. Sent once; the app never retries it.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/jobs/{id}/resume (ResumeJob, desk): wakes a job parked on 'model-limit'. A job that is not waiting is refused (SND-JOB-003); the caller just reads again.</summary>
    /// <param name="api">The API's base URL.</param>
    /// <param name="id">The job.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, or the refusal.</returns>
    public Task<Result<JobResumedView>> ResumeJob(Uri api, string id, CancellationToken cancellationToken);
}
