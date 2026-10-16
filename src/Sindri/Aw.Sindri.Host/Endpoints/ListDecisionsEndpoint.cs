using Aw.Kernel.Services;
using Aw.Sindri.Features.ListDecisions;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListDecisionsEndpoint
{
    internal static RouteGroupBuilder MapListDecisions(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/decisions", static async (string? pending, string? item, IService<ListDecisionsRequest, ListDecisionsResponse> service, CancellationToken ct) =>
        {
            var filter = RequestRoute.Pending(pending).Bind(p => RequestRoute.Item(item).Map(i => new ListDecisionsRequest(p, i)));
            return filter.IsError ? filter.ToHttpResult() : (await service.Handle(filter.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
