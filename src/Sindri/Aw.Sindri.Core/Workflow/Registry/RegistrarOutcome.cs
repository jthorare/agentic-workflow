using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What the recipe registrar registered and where it stands.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Added">False when the hash was already registered.</param>
/// <param name="Standing">Unapproved, approved or revoked.</param>
/// <param name="Sequence">The ledger sequence of 'recipe.registered' (the earlier one when it was already registered).</param>
public sealed record RegistrarOutcome(RecipeId Id, int Version, RecipeHash Hash, bool Added, RecipeStanding Standing, long Sequence);
