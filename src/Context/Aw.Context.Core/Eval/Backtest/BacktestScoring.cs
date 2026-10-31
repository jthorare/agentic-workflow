using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Scores one answer against one sample (spec §9.1, plan decision 5). Pure.</summary>
public static class BacktestScoring
{
    /// <summary>The nodes the truth files implement in the sample's own as-of map; a node that map lacks is never truth.</summary>
    /// <param name="asOf">The as-of graph.</param>
    /// <param name="truthFiles">The merge's changed files.</param>
    /// <returns>The node keys, ordinal order, without the 'code:' file nodes.</returns>
    public static IReadOnlyList<string> TruthNodes(GraphSnapshot asOf, IReadOnlyList<string> truthFiles)
    {
        ArgumentNullException.ThrowIfNull(asOf);
        ArgumentNullException.ThrowIfNull(truthFiles);
        var files = truthFiles.Select(f => "code:" + f).ToHashSet(StringComparer.Ordinal);
        var known = asOf.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return [.. asOf.Edges.Where(e => e.Type == EdgeType.Implements && files.Contains(e.From.Value) && known.Contains(e.To.Value) && !e.To.Value.StartsWith("code:", StringComparison.Ordinal))
            .Select(e => e.To.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>The nodes an answer named: the graph and affects sections' nodes and the anchors, without file nodes.</summary>
    /// <param name="result">The packed result.</param>
    /// <returns>The node keys, ordinal order.</returns>
    public static IReadOnlyList<string> PredictedNodes(ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return [.. result.Sections.Where(s => string.Equals(s.Name, "graph", StringComparison.Ordinal) || string.Equals(s.Name, "affects", StringComparison.Ordinal)).SelectMany(s => s.Items).Select(i => i.Node).OfType<string>().Concat(result.Anchors)
            .Where(k => !k.StartsWith("code:", StringComparison.Ordinal) && !k.StartsWith("map:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>The files an answer pointed at, in answer order: its 'code:' nodes, then its citations' repo paths.</summary>
    /// <param name="result">The packed result.</param>
    /// <returns>The paths without repeats.</returns>
    public static IReadOnlyList<string> PredictedFiles(ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return [.. ProbeScoring.Keys(result).Where(k => k.StartsWith("file:", StringComparison.Ordinal)).Select(k => k["file:".Length..]).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Scores an answer.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="arm">The arm's name.</param>
    /// <param name="truthNodes">The sample's truth nodes (<see cref="TruthNodes"/>).</param>
    /// <param name="result">The arm's answer.</param>
    /// <param name="armLeaky">True when the arm itself reads after the cut.</param>
    /// <returns>The score.</returns>
    public static SampleScore Score(CorpusSample sample, string arm, IReadOnlyList<string> truthNodes, ContextResult result, bool armLeaky)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(truthNodes);
        var predicted = PredictedNodes(result);
        var files = PredictedFiles(result).Take(PassBars.FileCutoff).ToList();
        var unmapped = truthNodes.Count == 0;
        var hit = predicted.Count(truthNodes.Contains);
        return new SampleScore(sample.IssueKey, arm, unmapped, unmapped ? null : Round((double)truthNodes.Count(predicted.Contains) / truthNodes.Count),
            unmapped ? null : Round(predicted.Count == 0 ? 0 : (double)hit / predicted.Count),
            Round(sample.TruthFiles.Count == 0 ? 0 : (double)sample.TruthFiles.Count(files.Contains) / sample.TruthFiles.Count),
            predicted, truthNodes, files, sample.Leaky || armLeaky);
    }

    /// <summary>One arm's means.</summary>
    /// <param name="scores">The arm's sample scores.</param>
    /// <param name="tokens">The model tokens spent on the arm.</param>
    /// <returns>The means; a metric with nothing to average is null (not measured), never 0.</returns>
    public static ArmScore Aggregate(IReadOnlyList<SampleScore> scores, int tokens)
    {
        ArgumentNullException.ThrowIfNull(scores);
        var mapped = scores.Where(s => !s.Unmapped).ToList();
        var judged = mapped.Count > 0 && mapped.TrueForAll(s => s.AdjudicatedPrecision is not null) ? Mean(mapped.Select(s => s.AdjudicatedPrecision)) : null;
        return new ArmScore(scores.Count, mapped.Count, Mean(mapped.Select(s => s.NodeRecall)), Mean(mapped.Select(s => s.NodePrecision)),
            Mean(scores.Select(s => (double?)s.FileRecallAt20)), scores.Count == 0 ? 0 : Round((double)scores.Count(s => s.Leaky) / scores.Count), tokens)
        {
            AdjudicatedPrecision = judged,
        };
    }

    /// <summary>Fewer pin-anchored questions than this and the hand check decides, not the numbers.</summary>
    public const int MinPinAnchored = 20;

    /// <summary>One ranker arm's scores over its pin-anchored samples.</summary>
    /// <param name="scores">The arm's sample scores.</param>
    /// <returns>The count and the means; null when no sample was pin-anchored.</returns>
    public static PinAnchoredScore? PinAnchored(IReadOnlyList<SampleScore> scores) => throw new NotSupportedException();

    private static double? Mean(IEnumerable<double?> values)
    {
        var list = values.OfType<double>().ToList();
        return list.Count == 0 ? null : Round(list.Average());
    }

    private static double Round(double value) => Math.Round(value, 4, MidpointRounding.ToEven);
}
