using Aw.Kernel.Services;
using Aw.Sindri.Features.ListRecipes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/workflow/recipes[?stale=true&amp;window=7d|30d|90d] (engine spec §6.3; query): every registered recipe with its inputs (README X34), or the stale ones, each with its status word and its stats at the window (API spec §3.8, E-25; SND-API-013 for a window outside the three).</summary>
internal static class ListRecipesEndpoint
{
    internal static RouteGroupBuilder MapListRecipes(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes", static async (bool? stale, string? window, IService<ListRecipesRequest, ListRecipesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListRecipesRequest(stale ?? false, window), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
