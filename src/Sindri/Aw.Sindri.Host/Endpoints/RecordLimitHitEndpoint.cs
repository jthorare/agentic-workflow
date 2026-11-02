using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.RecordLimitHit;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/launches/{launchId}/limit-hit (sandbox spec §3.3; issue): 'session wrap' says a limit was reached that does not end the session; proven by the launch's own proof.</summary>
internal static class RecordLimitHitEndpoint
{
    internal static RouteGroupBuilder MapRecordLimitHit(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/launches/{launchId}/limit-hit", static async (string launchId, LimitHitBody body, HttpRequest http,
                IService<RecordLimitHitRequest, RecordLimitHitResponse> service, CancellationToken ct) =>
        {
            var launch = LaunchRoute.Launch(launchId);
            return launch.IsError
                ? launch.ToHttpResult()
                : (await service.Handle(new RecordLimitHitRequest(launch.Value, new LaunchSecret(http.Headers[SindriHeaders.LaunchSecret].ToString()), body.Kind, body.Peak), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
