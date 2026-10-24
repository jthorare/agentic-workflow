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
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        depth = Math.Min(depth, MaxDepth);
        var index = new GraphIndex(graph);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [center.Value] = 0 };
        var frontier = new List<NodeKey> { center };
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            frontier = [.. frontier.SelectMany(k => Adjacent(index, k)).Select(s => s.Node).Where(k => depths.TryAdd(k.Value, level))];
        }

        var nodes = depths.Where(d => index.Nodes.ContainsKey(d.Key))
            .Select(d => new NeighborNode(index.Nodes[d.Key], d.Value))
            .OrderBy(n => n.Depth).ThenBy(n => n.Node.Key.Value, StringComparer.Ordinal).ToList();
        var edges = graph.Edges.Where(e => depths.ContainsKey(e.From.Value) && depths.ContainsKey(e.To.Value))
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
        var index = new GraphIndex(graph);
        var parents = new Dictionary<string, PathStep?>(StringComparer.Ordinal) { [from.Value] = null };
        var frontier = new List<NodeKey> { from };
        for (var level = 1; !parents.ContainsKey(to.Value) && frontier.Count > 0; level++)
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
