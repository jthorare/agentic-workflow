namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>One eval case's result (spec §9.2).</summary>
/// <param name="Case">The case file, as the recipe lists it.</param>
/// <param name="Passed">Whether every line reached its expected state.</param>
/// <param name="Line">The first line that diverged; null when it passed.</param>
/// <param name="Detail">What diverged; null when it passed.</param>
public sealed record EvalCaseResult(string Case, bool Passed, int? Line, string? Detail);
