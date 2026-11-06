using Aw.Kernel.Services;
using Aw.Sindri.Features.ListReviews;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/reviews (PR review page spec §6.1; query): the Reviews list.</summary>
internal static class GetReviewsEndpoint
{
    internal static RouteGroupBuilder MapGetReviews(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/reviews", static async Task<IResult> (IService<ListReviewsRequest, ListReviewsResponse> service, CancellationToken ct) =>
            (await service.Handle(new ListReviewsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
