using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Api;

/// <summary>The sandbox's reads and write (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SandboxStatusView>> SandboxStatus(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<SandboxCheckView>> SandboxCheck(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<DenialsView>> SandboxDenials(Uri api, string item, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<AllowView>> SandboxAllow(Uri api, AllowRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
