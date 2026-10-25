using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>
/// The cascade (spec §6.1): what a change to an anchor affects, computed per query over typed steps. Own scope is free;
/// a state hop (writes a thing another node reads), a dependency hop (reverse depends-on) and a flow hop (the next step
/// of a shared flow) cost 1 each; a reached surface or action is reported as its owning feature (ascend, free), and only
/// the reached node steps on, never the ascended feature's own scope. Breadth first under a depth cap with a visited
/// set, so each node is reported once with its shortest path. Pure.
/// </summary>
public static class Cascade
{
    /// <summary>The anchor's own scope and the nodes up to <paramref name="depth"/> steps from it.</summary>
    /// <param name="graph">The graph a caller may see (overrides applied, a session's scope already cut).</param>
    /// <param name="anchor">A node key in <paramref name="graph"/> (resolve aliases first).</param>
    /// <param name="depth">The depth cap, 0 to 4 (0: own scope only).</param>
    /// <returns>The own scope and the affected nodes.</returns>
    public static CascadeResult Affects(GraphSnapshot graph, NodeKey anchor, int depth) => Affects(graph, anchor, depth, CascadeOptions.Default);

    /// <summary>The anchor's own scope and the nodes up to <paramref name="depth"/> steps from it, taking only the hops <paramref name="options"/> names.</summary>
    /// <param name="graph">The graph a caller may see (overrides applied, a session's scope already cut).</param>
    /// <param name="anchor">A node key in <paramref name="graph"/> (resolve aliases first).</param>
    /// <param name="depth">The depth cap, 0 to 4 (0: own scope only).</param>
    /// <param name="options">The hops and the component fan-out cap.</param>
    /// <returns>The own scope, the affected nodes and the skipped components.</returns>
    public static CascadeResult Affects(GraphSnapshot graph, NodeKey anchor, int depth, CascadeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(graph);
        var index = GraphIndex.For(graph);
        var own = OwnScope(index, anchor);
        var ownKeys = own.Select(d => d.Key.Value).ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(ownKeys, StringComparer.Ordinal);
        var affected = new List<AffectedNode>();
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var reported = new HashSet<string>(ownKeys, StringComparer.Ordinal);
        var frontier = own.Select(d => new Reach(d.Key, d.Path, d.Edges)).OrderBy(r => OwnsHops(r.Path)).ThenBy(r => r.Node.Value, StringComparer.Ordinal).ToList();
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            var next = new List<Reach>();
            foreach (var reach in frontier)
            {
                foreach (var step in Steps(index, reach.Node, options, visited, skipped).Where(s => visited.Add(s.Node.Value)))
                {
                    var reached = new Reach(step.Node, [.. reach.Path, .. step.Path], [.. reach.Edges, .. step.Edges]);
                    next.Add(reached);
                    var owner = Ascend(index, step.Node);
                    if (reported.Add(owner.Value))
                    {
                        affected.Add(new AffectedNode(owner, level, reached.Path, reached.Edges));
                    }
                }
            }

            frontier = [.. next.OrderBy(r => OwnsHops(r.Path)).ThenBy(r => r.Node.Value, StringComparer.Ordinal)];
        }

