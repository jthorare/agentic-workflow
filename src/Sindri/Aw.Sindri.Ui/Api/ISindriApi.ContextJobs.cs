using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context jobs the app starts (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/context/map/refresh: builds the map again for every repo. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-JOB-409 (with 'runningJob') when a refresh is already running, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<JobStarted>> RefreshContextMap(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/context/map/build (BuildContextMap, job class): builds the map for the repo the caller reads. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-JOB-409 (with 'runningJob') when a build is already running, SND-AUTH-002 for an agent session, or SND-UI-002/003.</returns>
    public Task<Result<JobStarted>> BuildContextMap(Uri api, CancellationToken cancellationToken);
}
