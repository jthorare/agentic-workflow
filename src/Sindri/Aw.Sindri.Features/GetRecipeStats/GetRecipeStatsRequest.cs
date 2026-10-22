using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.GetRecipeStats;

/// <summary>'GET /workflow/recipes/{id}/stats?window=&amp;hash=' (API spec §3.8, E-24; query): a recipe's stats over the ledger's workflow events.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Window">'7d', '30d' or '90d'; null for 30d. Anything else is SND-API-013.</param>
/// <param name="Hash">A recipe hash, in full or a prefix of 8 or more digits, to count only the runs under that version; null for every version. A malformed one is SND-API-013.</param>
public sealed record GetRecipeStatsRequest(RecipeId Id, string? Window = null, string? Hash = null) : IQuery<RecipeStats>;
