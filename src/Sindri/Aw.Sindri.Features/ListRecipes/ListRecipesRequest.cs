using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>List the registered recipes (engine spec §6.3 'recipe list [--stale]', GET /workflow/recipes[?window=]; query).</summary>
/// <param name="Stale">True for only the approved recipes whose pins no longer match the build, whose pinned child is stale, or that are legacy-shape.</param>
/// <param name="Window">The window of each line's stats (API spec §3.8, E-25): '7d', '30d' or '90d'; null for 30d. Anything else is SND-API-013.</param>
public sealed record ListRecipesRequest(bool Stale, string? Window = null) : IQuery<ListRecipesResponse>;
