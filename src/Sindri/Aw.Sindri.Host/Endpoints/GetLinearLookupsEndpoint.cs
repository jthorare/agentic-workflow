using Aw.Kernel.Services;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.GetLinearLookups;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/linear/lookups/{team} (Linear hub spec 2026-11-04 §3; query): a team's states, members, labels and cycles. Owner only: a session is SND-AUTH-002 (403). A team never synced answers 200 with empty lists.</summary>
internal static class GetLinearLookupsEndpoint
{
    internal static RouteGroupBuilder MapGetLinearLookups(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/linear/lookups/{team}", static async (string team, IService<GetLinearLookupsRequest, LinearLookupsResponse> service, CancellationToken ct)
            => (await service.Handle(new GetLinearLookupsRequest(team), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
