namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>Run a recipe's eval cases in-process (spec §9.2, W27).</summary>
/// <param name="Path">The recipe file; its evals are read relative to its directory.</param>
public sealed record EvalRecipeRequest(string Path);
