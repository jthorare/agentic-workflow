using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.GetInstanceFact;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/facts/{key} (API spec §3.8; query): the scrubbed body behind one fact row, capped at
/// 256 KiB. The route takes the instance's opaque id ('WFI-…') and the fact's key in its 'fact:' form, URL-escaped; an undeclared
/// key answers SND-ITEM-404.
/// </summary>
internal static class GetInstanceFactEndpoint
{
    internal static RouteGroupBuilder MapGetInstanceFact(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/facts/{key}", static async (string instanceId, string key, IService<GetInstanceFactRequest, FactView> service, CancellationToken ct)
            => (await service.Handle(new GetInstanceFactRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId)), key), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
