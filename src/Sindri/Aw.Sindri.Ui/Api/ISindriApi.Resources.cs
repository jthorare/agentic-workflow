using Aw.Kernel;
using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.Api;

/// <summary>The resource calls (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/resources.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="owner">Only this owner's resources, or null for all.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The resources, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ResourceList>> ListResources(Uri api, string? owner, CancellationToken cancellationToken);

    /// <summary>POST /sindri/resources/purge: removes every resource of an owner. Refused while the owner is active.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many were removed, the API's refusal (with its reason), or SND-UI-002/003.</returns>
    public Task<Result<PurgeResourcesResult>> PurgeResources(Uri api, string owner, CancellationToken cancellationToken);
}
