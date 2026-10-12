namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// One group of validator rules (spec §9.1): a pure function from a bound recipe and its context to findings. The
/// validator runs the groups in a fixed order; each group reports every finding it has, not just the first.
/// </summary>
internal interface IRecipeRules
{
    /// <summary>Checks <paramref name="document"/>.</summary>
    /// <param name="document">The bound recipe.</param>
    /// <param name="context">What it is checked against.</param>
    /// <returns>The findings, in the order the group finds them.</returns>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context);
}
