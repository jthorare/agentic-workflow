using Aw.Judge.Core.Batching;

namespace Aw.Judge.Core.PromptSort;

/// <summary>The prompt sorter's pure side: the questions asked of Jev, the keyword fallback, and the blend of the two.</summary>
public static class PromptSorter
{
    /// <summary>How much of the redacted prompt goes to Jev.</summary>
    public const int StateCap = 4000;

    /// <summary>The eleven questions (nine yes/no and two scores) asked in one batch, each keyed by its axis name.</summary>
    public static IReadOnlyList<BatchQuestion> Questions => SortAxes.Questions;

    /// <summary>The axes, in the order their rows are kept.</summary>
    public static IReadOnlyList<string> Axes => SortAxes.All;

    /// <summary>Sorts by the keyword heuristics alone: Jev was not asked or gave no answer.</summary>
    /// <param name="prompt">The prompt as typed.</param>
    /// <param name="reason">Why Jev's answer is missing: 'no-jev', or its reason code.</param>
    /// <param name="failure">The reason code when Jev errored or timed out ('judge health' counts it), else null.</param>
    /// <returns>The outcome.</returns>
    public static SortOutcome Heuristic(string prompt, string reason, string? failure = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(reason);
        var (values, axes, _) = AxisBlender.Blend(PromptHeuristics.Sort(prompt), answers: null);
        return new SortOutcome(values, axes, 0, SortOutcome.HeuristicOnly, reason, failure);
    }

    /// <summary>Blends Jev's answers over the heuristics, axis by axis.</summary>
    /// <param name="prompt">The prompt as typed.</param>
    /// <param name="answers">Jev's usable answers by axis name.</param>
    /// <returns>The outcome: a blend when Jev decided at least one axis, else heuristic-only ('all-undecided').</returns>
    public static SortOutcome Blend(string prompt, IReadOnlyDictionary<string, BatchAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(answers);
        var (values, axes, decided) = AxisBlender.Blend(PromptHeuristics.Sort(prompt), answers);
        return decided > 0
            ? new SortOutcome(values, axes, decided, SortOutcome.Blend, "sorted")
            : new SortOutcome(values, axes, 0, SortOutcome.HeuristicOnly, "all-undecided");
    }
}
