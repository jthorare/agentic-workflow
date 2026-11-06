using Aw.Kernel.Services;
using Aw.Sindri.Features.StartSkillRun;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/skills/{id}/runs (skill flows spec §7; desk): one skill block as a trial run. The body names the item, the inputs and, for the author loop, the skills folder; a session Sindri launched is refused.</summary>
internal static class StartSkillRunEndpoint
{
    internal static RouteGroupBuilder MapStartSkillRun(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/skills/{id}/runs", static async (string id, StartSkillRunBody body, IService<StartSkillRunRequest, StartSkillRunResponse> service, CancellationToken ct)
            => (await service.Handle(new StartSkillRunRequest(id, body.Item, body.Inputs ?? new Dictionary<string, string>(StringComparer.Ordinal), body.Skills), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
