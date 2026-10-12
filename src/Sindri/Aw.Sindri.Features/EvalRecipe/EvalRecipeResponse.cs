namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>A recipe's eval results; no cases is the 'no evals declared' empty state.</summary>
/// <param name="Cases">Each case's result, in the recipe's order.</param>
public sealed record EvalRecipeResponse(IReadOnlyList<EvalCaseResult> Cases)
{
    /// <summary>Whether every case passed (exit 0; otherwise 1).</summary>
    public bool Passed => Cases.All(c => c.Passed);
}
