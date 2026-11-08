using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The takeover command (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ApplyCommandAccepted>> ApplyCommand(Uri api, string instanceId, ApplyCommandBody body, CancellationToken cancellationToken) =>
        Call<ApplyCommandAccepted>(api, HttpMethod.Post, "/sindri/workflow/instances/" + Uri.EscapeDataString(instanceId) + "/commands", cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));
}
