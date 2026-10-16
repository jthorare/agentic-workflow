using Aw.Kernel.Services;
using Aw.Sindri.Features.GetNeedsYou;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetNeedsYouEndpoint
{
    internal static RouteGroupBuilder MapGetNeedsYou(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/needs-you", static async (IService<GetNeedsYouRequest, GetNeedsYouResponse> service, CancellationToken ct)
            => (await service.Handle(new GetNeedsYouRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
