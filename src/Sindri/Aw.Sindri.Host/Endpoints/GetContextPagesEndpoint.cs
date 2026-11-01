using Aw.Context.Core.Pages;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/pages?repo=&amp;concept=&amp;page=&amp;marks=&amp;suggested=&amp;limit=&amp;offset=&amp;draft=&amp;mapVersion=&amp;q=: the product's pages with their actions and concepts. 'marks' is all (the default), owner or suggested; 'suggested=true' is marks=suggested; 'q' keeps the pages whose name or map label contains it.</summary>
internal static class GetContextPagesEndpoint
{
    internal static RouteGroupBuilder MapGetContextPages(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/pages", static async (string? repo, string? concept, string? page, string? marks, bool? suggested, int? limit, int? offset, bool? draft, string? mapVersion, string? q, IService<GetContextPagesRequest, PagesView> service, CancellationToken ct) =>
        {
            var chosen = suggested == true ? PageMarks.Suggested : Enum.TryParse<PageMarks>(marks, ignoreCase: true, out var parsed) ? parsed : PageMarks.All;
            return (await service.Handle(new GetContextPagesRequest(repo, concept, page, chosen, limit, offset, draft ?? false, mapVersion) { Q = q }, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
