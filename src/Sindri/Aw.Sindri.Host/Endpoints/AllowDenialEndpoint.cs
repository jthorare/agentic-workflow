using Aw.Kernel.Services;
using Aw.Sindri.Features.AllowDenial;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/sandbox/allow (sandbox spec §3.6; desk): body '{launch, kind, target}'; any caller may call it, a launched session included. Edits the live profile, records 'sandbox.allowed' with the caller's actor, and the change takes effect once the profile is approved.</summary>
internal static class AllowDenialEndpoint
{
    internal static RouteGroupBuilder MapAllowDenial(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sandbox/allow", static async (AllowDenialRequest request, IService<AllowDenialRequest, AllowDenialResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
