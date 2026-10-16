using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.Decide;

namespace Aw.Sindri.Host.Endpoints;

internal static class DecideEndpoint
{
    internal static RouteGroupBuilder MapDecide(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/decisions/{decisionId}", static async (string decisionId, DecideBody body, IService<DecideRequest, CommandAccepted> service, CancellationToken ct) =>
        {
            var decision = RequestRoute.Decision(decisionId);
            return decision.IsError ? decision.ToHttpResult() : (await service.Handle(body.ToRequest(decision.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
