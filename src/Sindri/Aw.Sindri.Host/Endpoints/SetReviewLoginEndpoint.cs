using Aw.Kernel.Services;
using Aw.Sindri.Features.SetReviewLogin;

namespace Aw.Sindri.Host.Endpoints;

internal static class SetReviewLoginEndpoint
{
    internal static RouteGroupBuilder MapSetReviewLogin(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/reviews/login", static async (SetReviewLoginRequest request, IService<SetReviewLoginRequest, SetReviewLoginResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
