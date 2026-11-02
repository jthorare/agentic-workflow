using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListProjects;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/projects[?all=true] (projects page spec §6.2; query): the Projects page. A session caller's Linear text is inside the untrusted fence; the human gets it plain.</summary>
internal static class GetProjectsEndpoint
{
    internal static RouteGroupBuilder MapGetProjects(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/projects", static async Task<IResult> (bool? all, IService<ListProjectsRequest, ListProjectsResponse> service, ICallerContextAccessor callers, IProjectFencing fencing, CancellationToken ct) =>
        {
            var page = await service.Handle(new ListProjectsRequest(all == true), ct).ConfigureAwait(false);
            return page.IsError || callers.Current?.Class != CallerClass.Session ? page.ToHttpResult() : fencing.Page(page.Value);
        });
        return group;
    }
}
