using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/index/purge: what one record source holds, and with confirm its deletion. A human at a terminal only (the catalog row).</summary>
internal static class PurgeIndexSourceEndpoint
{
    internal static RouteGroupBuilder MapPurgeIndexSource(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/index/purge", static async (PurgeIndexSourceRequest request, IService<PurgeIndexSourceRequest, IndexPurgeView> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
