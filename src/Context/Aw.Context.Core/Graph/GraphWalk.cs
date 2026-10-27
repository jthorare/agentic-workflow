namespace Aw.Context.Core.Graph;

/// <summary>Neighbors and paths over the graph (spec §8.5 'neighbors', 'path'), edges walked in either direction, breadth first. Pure.</summary>
public static class GraphWalk
{
    /// <summary>The deepest neighborhood a request gets; a deeper ask is read as this (spec §8.3).</summary>
    public const int MaxDepth = Engine.QueryRules.MaxDepth;

    /// <summary>The nodes within <paramref name="depth"/> edges of <paramref name="center"/>.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="center">A node key in <paramref name="graph"/>.</param>
    /// <param name="depth">0 or more; past <see cref="MaxDepth"/> it is read as <see cref="MaxDepth"/>.</param>
    /// <returns>The neighborhood.</returns>
    public static Neighborhood Neighbors(GraphSnapshot graph, NodeKey center, int depth)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Neighbors(GraphIndex.For(graph), center, depth);
    }

    /// <summary>The nodes within <paramref name="depth"/> edges of <paramref name="center"/> over the edges <paramref name="index"/> holds.</summary>
    internal static Neighborhood Neighbors(GraphIndex index, NodeKey center, int depth)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        depth = Math.Min(depth, MaxDepth);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [center.Value] = 0 };
        var frontier = new List<NodeKey> { center };
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            frontier = [.. frontier.SelectMany(k => Adjacent(index, k)).Select(s => s.Node).Where(k => depths.TryAdd(k.Value, level))];
        }

        var nodes = depths.Where(d => index.Nodes.ContainsKey(d.Key))
            .Select(d => new NeighborNode(index.Nodes[d.Key], d.Value))
            .OrderBy(n => n.Depth).ThenBy(n => n.Node.Key.Value, StringComparer.Ordinal).ToList();
        var edges = index.Graph.Edges.Where(e => depths.ContainsKey(e.From.Value) && depths.ContainsKey(e.To.Value))
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type).ToList();
        return new Neighborhood(center, nodes, edges);
    }

    /// <summary>The shortest path from <paramref name="from"/> to <paramref name="to"/>; any length (the path limit was removed per owner q93, 2026-10-23).</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="from">A node key in <paramref name="graph"/>.</param>
    /// <param name="to">A node key in <paramref name="graph"/>.</param>
    /// <returns>The path, or null when the two nodes are not connected.</returns>
    public static GraphPath? ShortestPath(GraphSnapshot graph, NodeKey from, NodeKey to)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return PathTo(Parents(GraphIndex.For(graph), from, to), from, to);
    }

    /// <summary>The breadth-first parent of every node reached from <paramref name="from"/>, stopping after the level that reaches <paramref name="stopAt"/> (every level when null); a node's parent is the same whether or not the walk stops early.</summary>
    internal static Dictionary<string, PathStep?> Parents(GraphIndex index, NodeKey from, NodeKey? stopAt)
    {
        var parents = new Dictionary<string, PathStep?>(StringComparer.Ordinal) { [from.Value] = null };
        var frontier = new List<NodeKey> { from };
        while (frontier.Count > 0 && (stopAt is not { } to || !parents.ContainsKey(to.Value)))
        {
            var next = new List<NodeKey>();
            foreach (var node in frontier)
            {
                foreach (var step in Adjacent(index, node).Where(s => parents.TryAdd(s.Node.Value, s)))
                {
                    next.Add(step.Node);
                }
            }

            frontier = next;
        }

        return parents;
    }

    /// <summary>The path <paramref name="parents"/> holds from <paramref name="from"/> to <paramref name="to"/>, or null when it holds none.</summary>
    internal static GraphPath? PathTo(Dictionary<string, PathStep?> parents, NodeKey from, NodeKey to)
    {
        if (!parents.ContainsKey(to.Value))
        {
            return null;
        }

        var steps = new List<PathStep>();
        for (var at = to.Value; parents[at] is { } step; at = step.Forward ? step.Edge.From.Value : step.Edge.To.Value)
        {
            steps.Add(step);
        }

        steps.Reverse();
        return new GraphPath(from, to, steps);
    }

    /// <summary>Every edge at <paramref name="node"/> as a step to its other end: outgoing first, then incoming, each in key order.</summary>
    private static IEnumerable<PathStep> Adjacent(GraphIndex index, NodeKey node) =>
        index.Out(node).Select(e => new PathStep(e, Forward: true, e.To))
            .Concat(index.In(node).Select(e => new PathStep(e, Forward: false, e.From)))
            .Where(s => s.Node.Value != node.Value);
}
