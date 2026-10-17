using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.SessionHeartbeat;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/sessions/{launchId}/heartbeat (API spec §8.1; issue): 'session wrap' says the session is alive; proven by the launch's own proof, never by the caller class.</summary>
internal static class SessionHeartbeatEndpoint
{
    internal static RouteGroupBuilder MapSessionHeartbeat(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sessions/{launchId}/heartbeat", static async (string launchId, SessionHeartbeatBody body, HttpRequest http,
                IService<SessionHeartbeatRequest, SessionHeartbeatResponse> service, CancellationToken ct) =>
        {
            var launch = LaunchRoute.Launch(launchId);
            return launch.IsError
                ? launch.ToHttpResult()
                : (await service.Handle(new SessionHeartbeatRequest(launch.Value, new LaunchSecret(http.Headers[SindriHeaders.LaunchSecret].ToString()), body.SessionId), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
