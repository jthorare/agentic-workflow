using Aw.Kernel.Services;
using Aw.Sindri.Features.CreateHandoffItem;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/handoff (#2258 PR 12): a local app or script hands Sindri work; loopback only, per-start token, 64 KB cap, rate limit.</summary>
internal static class HandoffEndpoint
{
    internal static RouteGroupBuilder MapHandoff(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/handoff", static async (CreateHandoffItemRequest request, IHandoffPeer peer, IService<CreateHandoffItemRequest, HandoffItemCreatedResponse> service, CancellationToken ct) =>
        {
            _ = peer;   // TODO(#2258 PR 12): the guards come with the green commit
            return (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
