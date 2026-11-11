using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'recipe.revoked' payload (spec §4.1), keyed 'recipe.revoked:&lt;hash&gt;': one per revoked hash, cascaded ones included.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The revoked hash.</param>
/// <param name="CascadedFrom">The base whose revoke cascaded to it, or null.</param>
public sealed record RecipeRevoked(RecipeId Id, int Version, RecipeHash Hash, RecipeHash? CascadedFrom);
