using Aw.Kernel.Services;
using Aw.Sindri.Features.OpenItem;

namespace Aw.Sindri.Host.Endpoints;

internal static class OpenItemEndpoint
{
    internal static RouteGroupBuilder MapOpenItem(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/items/{itemId}/open", static async (string itemId, OpenItemBody body, IService<OpenItemRequest, LaunchRequested> service, CancellationToken ct) =>
        {
            var item = ItemRoute.Item(itemId);
            return item.IsError ? item.ToHttpResult() : (await service.Handle(body.ToRequest(item.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
