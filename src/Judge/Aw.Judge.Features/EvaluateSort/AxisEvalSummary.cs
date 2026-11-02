using Aw.Judge.Core.Labels;

namespace Aw.Judge.Features.EvaluateSort;

/// <summary>One axis scored against one label source.</summary>
/// <param name="Axis">The axis name.</param>
/// <param name="Source">The label source.</param>
/// <param name="N">How many items were scored.</param>
/// <param name="Heuristic">The keyword heuristic alone.</param>
/// <param name="Blend">The heuristic blended with the judge.</param>
public sealed record AxisEvalSummary(string Axis, LabelSource Source, int N, PolicyScore Heuristic, PolicyScore Blend);
