using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;
using Aw.Sindri.Features.GetRecipeStats;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/recipes/{id}/stats[?window=7d|30d|90d&amp;hash=] (API spec §3.8, E-24; query): the recipe's stats over the ledger's workflow
/// events for the window (30d by default), optionally scoped to the runs under one version. A window or hash outside its set is SND-API-013.
/// </summary>
internal static class GetRecipeStatsEndpoint
{
    internal static RouteGroupBuilder MapGetRecipeStats(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes/{id}/stats", static async (string id, string? window, string? hash, IService<GetRecipeStatsRequest, RecipeStats> service, CancellationToken ct)
            => (await service.Handle(new GetRecipeStatsRequest(RecipeId.From(id), window, hash), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
