using Aw.Kernel.Services;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Features.DetachProject;
using Aw.Sindri.Features.HideProject;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The four project writes (projects page spec §5.3): POST /sindri/projects, /{projectId}/change, /{projectId}/detach and /hide. Each binds, calls one service and maps the result.</summary>
internal static class ProjectEndpoints
{
    internal static RouteGroupBuilder MapProjectWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/projects", static async (AttachProjectRequest request, IService<AttachProjectRequest, ProjectAccepted> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/projects/hide", static async (HideProjectRequest request, IService<HideProjectRequest, ProjectAccepted> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/projects/{projectId}/change", static async (string projectId, ChangeProjectBody body, IService<ChangeProjectRequest, ProjectAccepted> service, CancellationToken ct) =>
        {
            var project = ProjectRoute.Project(projectId);
            return project.IsError ? project.ToHttpResult() : (await service.Handle(body.ToRequest(project.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapPost("/projects/{projectId}/detach", static async (string projectId, DetachProjectBody body, IService<DetachProjectRequest, ProjectAccepted> service, CancellationToken ct) =>
        {
            var project = ProjectRoute.Project(projectId);
            return project.IsError ? project.ToHttpResult() : (await service.Handle(body.ToRequest(project.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
