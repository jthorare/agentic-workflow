using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListLinearProjects;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/projects/linear (projects page spec §3.2, §6.2; query): the From Linear tab's rows. The literal segment wins over /projects/{project}. A session caller's Linear text is inside the untrusted fence.</summary>
internal static class ListLinearProjectsEndpoint
{
    internal static RouteGroupBuilder MapListLinearProjects(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/projects/linear", static async Task<IResult> (IService<ListLinearProjectsRequest, ListLinearProjectsResponse> service, ICallerContextAccessor callers, IProjectFencing fencing, CancellationToken ct) =>
        {
            var rows = await service.Handle(new ListLinearProjectsRequest(), ct).ConfigureAwait(false);
            return rows.IsError || callers.Current?.Class != CallerClass.Session ? rows.ToHttpResult() : fencing.Choices(rows.Value);
        });
        return group;
    }
}
