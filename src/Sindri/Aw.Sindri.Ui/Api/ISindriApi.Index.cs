using Aw.Kernel;
using Aw.Sindri.Ui.Api.Index;

namespace Aw.Sindri.Ui.Api;

/// <summary>The context index's read (<see cref="ISindriApi"/>, continued).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/index/status: each context source's pull state for the Health card. A query; it never carries X-Sindri-After.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sources, or the refusal.</returns>
    public Task<Result<IndexStatusView>> IndexStatus(Uri api, CancellationToken cancellationToken);
}
