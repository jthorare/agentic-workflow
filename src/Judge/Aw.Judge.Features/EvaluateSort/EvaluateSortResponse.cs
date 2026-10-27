namespace Aw.Judge.Features.EvaluateSort;

/// <summary>The EvaluateSort response.</summary>
/// <param name="RanAt">When the eval ran.</param>
/// <param name="ReportPath">The report written under judge/evals/.</param>
/// <param name="Summaries">The heuristic and the blend scored per axis and label source.</param>
public sealed record EvaluateSortResponse(DateTimeOffset RanAt, string ReportPath, IReadOnlyList<AxisEvalSummary> Summaries);
