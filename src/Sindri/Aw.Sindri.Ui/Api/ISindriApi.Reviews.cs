using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Api;

/// <summary>The review reads of the API (<see cref="ISindriApi"/>, continued; review page spec §6).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/reviews (review page spec §6.1): the owner's PRs and the ones waiting on the owner, the library header and the waiting count. A view read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list (the login prompt while review.login is unset), the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ReviewList>> Reviews(Uri api, CancellationToken cancellationToken);
}
