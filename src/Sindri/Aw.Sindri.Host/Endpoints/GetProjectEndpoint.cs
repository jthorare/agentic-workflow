using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.GetProject;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/projects/{project} (projects page spec §5.3, §6.2; query): one project's card, named by a prj_ id, a Linear key or a Linear url. A session caller's Linear text is inside the untrusted fence.</summary>
internal static class GetProjectEndpoint
{
    internal static RouteGroupBuilder MapGetProject(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/projects/{project}", static async Task<IResult> (string project, IService<GetProjectRequest, GetProjectResponse> service, ICallerContextAccessor callers, IProjectFencing fencing, CancellationToken ct) =>
        {
            var answer = await service.Handle(new GetProjectRequest(ProjectRoute.Segment(project)), ct).ConfigureAwait(false);
            return answer.IsError || callers.Current?.Class != CallerClass.Session ? answer.ToHttpResult() : fencing.One(answer.Value);
        });
        return group;
    }
}
