using Aw.Judge.Core.Batching;

namespace Aw.Judge.Core.PromptSort;

/// <summary>
/// The decisive-band policy: per axis, Jev counts only when decisive (a yes/no outside 0.4 to 0.6, a score at confidence
/// of at least 0.4); otherwise the keyword heuristic stays in charge of that axis. Never a whole-call failure.
/// </summary>
internal static class AxisBlender
{
    /// <summary>Blends Jev's answers over the heuristic values; null answers (Jev unavailable) keep every heuristic value.</summary>
    internal static (SortValues Values, IReadOnlyList<SortedAxis> Axes, int JudgeDecided) Blend(SortValues heuristic, IReadOnlyDictionary<string, BatchAnswer>? answers)
    {
        var values = new Dictionary<string, string>(heuristic.Values, StringComparer.Ordinal);
        var axes = new List<SortedAxis>();
        foreach (var axis in SortAxes.All)
        {
            var read = answers is not null && answers.TryGetValue(axis, out var answer) ? Read(axis, answer) : null;
            var row = Row(axis, heuristic.Values[axis], read);
            values[axis] = row.Value;
            axes.Add(row);
        }

        return (new SortValues(values), axes, axes.Count(static a => string.Equals(a.Source, "judge", StringComparison.Ordinal)));
    }

    private static SortedAxis Row(string axis, string heuristic, AxisRead? read)
    {
        if (read?.Verdict is not { } verdict)
        {
            return new SortedAxis(axis, heuristic, heuristic, "heuristic", "undecided", read?.Probability, read?.Confidence);
        }

        var agreed = string.Equals(SortAxes.Class(axis, verdict), SortAxes.Class(axis, heuristic), StringComparison.Ordinal);
        return new SortedAxis(axis, verdict, heuristic, "judge", agreed ? "agreed" : "overrode", read.Probability, read.Confidence);
    }

    // What one answer says: the number it is, how sure, and the value it decides (null inside the band).
    private static AxisRead? Read(string axis, BatchAnswer answer) => (axis, answer) switch
    {
        (SortAxes.Complexity, ScoreBatchAnswer score) => ReadScore(score, SortAxes.ComplexityCriteria, SortAxes.ComplexityLevels, weights: null),
        (SortAxes.Ambiguity, ScoreBatchAnswer score) => ReadScore(score, SortAxes.AmbiguityCriteria, levels: null, SortAxes.AmbiguityWeights),
        (_, NoulBatchAnswer noul) when axis is not (SortAxes.Complexity or SortAxes.Ambiguity) => ReadNoul(noul.Noul),
        _ => null,
    };

    private static AxisRead ReadNoul(double noul) => new(
        noul >= SortAxes.High ? SortAxes.Yes : noul <= SortAxes.Low ? SortAxes.No : null,
        noul,
        Confidence: null);

    private static AxisRead ReadScore(ScoreBatchAnswer answer, IReadOnlyList<string> criteria, IReadOnlyList<string>? levels, IReadOnlyList<double>? weights)
    {
        var top = criteria.Count - 1;
        var probabilities = criteria.Select((criterion, index) => Probability(answer.Probabilities, index, criterion)).ToArray();
        var mass = probabilities.Sum();
        var index = mass > 0 ? Array.IndexOf(probabilities, probabilities.Max()) : Math.Min(top, Math.Max(0, (int)Math.Floor(answer.Score + 0.5)));
        var value = Value(weights, probabilities, mass, index, top);
        var level = levels is not null ? levels[index] : value >= 0.5 ? "vague" : value >= 0.2 ? "partly" : "clear";
        return new AxisRead(answer.Confidence >= SortAxes.MinConfidence ? level : null, value, answer.Confidence);
    }

    private static double Value(IReadOnlyList<double>? weights, double[] probabilities, double mass, int index, int top)
    {
        if (weights is null)
        {
            return (double)index / top;
        }

        return mass > 0 ? probabilities.Select((p, i) => p * weights[i]).Sum() / mass : weights[index];
    }

    // Jev's probabilities may be keyed by index ("0"), by the short level name or by the full "name: description" sent.
    private static double Probability(IReadOnlyDictionary<string, double> probabilities, int index, string criterion)
    {
        foreach (var key in new[] { index.ToString(System.Globalization.CultureInfo.InvariantCulture), criterion, criterion.Split(':')[0] })
        {
            if (probabilities.TryGetValue(key, out var found))
            {
                return found;
            }
        }

        return 0;
    }

    private sealed record AxisRead(string? Verdict, double Probability, double? Confidence);
}
