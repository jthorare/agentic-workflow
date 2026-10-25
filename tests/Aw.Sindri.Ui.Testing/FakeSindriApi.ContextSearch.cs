using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The ranked map search of the fake: not served (a 404) until a test serves it, so every other Context test sees today's drawn search.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<MapSearchQuery> _searchAsked = [];

    /// <summary>GET /sindri/context/search's answer per question (a 404 by default: a server without the route).</summary>
    public Func<MapSearchQuery, RouteAnswer> ContextSearchAnswer { get; set; } = _ => _notServed;

    /// <summary>The searches asked, in order.</summary>
    public IReadOnlyList<MapSearchQuery> ContextSearchAsked { get { lock (_gate) { return [.. _searchAsked]; } } }

    public Task<Result<MapSearchView>> ContextSearch(Uri api, MapSearchQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _searchAsked.Add(query);
        }

        return Task.FromResult(NotReceived<MapSearchView>(api) ?? Answer<MapSearchView>(api, "/sindri/context/search", ContextSearchAnswer(query)));
    }
}
