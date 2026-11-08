using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.CompleteStep;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete (engine spec rev 8.1 §5.3, §6.3; desk): the report for
/// an open desk run. The body is the report; the route names the instance and the run and wins over the body's.
/// </summary>
internal static class CompleteStepEndpoint
{
    internal static RouteGroupBuilder MapCompleteStep(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/instances/{instanceId}/runs/{run}/complete", static async (string instanceId, string run, CompleteStepRequest request, HttpRequest http,
                IService<CompleteStepRequest, CompleteStepResponse> service, CancellationToken ct)
            => (await service.Handle(request with { Instance = InstanceRoute.Id(instanceId), Run = RunToken.From(run), LaunchSecret = Secret(http) }, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    /// <summary>The launch secret the session's call carries (API spec §8.1); null when the header is missing or empty (a desk report).</summary>
    private static LaunchSecret? Secret(HttpRequest http) =>
        http.Headers[SindriHeaders.LaunchSecret].ToString() is { Length: > 0 } value ? new LaunchSecret(value) : null;
}
