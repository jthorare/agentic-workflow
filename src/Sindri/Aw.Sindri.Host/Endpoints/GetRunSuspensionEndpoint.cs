using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.GetRunSuspension;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/launches/{launchId}/suspended (sandbox spec §7, SB16): whether the launch's run is parked, escalated or taken over; proven by the launch secret, as the session heartbeat is.</summary>
internal static class GetRunSuspensionEndpoint
{
    internal static RouteGroupBuilder MapGetRunSuspension(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/launches/{launchId}/suspended", static async (string launchId, HttpRequest http, IService<GetRunSuspensionRequest, RunSuspensionView> service, CancellationToken ct) =>
        {
            var launch = LaunchRoute.Launch(launchId);
            return launch.IsError
                ? launch.ToHttpResult()
                : (await service.Handle(new GetRunSuspensionRequest(launch.Value, new LaunchSecret(http.Headers[SindriHeaders.LaunchSecret].ToString())), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
