using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The profile route (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ProfileRead>> ProfileSummary(Uri api, CancellationToken cancellationToken) => Get<ProfileRead>(api, "/sindri/profile", cancellationToken);
}
