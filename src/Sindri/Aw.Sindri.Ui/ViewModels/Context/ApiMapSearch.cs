using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The ranked search of the map over GET /sindri/context/search; falls back to another search when the route is not there.</summary>
internal sealed class ApiMapSearch : IMapSearch
{
    /// <summary>Makes the search.</summary>
    /// <param name="api">Sindri's API.</param>
    /// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
    /// <param name="repo">The repo the page is on; null for the server's choice.</param>
    /// <param name="fallback">The search used when the route is unavailable (a 404 or no connection).</param>
    /// <param name="problem">Told the words of a refusal or a too long search, so the page can show them.</param>
    /// <param name="limit">How many hits to ask for; kept to 1 to 200.</param>
    internal ApiMapSearch(ISindriApi api, Func<Uri?> apiUrl, Func<string?> repo, IMapSearch fallback, Action<string> problem, int limit = 50)
    {
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) => throw new NotSupportedException();
}
