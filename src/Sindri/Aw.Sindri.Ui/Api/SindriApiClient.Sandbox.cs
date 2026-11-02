using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Api;

/// <summary>The sandbox's reads and write (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SandboxStatusView>> SandboxStatus(Uri api, CancellationToken cancellationToken) =>
        Get<SandboxStatusView>(api, "/sindri/sandbox/status", cancellationToken);

    /// <inheritdoc />
    public Task<Result<SandboxCheckView>> SandboxCheck(Uri api, CancellationToken cancellationToken) =>
        Get<SandboxCheckView>(api, "/sindri/sandbox/check", cancellationToken);

    /// <inheritdoc />
    public Task<Result<DenialsView>> SandboxDenials(Uri api, string item, CancellationToken cancellationToken) =>
        Get<DenialsView>(api, $"/sindri/sandbox/denials?item={Uri.EscapeDataString(item)}", cancellationToken);

    /// <inheritdoc />
    public Task<Result<AllowView>> SandboxAllow(Uri api, AllowRequest request, CancellationToken cancellationToken) =>
        Call<AllowView>(api, HttpMethod.Post, "/sindri/sandbox/allow", cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));
}
