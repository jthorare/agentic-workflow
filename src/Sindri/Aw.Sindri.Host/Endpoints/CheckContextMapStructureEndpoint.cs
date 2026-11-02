using Aw.Context.Core.Map;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

internal static class CheckContextMapStructureEndpoint
{
    internal static RouteGroupBuilder MapCheckContextMapStructure(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/context/map/checks", static async (CheckContextMapStructureRequest request, IService<CheckContextMapStructureRequest, MapChecksView> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
