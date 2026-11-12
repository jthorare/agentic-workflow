using Aw.Kernel.Services;
using Aw.Sindri.Features.RestartServe;

namespace Aw.Sindri.Host.Endpoints;

internal static class RestartServeEndpoint
{
    internal static RouteGroupBuilder MapRestartServe(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/restart", static async (IService<RestartServeRequest, RestartServeResponse> service, CancellationToken ct)
            => (await service.Handle(new RestartServeRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
