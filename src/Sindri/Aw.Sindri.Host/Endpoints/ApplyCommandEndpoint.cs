using Aw.Kernel.Services;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/instances/{instanceId}/commands (engine spec rev 8 §5.2, §6.3; classed by variant, §6.2): an
/// answer, a recipe command or a cancel. The body is the command; the route names the instance and wins over the body's.
/// </summary>
internal static class ApplyCommandEndpoint
{
    internal static RouteGroupBuilder MapApplyCommand(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/instances/{instanceId}/commands", static async (string instanceId, ApplyCommandBody body,
                IService<ApplyCommandRequest, ApplyCommandResponse> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(instanceId), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
