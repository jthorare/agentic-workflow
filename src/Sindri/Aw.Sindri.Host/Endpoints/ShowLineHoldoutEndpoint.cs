using Aw.Kernel.Services;
using Aw.Sindri.Features.LineHoldout;

namespace Aw.Sindri.Host.Endpoints;

internal static class ShowLineHoldoutEndpoint
{
    internal static RouteGroupBuilder MapShowLineHoldout(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/line/holdout/show", static async (ShowLineHoldoutRequest request, IService<ShowLineHoldoutRequest, ShowLineHoldoutResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
