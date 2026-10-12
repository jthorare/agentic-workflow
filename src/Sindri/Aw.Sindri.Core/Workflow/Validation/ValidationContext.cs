using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// Everything the validator checks a recipe against, as data (spec §7.1): the build's catalog, the profile's facts,
/// the child recipes, and a generated recipe's base. The validator does no I/O; the caller resolves all of this.
/// </summary>
/// <param name="Catalog">The registered blocks and events.</param>
/// <param name="Profile">The profile's facts; <see cref="RecipeProfileFacts.None"/> without a profile.</param>
/// <param name="Children">Finds child recipes.</param>
/// <param name="BaseRecipe">A generated recipe's base, which the CAP rules compare against; null for a hand-written one.</param>
public sealed record ValidationContext(
    BlockCatalog Catalog,
    RecipeProfileFacts Profile,
    IChildRecipeResolver Children,
    Recipe? BaseRecipe);
