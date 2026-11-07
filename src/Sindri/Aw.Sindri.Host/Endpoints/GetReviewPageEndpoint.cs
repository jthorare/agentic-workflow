using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.GetReviewPage;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/reviews/{owner}/{repo}/{number} (PR review page spec §6.2; query): one PR's page model. A session caller's forge text (title, branch, patches, threads) is inside the untrusted fence; the human gets it plain.</summary>
internal static class GetReviewPageEndpoint
{
    internal static RouteGroupBuilder MapGetReviewPage(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/reviews/{owner}/{repo}/{number}", static async Task<IResult> (string owner, string repo, string number, IService<GetReviewPageRequest, ReviewPageModel> service, ICallerContextAccessor callers, IReviewFencing fencing, CancellationToken ct) =>
        {
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                return HttpResults.Problem(SindriErrors.InvalidInput.With("not a GitHub pull request number: " + number));
            }

            var page = await service.Handle(new GetReviewPageRequest(owner, repo, n), ct).ConfigureAwait(false);
            return page.IsError || callers.Current?.Class != CallerClass.Session ? page.ToHttpResult() : fencing.Page(page.Value);
        });
        return group;
    }
}
