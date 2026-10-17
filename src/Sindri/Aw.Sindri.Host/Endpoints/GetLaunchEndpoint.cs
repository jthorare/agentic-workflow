using Aw.Kernel.Services;
using Aw.Sindri.Features.GetLaunch;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetLaunchEndpoint
{
    internal static RouteGroupBuilder MapGetLaunch(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/launches/{launchId}", static async (string launchId, IService<GetLaunchRequest, LaunchView> service, CancellationToken ct) =>
        {
            var launch = LaunchRoute.Launch(launchId);
            return launch.IsError ? launch.ToHttpResult() : (await service.Handle(new GetLaunchRequest(launch.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
