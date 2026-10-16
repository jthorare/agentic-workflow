using Aw.Kernel.Services;
using Aw.Sindri.Features.ListRequests;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListRequestsEndpoint
{
    internal static RouteGroupBuilder MapListRequests(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/requests", static async (string? state, string? item, IService<ListRequestsRequest, ListRequestsResponse> service, CancellationToken ct) =>
        {
            var filter = RequestRoute.State(state).Bind(s => RequestRoute.Item(item).Map(i => new ListRequestsRequest(s, i)));
            return filter.IsError ? filter.ToHttpResult() : (await service.Handle(filter.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
