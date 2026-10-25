using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (<see cref="ISindriApi"/>, continued): the ranked search over the map.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/search?q=&amp;limit=&amp;area=&amp;repo= (context search spec): the map's items that match some words, best first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The words and the narrowing.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The hits, the API's refusal (SND-CONTEXT-006 for a bad q or limit), or SND-UI-002/003.</returns>
    public Task<Result<MapSearchView>> ContextSearch(Uri api, MapSearchQuery query, CancellationToken cancellationToken);
}
