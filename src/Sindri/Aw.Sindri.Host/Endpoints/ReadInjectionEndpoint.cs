using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.ReadInjection;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/injection[?section=S] (API spec §3.8; query): the scrubbed text behind the open run's
/// injection plan. It follows the fact route's read rules; no open run is SND-WF-005, and a refill that differs from the record is SND-WF-017.
/// </summary>
internal static class ReadInjectionEndpoint
{
    internal static RouteGroupBuilder MapReadInjection(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/injection", static async (string instanceId, string? section, IService<ReadInjectionRequest, InjectionView> service, CancellationToken ct)
            => (await service.Handle(new ReadInjectionRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId)), section), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
