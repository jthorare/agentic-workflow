using Aw.Kernel;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context jobs (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<JobStarted>> RefreshContextMap(Uri api, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, JobRoutes.RefreshContextMap, cancellationToken);

    /// <inheritdoc />
    public Task<Result<JobStarted>> BuildContextMap(Uri api, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, JobRoutes.BuildContextMap, cancellationToken);
}
