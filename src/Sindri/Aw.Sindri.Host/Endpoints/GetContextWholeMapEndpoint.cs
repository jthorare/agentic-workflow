using Aw.Context.Core.WholeMap;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/map/whole?detail=&amp;area=&amp;concept=&amp;locate=&amp;draft=&amp;mapVersion=&amp;repo=: the whole map, as nodes when it fits the node budget and as areas when it doesn't.</summary>
internal static class GetContextWholeMapEndpoint
{
    internal static RouteGroupBuilder MapGetContextWholeMap(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/map/whole", static async (string? detail, string? area, string? concept, string[]? locate, bool? draft, string? mapVersion, string? repo, IService<GetContextWholeMapRequest, WholeMapView> service, CancellationToken ct)
            => (await service.Handle(new GetContextWholeMapRequest(detail, locate ?? [], draft ?? false, mapVersion, repo, area, concept), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
