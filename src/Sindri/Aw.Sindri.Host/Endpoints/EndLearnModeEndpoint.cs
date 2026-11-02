using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.EndLearnMode;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/sandbox/learn-off (sandbox spec §3.6; desk): body '{repo?}'; a launched session is refused by the authorization table.</summary>
internal static class EndLearnModeEndpoint
{
    internal static RouteGroupBuilder MapEndLearnMode(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sandbox/learn-off", static async (EndLearnModeRequest request, IService<EndLearnModeRequest, CommandAccepted> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
