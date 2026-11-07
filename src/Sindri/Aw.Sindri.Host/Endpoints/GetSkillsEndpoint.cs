using Aw.Kernel.Services;
using Aw.Sindri.Features.GetSkills;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/skills (skill flows spec §7): the loaded skill blocks, the path each came from and the reason for any that didn't load.</summary>
internal static class GetSkillsEndpoint
{
    internal static RouteGroupBuilder MapGetSkills(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/skills", static async (IService<GetSkillsRequest, GetSkillsResponse> service, CancellationToken ct) =>
            (await service.Handle(new GetSkillsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
