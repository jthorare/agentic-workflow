namespace Aw.Judge.Core.PromptSort;

/// <summary>What sorting one prompt found.</summary>
/// <param name="Values">The blended value of each axis.</param>
/// <param name="Axes">One row per axis: the blended value, the heuristic's and where it came from.</param>
/// <param name="JudgeDecided">How many axes Jev decided.</param>
/// <param name="Mode">'blend' when Jev decided at least one axis, else 'heuristic-only'.</param>
/// <param name="Reason">Why: 'sorted', 'all-undecided', 'no-jev' or Jev's reason for having no answer.</param>
/// <param name="Failure">A Jev error or timeout that 'judge health' must count (the reason code), or null.</param>
public sealed record SortOutcome(SortValues Values, IReadOnlyList<SortedAxis> Axes, int JudgeDecided, string Mode, string Reason, string? Failure = null)
{
    internal const string Blend = "blend";
    internal const string HeuristicOnly = "heuristic-only";

    /// <summary>Whether Jev itself decided this axis (not the keyword heuristic).</summary>
    /// <param name="axis">The axis name.</param>
    /// <returns>True when its source is the judge.</returns>
    public bool Judged(string axis) => Axes.Any(a => string.Equals(a.Axis, axis, StringComparison.Ordinal) && string.Equals(a.Source, "judge", StringComparison.Ordinal));
}
