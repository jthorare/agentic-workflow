using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/pins?repo=: the live profile's pins against the approved profile and the map, and the map's rebuild state (the Pin sheet's read).</summary>
internal static class GetContextPinsEndpoint
{
    internal static RouteGroupBuilder MapGetContextPins(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/pins", static async (string? repo, IService<GetContextPinsRequest, ContextPinsView> service, CancellationToken ct)
            => (await service.Handle(new GetContextPinsRequest(repo), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
