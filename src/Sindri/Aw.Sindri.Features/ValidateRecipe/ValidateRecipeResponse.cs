using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>A validation's findings and, when the recipe bound, its hash ('recipe validate' prints the lines, then the hash).</summary>
/// <param name="Findings">Every finding: the reader's, then each rule group's.</param>
/// <param name="Hash">The hash of the normalized long form; null when the recipe didn't bind.</param>
public sealed record ValidateRecipeResponse(IReadOnlyList<RecipeFinding> Findings, RecipeHash? Hash)
{
    /// <summary>The normalized long form the reader bound (what <see cref="Hash"/> covers); null when the recipe didn't bind.</summary>
    public Recipe? Normalized { get; init; }

    /// <summary>Whether the recipe passes: it bound and no finding is an error (exit 0; otherwise 1).</summary>
    public bool Passed => Hash is not null && RecipeValidator.Passes(Findings);
}