        return new CascadeResult([.. own.Select(d => d.Key)], [.. affected.OrderBy(a => a.Depth).ThenBy(a => a.Node.Value, StringComparer.Ordinal)])
        {
            SkippedComponents = [.. skipped.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => new SkippedComponent(k.Key, k.Value))],
        };
    }

    /// <summary>
    /// The anchor first, then by key: a feature's descendant features, their surfaces and those surfaces' actions (a surface's
    /// actions), each with the real owns hops down to it (part-of into a feature, on a surface), so an affects path never invents one.
    /// </summary>
    private static List<Descent> OwnScope(GraphIndex index, NodeKey anchor)
    {
        var root = new Descent(anchor, [anchor.Value], []);
        var features = FeatureScope(index, anchor, root);

        var surfaces = anchor.Kind == NodeKind.Surface
            ? [root]
            : features.SelectMany(f => index.In(f.Key, EdgeType.PartOf).Where(e => e.From.Kind == NodeKind.Surface).Select(e => f.Down(e.From, e))).ToList();
        var actions = surfaces.SelectMany(s => index.In(s.Key, EdgeType.On).Select(e => s.Down(e.From, e)));
        var rest = features.Concat(surfaces).Concat(actions)
            .Where(d => d.Key.Value != anchor.Value).DistinctBy(d => d.Key.Value, StringComparer.Ordinal).OrderBy(d => d.Key.Value, StringComparer.Ordinal);
        return [root, .. rest];
    }

    /// <summary>The features an anchor owns: a feature and its descendants, a concept's member features and theirs (the anchor first when it is a feature), none otherwise.</summary>
    private static List<Descent> FeatureScope(GraphIndex index, NodeKey anchor, Descent root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { anchor.Value };
        var queue = new Queue<Descent>();
        if (anchor.Kind == NodeKind.Feature)
        {
            queue.Enqueue(root);
        }
        else if (anchor.Kind == NodeKind.Concept)
        {
            foreach (var edge in index.In(anchor, EdgeType.BelongsTo).Where(e => e.From.Kind == NodeKind.Feature && seen.Add(e.From.Value)))
            {
                queue.Enqueue(root.Down(edge.From, edge));
            }
        }

        var features = new List<Descent>();
        while (queue.TryDequeue(out var feature))
        {
            features.Add(feature);
            foreach (var edge in index.In(feature.Key, EdgeType.PartOf).Where(e => e.From.Kind == NodeKind.Feature && seen.Add(e.From.Value)))
            {
                queue.Enqueue(feature.Down(edge.From, edge));
            }
        }

        return features;
    }

    private static IEnumerable<Step> Steps(GraphIndex index, NodeKey node, CascadeOptions options, HashSet<string> visited, Dictionary<string, int> skipped)
    {
        var own = OwnSteps(index, node, options.Hops);
        var component = options.Hops.HasFlag(CascadeHops.Component) && node.Kind is NodeKind.Feature or NodeKind.Surface or NodeKind.Action
            ? ComponentHop.From(index, node, options.ComponentFanOut, visited, skipped)
            : [];
        return own.Concat(component);
    }

    private static IEnumerable<Step> OwnSteps(GraphIndex index, NodeKey node, CascadeHops hops) => node.Kind switch
    {
        NodeKind.Feature => hops.HasFlag(CascadeHops.Dependency) ? index.In(node, EdgeType.DependsOn).Select(e => new Step(e.From, [$"depended-on-by {e.From.Value}"], [e])) : [],
        NodeKind.Surface or NodeKind.Action => (hops.HasFlag(CascadeHops.State) ? StateHops(index, node) : []).Concat(hops.HasFlag(CascadeHops.Flow) ? FlowHops(index, node) : []),
        NodeKind.Thing => hops.HasFlag(CascadeHops.State) ? Readers(index, node, prefix: null) : [],
        NodeKind.Flow or NodeKind.Decision or NodeKind.Record or NodeKind.Code or NodeKind.Concept => [],
        _ => [],
    };

    private static IEnumerable<Step> StateHops(GraphIndex index, NodeKey node) =>
        index.Out(node, EdgeType.Writes).SelectMany(write => Readers(index, write.To, write).Where(s => s.Node.Value != node.Value));

    private static IEnumerable<Step> Readers(GraphIndex index, NodeKey thing, GraphEdge? prefix) =>
        index.In(thing, EdgeType.Reads).Select(read => prefix is null
            ? new Step(read.From, [$"reads {read.From.Value}"], [read])
            : new Step(read.From, [$"writes {thing.Value}", $"reads {read.From.Value}"], [prefix, read]));

    private static IEnumerable<Step> FlowHops(GraphIndex index, NodeKey node) =>
        index.Out(node, EdgeType.StepOf).Where(e => e.Order is not null).SelectMany(own =>
            index.In(own.To, EdgeType.StepOf).Where(e => e.Order > own.Order).GroupBy(e => e.Order).OrderBy(g => g.Key).Take(1).SelectMany(g => g)
                .Select(next => new Step(next.From, [$"step-of {own.To.Value}", $"next {next.From.Value}"], [own, next])));

    /// <summary>A surface's owning feature (part-of), an action's through its surface (on, then part-of); the node itself when it has none.</summary>
    internal static NodeKey Ascend(GraphIndex index, NodeKey node)
    {
        var surface = node.Kind == NodeKind.Action ? index.Out(node, EdgeType.On).Select(e => (NodeKey?)e.To).FirstOrDefault() : node;
        if (surface is not { Kind: NodeKind.Surface } s)
        {
            return node;
        }

        return index.Out(s, EdgeType.PartOf).Select(e => (NodeKey?)e.To).FirstOrDefault(k => k is { Kind: NodeKind.Feature }) ?? node;
    }

    /// <summary>How many 'owns' steps a path walks down the anchor's own scope: among equally deep paths the one with the fewest claims a node, then key order.</summary>
    /// <param name="path">The path as <see cref="AffectedNode.Path"/> writes it.</param>
    /// <returns>The count.</returns>
    public static int OwnsHops(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Count(p => p.StartsWith("owns ", StringComparison.Ordinal));
    }

    private sealed record Descent(NodeKey Key, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges)
    {
        public Descent Down(NodeKey child, GraphEdge edge) => new(child, [.. Path, $"owns {child.Value}"], [.. Edges, edge]);
    }

    private sealed record Reach(NodeKey Node, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges);

    internal sealed record Step(NodeKey Node, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges);
}
