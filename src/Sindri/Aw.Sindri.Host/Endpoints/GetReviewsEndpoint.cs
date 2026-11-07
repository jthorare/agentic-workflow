using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListReviews;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/reviews (PR review page spec §6.1; query): the Reviews list. A session caller's forge text (titles, authors, unreadable folders) is inside the untrusted fence; the human gets it plain.</summary>
internal static class GetReviewsEndpoint
{
    internal static RouteGroupBuilder MapGetReviews(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/reviews", static async Task<IResult> (IService<ListReviewsRequest, ListReviewsResponse> service, ICallerContextAccessor callers, IReviewFencing fencing, CancellationToken ct) =>
        {
            var list = await service.Handle(new ListReviewsRequest(), ct).ConfigureAwait(false);
            return list.IsError || callers.Current?.Class != CallerClass.Session ? list.ToHttpResult() : fencing.List(list.Value);
        });
        return group;
    }
}
