using Aw.Kernel.Services;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.GetLinearSince;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/linear/since[?after=] (Linear hub spec 2026-11-04 §3; query): what changed on the owner's issues since a marker. Owner only: a session is SND-AUTH-002 (403); a marker this route never gave is SND-API-013 (400).</summary>
internal static class GetLinearSinceEndpoint
{
    internal static RouteGroupBuilder MapGetLinearSince(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/linear/since", static async (string? after, IService<GetLinearSinceRequest, LinearSinceResponse> service, CancellationToken ct)
            => (await service.Handle(new GetLinearSinceRequest(after), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
