using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>The code band (spec §8.9): one entry per file that implements or tests a scope node. File level until the code index exists (Phase 9).</summary>
internal static class LensCode
{
    /// <summary>Builds the band.</summary>
    /// <param name="scope">The anchor's scope.</param>
    /// <returns>The band (at most 100 files) and the keys of all its files.</returns>
    internal static (CodeBand Band, HashSet<string> Keys) Build(LensScope scope)
    {
        var files = Files(scope.IntoScope(EdgeType.Implements, EdgeType.Tests).Where(e => scope.Nodes.ContainsKey(e.From.Value)));
        var band = Band(files, NoCode(scope.Graph, scope.Cascade.OwnScope));
        return (band, scope.IntoScope(EdgeType.Implements, EdgeType.Tests).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>The band from whole file lists (every file listed).</summary>
    internal static CodeBand Band(IReadOnlyList<LensFile> files, int featuresWithNoCode) =>
        new([.. files], LensSymbols.Unavailable, new CodeCounts(files.Count, 0, featuresWithNoCode), Truncated: false);

    /// <summary>One entry per file that has an edge among <paramref name="edges"/>, ordered by file key.</summary>
    internal static List<LensFile> Files(IEnumerable<GraphEdge> edges) =>
        [.. edges.GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(File)];

    /// <summary>Features of <paramref name="own"/> that no confirmed 'implements' edge reaches.</summary>
    internal static int NoCode(GraphSnapshot graph, IEnumerable<NodeKey> own) =>
        own.Count(k => k.Kind == NodeKind.Feature && !Implemented(graph).Contains(k.Value));

    private static HashSet<string> Implemented(GraphSnapshot graph) =>
        graph.Edges.Where(e => e.Type == EdgeType.Implements && LensScope.Confirmed(e.Provenance)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);

    private static LensFile File(IGrouping<string, GraphEdge> group)
    {
        var edges = group.ToList();
        var best = edges.OrderByDescending(e => LensScope.Confirmed(e.Provenance)).ThenByDescending(e => e.Confidence).ThenBy(e => e.To.Value, StringComparer.Ordinal).First();
        var confirmed = edges.Exists(e => LensScope.Confirmed(e.Provenance));
        return new LensFile(best.From.Name, Targets(edges, EdgeType.Implements), Targets(edges, EdgeType.Tests), best.Provenance, best.Confidence, confirmed, Suggested: !confirmed,
            confirmed ? null : LinkIds.For(best), best.From.Name, NodeKind.Code, TrustPolicy.For(best.Provenance, confirmed));
    }

    private static IReadOnlyList<NodeKey> Targets(List<GraphEdge> edges, EdgeType type) =>
        [.. edges.Where(e => e.Type == type).Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];
}
