using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The service and stop commands (<see cref="ISindriApi"/>, continued). Each is sent once; the app never retries one.</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/services/{name}/run (RunService, a job): runs the service now.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-JOB-409 (with 'runningJob') when one is running, SND-SVC-404, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<JobStarted>> RunService(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/services/{name}/pause (PauseService, desk).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, SND-SVC-404, SND-AUTH-002 or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> PauseService(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/services/{name}/resume (ResumeService, desk).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, SND-SVC-404, SND-AUTH-002 or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> ResumeService(Uri api, string name, CancellationToken cancellationToken);

    /// <summary>POST /sindri/stop (StopProcess, destructive): stops Sindri and every service and run. The app asks first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The process told to stop, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<StopAcceptedView>> StopSindri(Uri api, CancellationToken cancellationToken);
}
