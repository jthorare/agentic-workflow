using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.Api;

/// <summary>The resource calls (<see cref="SindriApiClient"/>, continued). The purge is sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ResourceList>> ListResources(Uri api, string? owner, CancellationToken cancellationToken) =>
        Get<ResourceList>(api, ResourceRoutes.Listed(owner), cancellationToken);

    /// <inheritdoc />
    public Task<Result<PurgeResourcesResult>> PurgeResources(Uri api, string owner, CancellationToken cancellationToken) =>
        Call<PurgeResourcesResult>(api, HttpMethod.Post, ResourceRoutes.Purge, cancellationToken, new CallOptions(Content: AwJson.Serialize(new PurgeResourcesBody(owner), Json)));
}
