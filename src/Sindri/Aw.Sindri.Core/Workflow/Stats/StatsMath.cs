namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The medians and percentiles the stats report.</summary>
internal static class StatsMath
{
    public static long? Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? null : (long)Math.Round(sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2, MidpointRounding.AwayFromZero);
    }

    // nearest rank: the smallest value that at least p of the values are at or below
    public static long? Rank(List<double> sorted, double p) => sorted.Count == 0 ? null : (long)sorted[Math.Max((int)Math.Ceiling(p * sorted.Count), 1) - 1];

    public static void Increment(this Dictionary<string, int> counts, string reason) => counts[reason] = counts.GetValueOrDefault(reason) + 1;
}
