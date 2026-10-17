using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.GetLaunchTargets;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/launch/targets (UI spec E-3, screens spec E-28): what this box can launch, and each target's budget.</summary>
internal static class GetLaunchTargetsEndpoint
{
    internal static RouteGroupBuilder MapGetLaunchTargets(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/launch/targets", static async (IService<GetLaunchTargetsRequest, LaunchTargets> service, CancellationToken ct) =>
            (await service.Handle(new GetLaunchTargetsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
