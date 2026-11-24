using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Home read (<see cref="ISindriApi"/>, continued; home spec §8).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/home?after=: a page of the rows that need the owner, each group in ranked order.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="after">The cursor of the page before, or null for the first page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<HomeView>> Home(Uri api, string? after, CancellationToken cancellationToken);
}
