using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>The product band (spec §8.9): the own scope, the neighbors at the depth and the nodes the cascade reaches, each node in the first list that holds it.</summary>
internal static class LensProduct
{
    private static readonly NodeKind[] _kinds = [NodeKind.Feature, NodeKind.Surface, NodeKind.Action, NodeKind.Thing, NodeKind.Flow];
    private static readonly EdgeType[] _structure = [EdgeType.PartOf, EdgeType.On, EdgeType.Reads, EdgeType.Writes, EdgeType.StepOf, EdgeType.DependsOn, EdgeType.MovesTo];

    /// <summary>The band and the keys of every item it lists (the other bands' edges are drawn among them).</summary>
    /// <param name="scope">The anchor's scope.</param>
    /// <param name="depth">How far the neighbors reach.</param>
    /// <returns>The band and its item keys.</returns>
    internal static (ProductBand Band, HashSet<string> Keys) Build(LensScope scope, int depth)
    {
        var lists = Lists(scope, depth);
        var (own, neighbors, affects, seen) = (lists.Own, lists.Neighbors, lists.Affects, lists.Seen);
        var edges = Edges(scope.Graph, seen);
        return (Band(own, neighbors, affects, edges), seen);
    }

    /// <summary>The band from whole lists: every item listed.</summary>
    internal static ProductBand Band(IReadOnlyList<LensItem> own, IReadOnlyList<LensReach> neighbors, IReadOnlyList<LensReach> affects, IReadOnlyList<LensEdge> edges)
    {
        return new ProductBand([.. own], [.. neighbors], [.. affects],
            new ProductCounts(own.Count, affects.Count) { Neighbors = neighbors.Count, Edges = edges.Count }, [.. edges]);
    }

    /// <summary>Builds the graph indexes the lists read, so workers that build the lists at once only read them.</summary>
    internal static void Warm(GraphSnapshot graph)
    {
        _ = GraphIndex.For(graph);
        _ = GraphIndex.Of(graph, _structure);
        _ = EdgeLookup.For(graph);
    }

    /// <summary>The whole lists of one anchor's scope (nothing cut) and the keys of every item.</summary>
    internal static ProductLists Lists(LensScope scope, int depth)
    {
        var own = scope.Cascade.OwnScope.Where(k => scope.Nodes.ContainsKey(k.Value)).Select(k => LensScope.Item(scope.Nodes[k.Value])).ToList();
        var seen = own.Select(i => i.Node.Value).ToHashSet(StringComparer.Ordinal);
        var neighbors = Neighbors(scope, depth, seen);
        seen.UnionWith(neighbors.Select(n => n.Node.Value));
        var affects = Affects(scope, seen);
        seen.UnionWith(affects.Select(a => a.Node.Value));
        return new ProductLists(own, neighbors, affects, seen);
    }

    private static List<LensReach> Neighbors(LensScope scope, int depth, HashSet<string> taken)
    {
        var near = GraphWalk.Neighbors(scope.Graph, scope.Anchor.Key, depth).Nodes.Where(n => n.Depth > 0 && _kinds.Contains(n.Node.Key.Kind) && !taken.Contains(n.Node.Key.Value)).ToList();
        if (near.Count == 0)
        {
            return [];
        }

        var from = scope.Anchor.Key;
        var structure = GraphWalk.Parents(GraphIndex.Of(scope.Graph, _structure), from, stopAt: null);
        var any = new Lazy<Dictionary<string, PathStep?>>(() => GraphWalk.Parents(GraphIndex.For(scope.Graph), from, stopAt: null));
        return [.. near.Select(n => Reach(n.Node, n.Depth, Path(structure, any, from, n.Node.Key)))];
    }

    // The hops from the anchor, over structure edges where they reach and over any edge where they don't ("part-of-by" is an edge walked from its target).
    private static string[] Path(Dictionary<string, PathStep?> structure, Lazy<Dictionary<string, PathStep?>> any, NodeKey from, NodeKey to)
    {
        var path = GraphWalk.PathTo(structure, from, to) ?? GraphWalk.PathTo(any.Value, from, to);
        return path is null ? [] : [.. path.Steps.Select(s => $"{EdgeRules.Name(s.Edge.Type)}{(s.Forward ? string.Empty : "-by")} {s.Node.Value}")];
    }

    private static List<LensReach> Affects(LensScope scope, HashSet<string> taken) =>
        [.. scope.Cascade.Affected.Where(a => scope.Nodes.ContainsKey(a.Node.Value) && !taken.Contains(a.Node.Value)).Select(a => Reach(scope.Nodes[a.Node.Value], a.Depth, [.. a.Path]))];

    private static LensReach Reach(GraphNode n, int depth, IReadOnlyList<string> path)
    {
        var item = LensScope.Item(n);
        return new LensReach(item.Node, item.Label, item.Kind, item.Provenance, item.Confidence, item.Confirmed, item.Trust, depth, path);
    }

    internal static List<LensEdge> Edges(GraphSnapshot graph, HashSet<string> items) =>
        [.. graph.Edges.Where(e => _structure.Contains(e.Type) && items.Contains(e.From.Value) && items.Contains(e.To.Value))
            .OrderBy(e => e.Type).ThenBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)
            .Select(LensEdges.Of)];
}
