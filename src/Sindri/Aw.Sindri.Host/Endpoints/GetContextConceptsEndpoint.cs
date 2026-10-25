using Aw.Context.Core.Concepts;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/concepts?repo=&amp;cursor=&amp;limit=&amp;q=&amp;draft=&amp;mapVersion=: one page of the product's concepts, the top band of the map.</summary>
internal static class GetContextConceptsEndpoint
{
    internal static RouteGroupBuilder MapGetContextConcepts(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/concepts", static async (string? repo, string? cursor, int? limit, string? q, bool? draft, string? mapVersion, IService<GetContextConceptsRequest, ConceptsView> service, CancellationToken ct)
            => (await service.Handle(new GetContextConceptsRequest(repo, cursor, limit, q, draft ?? false, mapVersion), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
