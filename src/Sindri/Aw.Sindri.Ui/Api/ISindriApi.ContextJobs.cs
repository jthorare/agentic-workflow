using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context jobs the app starts (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>POST /sindri/context/map/refresh: builds the map again for every repo. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-JOB-409 (with 'runningJob') when a refresh is already running, or SND-UI-002/003 (an agent session may start it too, owner q93).</returns>
    public Task<Result<JobStarted>> RefreshContextMap(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/context/map/build (BuildContextMap, job class): builds the map for the repo the caller reads. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-JOB-409 (with 'runningJob') when a build is already running, or SND-UI-002/003 (an agent session may start it too, owner q93).</returns>
    public Task<Result<JobStarted>> BuildContextMap(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/context/pins (PinConcepts, job class): writes the pins to the profile and never approves. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The pins to set or remove and the profile hash the sheet read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-CONTEXT-171..177 for a refused pin, SND-PROFILE-003 when the profile moved, or SND-UI-002/003 (an agent session may start it too).</returns>
    public Task<Result<JobStarted>> PinConcepts(Uri api, Context.PinConceptsBody request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/context/pages (SetPages, job class): writes the pages to the profile and never approves. Sent once; the app never retries on its own.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The pages to set or remove and the profile hash the sheet read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started job, SND-CONTEXT-250..256 for a refused page, SND-PROFILE-003 when the profile moved, or SND-UI-002/003 (an agent session may start it too).</returns>
    public Task<Result<JobStarted>> SetContextPages(Uri api, Context.PagesSetBody request, CancellationToken cancellationToken);
}
