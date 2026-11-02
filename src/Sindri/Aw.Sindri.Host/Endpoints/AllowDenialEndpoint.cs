using Aw.Kernel.Services;
using Aw.Sindri.Features.AllowDenial;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/sandbox/allow (sandbox spec §3.6; desk): body '{launch, kind, target}'; a launched session is refused by the authorization table. Edits the live profile; the owner approves it.</summary>
internal static class AllowDenialEndpoint
{
    internal static RouteGroupBuilder MapAllowDenial(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sandbox/allow", static async (AllowDenialRequest request, IService<AllowDenialRequest, AllowDenialResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
