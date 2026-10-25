using Aw.Context.Core.Search;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/context/search?q=&amp;limit=&amp;area=&amp;repo=&amp;draft=&amp;mapVersion= and QUERY /sindri/context/search with the same fields as the JSON body: ranked full-text search over the map,
/// one question, two transports, one service and one session filter. QUERY is the HTTP method for a safe read whose question is too large or too awkward for a URL
/// (draft-ietf-httpbis-safe-method-w-body); a POST to the route is a 405. It is a query like the GET (ARCH-CQRS-001): it takes no idempotency key, writes no ledger event and refuses an Origin header as a command does.
/// </summary>
internal static class SearchContextMapEndpoint
{
    internal static RouteGroupBuilder MapSearchContextMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/search", static async (string? q, int? limit, string? area, string? repo, bool? draft, string? mapVersion, IService<SearchContextMapRequest, MapSearchView> search, CancellationToken ct) =>
            (await search.Handle(new SearchContextMapRequest(q, limit, area, repo, draft ?? false, mapVersion), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGroup(string.Empty).WithMetadata(BodyQueryRouteMetadata.Instance).MapMethods("/context/search", [HttpMethods.Query], static async (SearchContextMapRequest request, IService<SearchContextMapRequest, MapSearchView> search, CancellationToken ct) =>
            (await search.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
