using Aw.Kernel;
using Aw.Kernel.Json;
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

    /// <inheritdoc />
    public Task<Result<JobStarted>> PinConcepts(Uri api, Context.PinConceptsBody request, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, JobRoutes.PinConcepts, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<JobStarted>> SetContextPages(Uri api, Context.PagesSetBody request, CancellationToken cancellationToken) =>
        Call<JobStarted>(api, HttpMethod.Post, JobRoutes.SetContextPages, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));
}
