using Aw.Context.Core.Results;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/query?q=&amp;node=&amp;item=&amp;symbol=&amp;depth=&amp;budget=&amp;draft=&amp;diff=base..head&amp;repo=&amp;mapVersion=&amp;hops=&amp;concept= (context spec §8.5): keys travel in the query string, never the path.</summary>
internal static class QueryContextEndpoint
{
    internal static RouteGroupBuilder MapQueryContext(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/query", static async (IService<QueryContextRequest, ContextResult> service, string? q, string? node, string? item,
                string? symbol, string? depth, string? budget, bool? draft, string? diff, string? repo, string? mapVersion, string? hops, string? concept, CancellationToken ct)
            => (await service.Handle(new QueryContextRequest(q, node, item, symbol, depth, budget, draft ?? false, diff, repo, mapVersion, hops) { Concept = concept }, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
