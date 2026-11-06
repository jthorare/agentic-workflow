using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Api;

/// <summary>The review calls (<see cref="SindriApiClient"/>, continued; review page spec §6).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ReviewList>> Reviews(Uri api, CancellationToken cancellationToken) =>
        Get<ReviewList>(api, ReviewRoutes.List, cancellationToken, view: true);
}
