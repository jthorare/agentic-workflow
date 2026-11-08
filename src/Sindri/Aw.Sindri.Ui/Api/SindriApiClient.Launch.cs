using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The agent run's calls (<see cref="SindriApiClient"/>, continued): a launch, the Attach send and an instance's events.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<LaunchView>> Launch(Uri api, string launch, CancellationToken cancellationToken) =>
        Get<LaunchView>(api, "/sindri/launches/" + Uri.EscapeDataString(launch), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<OpenAcceptedView>> OpenItem(Uri api, string itemId, OpenItemRequest request, CancellationToken cancellationToken) =>
        Call<OpenAcceptedView>(api, HttpMethod.Post, "/sindri/items/" + Uri.EscapeDataString(itemId) + "/open", cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

}
