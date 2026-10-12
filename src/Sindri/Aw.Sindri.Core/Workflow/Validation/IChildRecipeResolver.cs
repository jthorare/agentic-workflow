using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// Finds the child recipes a recipe names (spec §9.1): from the registered recipes when Sindri is served, or from
/// the same directory when not (so workflow.item.yaml finds workflow.step.yaml). Synchronous, so the validator stays
/// a pure function over what it is given; the CHILD rules follow children through it (cycles, depth).
/// </summary>
public interface IChildRecipeResolver
{
    /// <summary>The recipe <paramref name="id"/>.</summary>
    /// <param name="id">A child recipe's id.</param>
    /// <returns>The recipe, or null when there is none (RCP-CHILD-001).</returns>
    public ChildRecipe? Resolve(RecipeId id);
}
