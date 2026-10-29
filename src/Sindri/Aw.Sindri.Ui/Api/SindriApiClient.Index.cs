using Aw.Kernel;
using Aw.Sindri.Ui.Api.Index;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context index's read (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<IndexStatusView>> IndexStatus(Uri api, CancellationToken cancellationToken) =>
        Get<IndexStatusView>(api, "/sindri/index/status", cancellationToken);
}
