using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The ranked search of the map over GET /sindri/context/search; falls back to another search when the route is not there.</summary>
internal sealed class ApiMapSearch : IMapSearch
{
    private const int MaxQuery = 512;
    private const int MinLimit = 1;
    private const int MaxLimit = 200;

    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly Func<string?> _repo;
    private readonly IMapSearch _fallback;
    private readonly Action<string> _problem;
    private readonly int _limit;

    /// <summary>Makes the search.</summary>
    /// <param name="api">Sindri's API.</param>
    /// <param name="apiUrl">The control plane's API address now, or null when none is known.</param>
    /// <param name="repo">The repo the page is on; null for the server's choice.</param>
    /// <param name="fallback">The search used when the route is unavailable (a 404 or no connection).</param>
    /// <param name="problem">Told the words of a refusal or a too long search, so the page can show them.</param>
    /// <param name="limit">How many hits to ask for; kept to 1 to 200.</param>
    internal ApiMapSearch(ISindriApi api, Func<Uri?> apiUrl, Func<string?> repo, IMapSearch fallback, Action<string> problem, int limit = 50)
    {
        (_api, _apiUrl, _repo, _fallback, _problem) = (api, apiUrl, repo, fallback, problem);
        _limit = Math.Clamp(limit, MinLimit, MaxLimit);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var words = query.Trim();
        if (words.Length == 0)
        {
            return [];   // an empty box is no search
        }

        if (words.Length > MaxQuery)
        {
            _problem(Labels.ContextSearchTooLong);
            return [];
        }

        if (_apiUrl() is not { } api)
        {
            return await _fallback.Search(words, cancellationToken).ConfigureAwait(false);
        }

        var answer = await _api.ContextSearch(api, new MapSearchQuery(words, _limit, Area: null, _repo()), cancellationToken).ConfigureAwait(false);
        if (answer.IsSuccess)
        {
            return [.. answer.Value.Hits.Select(h => new MapSearchHit(h.Key, h.Kind, h.Label, h.Area, h.Repo, h.Score, h.Matched, h.Snippet))];
        }

        if (IsRouteUnavailable(answer.Error.Code))
        {
            return await _fallback.Search(words, cancellationToken).ConfigureAwait(false);
        }

        _problem(answer.Error.Message);
        return [];
    }

    // A server without the route answers a 404 the app can't read (SND-UI-003); no server answers nothing (SND-UI-002).
    private static bool IsRouteUnavailable(string code) =>
        string.Equals(code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) || string.Equals(code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);
}
