using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;

namespace Aw.Context.Core.Lenses;

/// <summary>Conflicts on the own scope (spec §8.9): a decision that supersedes another, once per decided node. a decision that cites a file the map says implements something else (S-CF), and the model-checked 'contradicts' pairs.</summary>
internal static class LensConflicts
{
    private const int ShortKey = 17;

    /// <summary>The conflicts of the scope, ordered by node, decision and the one it supersedes.</summary>
    internal static List<LensConflict> Of(LensScope scope, IReadOnlyList<DecisionConflict> checkedPairs) => Of(scope.Graph, scope.Own, checkedPairs);

    /// <summary>The conflicts on the nodes of <paramref name="own"/> in <paramref name="graph"/>.</summary>
    internal static List<LensConflict> Of(GraphSnapshot graph, HashSet<string> own, IReadOnlyList<DecisionConflict> checkedPairs)
    {
        var supersedes = Supersedes(graph, own);
        var contradicts = Contradicts(graph, own, checkedPairs);
        return [.. supersedes, .. contradicts, .. FileConflicts(graph, own)];
    }

    private static IEnumerable<GraphEdge> Into(GraphSnapshot graph, HashSet<string> own, EdgeType type) => graph.Edges.Where(e => e.Type == type && own.Contains(e.To.Value));

    private static List<LensConflict> Contradicts(GraphSnapshot graph, HashSet<string> own, IReadOnlyList<DecisionConflict> checkedPairs)
    {
        var decides = Into(graph, own, EdgeType.Decides).ToLookup(e => e.From.Value, e => e.To, StringComparer.Ordinal);
        return [.. checkedPairs.Where(c => c.Verdict == ConflictVerdict.Contradicts)
            .SelectMany(c => decides[c.A.Value].Where(n => decides[c.B.Value].Any(m => m.Value == n.Value)).Select(n => (Node: n, Pair: c)))
            .OrderBy(x => x.Node.Value, StringComparer.Ordinal).ThenBy(x => x.Pair.A.Value, StringComparer.Ordinal).ThenBy(x => x.Pair.B.Value, StringComparer.Ordinal)
            .Select(x => new LensConflict(ConflictKind.Contradicts, x.Node, x.Pair.A, x.Pair.B.Value, $"{Short(x.Pair.A)} contradicts {Short(x.Pair.B)}", [], []))];
    }

    // S-CF: a decision that decides an own-scope node and also cites a file (decides → code) which implements other nodes but not that one or anything in its part-of chain (ancestors and descendants count as the same feature).
    private static List<LensConflict> FileConflicts(GraphSnapshot graph, HashSet<string> own)
    {
        var implemented = graph.Edges.Where(e => e.Type == EdgeType.Implements).ToLookup(e => e.From.Value, e => e.To, StringComparer.Ordinal);
        var cited = graph.Edges.Where(e => e.Type == EdgeType.Decides && e.To.Kind == NodeKind.Code && implemented.Contains(e.To.Value)).ToLookup(e => e.From.Value, e => e.To, StringComparer.Ordinal);
        var chains = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        IReadOnlySet<string> Chain(NodeKey node) => chains.TryGetValue(node.Value, out var c) ? c : chains[node.Value] = RepoScope.WithHierarchy(graph, new HashSet<string>([node.Value], StringComparer.Ordinal));
        return [.. Into(graph, own, EdgeType.Decides)
            .SelectMany(decides => cited[decides.From.Value]
                .Where(file => !implemented[file.Value].Any(i => Chain(decides.To).Contains(i.Value)))
                .Select(file => (Node: decides.To, Decision: decides.From, File: file, Targets: implemented[file.Value].Select(i => i.Value).Order(StringComparer.Ordinal).ToList())))
            .OrderBy(c => c.Node.Value, StringComparer.Ordinal).ThenBy(c => c.Decision.Value, StringComparer.Ordinal).ThenBy(c => c.File.Value, StringComparer.Ordinal)
            .Select(c => new LensConflict(ConflictKind.Contradicts, c.Node, c.Decision, c.File.Value, $"{Short(c.Decision)} cites {c.File.Value}, which implements {string.Join(", ", c.Targets)}", [], []))];
    }

    private static List<LensConflict> Supersedes(GraphSnapshot graph, HashSet<string> own)
    {
        var supersedes = graph.Edges.Where(e => e.Type == EdgeType.Supersedes).ToList();
        return [.. Into(graph, own, EdgeType.Decides)
            .SelectMany(decides => supersedes.Where(s => s.From.Value == decides.From.Value).Select(s => (Node: decides.To, Decision: s.From, Other: s.To)))
            .Distinct().OrderBy(c => c.Node.Value, StringComparer.Ordinal).ThenBy(c => c.Decision.Value, StringComparer.Ordinal).ThenBy(c => c.Other.Value, StringComparer.Ordinal)
            .Select(c => new LensConflict(ConflictKind.Supersedes, c.Node, c.Decision, c.Other.Value, $"{Short(c.Decision)} supersedes {Short(c.Other)}", [], []))];
    }

    private static string Short(NodeKey key) => key.Value.Length <= ShortKey ? key.Value : key.Value[..ShortKey];
}
