using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'recipe.registered' payload (spec §4.1), keyed 'recipe.registered:&lt;hash&gt;'.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
public sealed record RecipeRegistered(RecipeId Id, int Version, RecipeHash Hash);
