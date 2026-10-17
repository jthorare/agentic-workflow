using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.SessionExit;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/sessions/{launchId}/exit (API spec §8.1; issue): 'session wrap' says the agent exited with a code; proven by the launch's own proof.</summary>
internal static class SessionExitEndpoint
{
    internal static RouteGroupBuilder MapSessionExit(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sessions/{launchId}/exit", static async (string launchId, SessionExitBody body, HttpRequest http,
                IService<SessionExitRequest, SessionExitResponse> service, CancellationToken ct) =>
        {
            var launch = LaunchRoute.Launch(launchId);
            return launch.IsError
                ? launch.ToHttpResult()
                : (await service.Handle(new SessionExitRequest(launch.Value, new LaunchSecret(http.Headers[SindriHeaders.LaunchSecret].ToString()), body.ExitCode, body.SessionId), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
