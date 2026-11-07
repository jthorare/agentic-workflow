using Aw.Kernel.Services;
using Aw.Sindri.Features.SendReviewFix;

namespace Aw.Sindri.Host.Endpoints;

internal static class SendReviewFixEndpoint
{
    internal static RouteGroupBuilder MapSendReviewFix(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/reviews/{owner}/{repo}/{number:int}/fix", static async (string owner, string repo, int number, ReviewFixBody body, IService<SendReviewFixRequest, SendReviewFixResponse> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(owner, repo, number), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
