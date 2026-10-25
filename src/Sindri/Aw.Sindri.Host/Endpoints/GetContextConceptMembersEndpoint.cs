using Aw.Context.Core.Concepts;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/concepts/{id}/members?cursor=&amp;limit=&amp;repo=&amp;draft=&amp;mapVersion=: one page of one concept's members; the id is a concept key ('concept:scheduling', URL-encoded) or 'concept:other'.</summary>
internal static class GetContextConceptMembersEndpoint
{
    internal static RouteGroupBuilder MapGetContextConceptMembers(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/concepts/{id}/members", static async (string id, string? cursor, int? limit, string? repo, bool? draft, string? mapVersion, IService<GetContextConceptMembersRequest, ConceptMembersView> service, CancellationToken ct)
            => (await service.Handle(new GetContextConceptMembersRequest(id, cursor, limit, repo, draft ?? false, mapVersion), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
