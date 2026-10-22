using Aw.Sindri.Features.AddRepo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/setup/repos (setup-once spec §5.1, §7.2): add a repo from the app. The body is '{path, name?}'; the cheap checks answer 400 or 409, then 200 with the job, like every job route.</summary>
internal static class AddRepoEndpoint
{
    internal static RouteGroupBuilder MapAddRepo(this RouteGroupBuilder group) => group.MapJob<AddRepoRequest, AddedRepo>("/setup/repos");
}
