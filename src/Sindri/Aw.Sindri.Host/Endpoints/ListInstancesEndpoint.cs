using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListInstances;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances[?address=&amp;item=&amp;state=&amp;needsMe=&amp;recipe=&amp;urgency=&amp;subject=&amp;root=&amp;open=&amp;query=] (engine spec §6.3; query): the instances
/// by their search attributes, or with 'address' the one instance a CLI address or bare item names, with its opaque id.
/// </summary>
internal static class ListInstancesEndpoint
{
    internal static RouteGroupBuilder MapListInstances(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances", static async (string? address, string? item, string? state, string? needsMe, string? recipe, string? urgency, string? subject, string? root, string? query, string? open,
                IService<ListInstancesRequest, InstanceList> service, CancellationToken ct)
            => (await service.Handle(new ListInstancesRequest(needsMe, item is null ? null : ItemKey.From(item), state, query, address, recipe, urgency, subject, root, open), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
