using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Subgraph;

/// <summary>Builds the subgraph view (scoping spec C14). Pure over an applied graph.</summary>
public static class SubgraphBuilder
{
    private const int ShownKey = 80;

    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="keys">The node keys as given.</param>
    /// <param name="depth">How far to widen, 0 to 4.</param>
    /// <param name="conflicts">The model-checked decision pairs.</param>
    /// <returns>The view; SND-CONTEXT-006 for no key or a malformed key, or a negative depth; a depth past 8 is read as 8.</returns>
    public static Result<SubgraphView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> keys, int depth, IReadOnlyList<DecisionConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(conflicts);
        if (QueryRules.CheckDepth(depth) is { } badDepth)
        {
            return badDepth;
        }

        depth = Math.Min(depth, QueryRules.MaxDepth);

        var nodes = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        var resolved = Resolve(nodes, keys);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (given, missing) = resolved.Value;
        var reached = Widen(graph, nodes, given, depth);
        var order = reached.OrderBy(r => r.Value.Depth).ThenBy(r => r.Value.Origin).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key).ToList();
        var all = reached.Keys.ToHashSet(StringComparer.Ordinal);
        var bands = new LensBands(Intent(graph, nodes, order, reached, all), Product(graph, nodes, order, reached), Code(graph, nodes, order, reached, all));
        var givenProduct = given.Where(k => IsProduct(k.Kind)).ToList();
        var ownSet = givenProduct.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        var coverage = LensCoverageBuilder.Of(graph, nodes, givenProduct) with { Map = MapCoverageBuilder.Of(graph, ownSet, basis, all.Where(k => nodes[k].Key.Kind == NodeKind.Code)) };
        var searched = LensSearchedBuilder.For(basis, [.. given.Select(k => k.Value)], depth, bands) with { Hops = [] };
        return new SubgraphView(1, given, missing, basis.Map, depth, basis.AsOf, bands, LensEdges.Across(graph, all), LensConflicts.Of(graph, all, conflicts), coverage, searched, [],
            new Dictionary<string, Citation>(StringComparer.Ordinal))
        {
            MapCommit = basis.MapCommit,
        };
    }

    private static Result<(List<NodeKey> Given, List<string> Missing)> Resolve(Dictionary<string, GraphNode> nodes, IReadOnlyList<string> keys)
    {
        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count is 0)
        {
            return ContextErrors.QueryInvalid.With("a subgraph call takes at least one key");
        }

        var given = new List<NodeKey>();
        var missing = new List<string>();
        foreach (var raw in distinct)
        {
            if (!NodeKey.TryFrom(raw, out var key))
            {
                var shown = raw.Length <= ShownKey ? raw : "(a long key)";
                return ContextErrors.QueryInvalid.With($"'{shown}' is not a node key (a kind, a colon and a name: feature:, surface:, action:, thing:, flow:, decision:, record: or code:)");
            }

            if (nodes.ContainsKey(raw))
            {
                given.Add(key);
            }
            else
            {
                missing.Add(raw);
            }
        }

        return (given, missing);
    }

    private sealed record Hit(int Depth, int Origin, string[] Path);

    private static bool IsProduct(NodeKind kind) => kind is not (NodeKind.Record or NodeKind.Decision or NodeKind.Code);

    // Breadth first over every edge in either direction, from the given keys in request order; the first to reach a node owns it.
    private static Dictionary<string, Hit> Widen(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, List<NodeKey> given, int depth)
    {
        var reached = new Dictionary<string, Hit>(StringComparer.Ordinal);
        for (var i = 0; i < given.Count; i++)
        {
            reached[given[i].Value] = new Hit(0, i, []);
        }

        if (depth == 0)
        {
            return reached;
        }

        var adjacent = Adjacency(graph, nodes);
        var frontier = given.Select(k => k.Value).ToList();
        for (var level = 1; level <= depth && frontier.Count > 0; level++)
        {
            frontier = Expand(frontier, adjacent, reached, level);
        }

        return reached;
    }

    private static List<string> Expand(List<string> frontier, Dictionary<string, List<(string Node, string Hop)>> adjacent, Dictionary<string, Hit> reached, int level)
    {
        var next = new List<string>();
        foreach (var from in frontier)
        {
            var parent = reached[from];
            foreach (var (node, hop) in adjacent.GetValueOrDefault(from) ?? [])
            {
                if (reached.TryAdd(node, new Hit(level, parent.Origin, [.. parent.Path, hop])))
                {
                    next.Add(node);
                }
            }
        }

        return next;
    }

    private static Dictionary<string, List<(string Node, string Hop)>> Adjacency(GraphSnapshot graph, Dictionary<string, GraphNode> nodes)
    {
        var adjacent = new Dictionary<string, List<(string Node, string Hop)>>(StringComparer.Ordinal);
        foreach (var e in graph.Edges.Where(e => nodes.ContainsKey(e.From.Value) && nodes.ContainsKey(e.To.Value)))
        {
            var name = EdgeRules.Name(e.Type);
            Link(adjacent, e.From.Value, (e.To.Value, $"{name} {e.To.Value}"));
            Link(adjacent, e.To.Value, (e.From.Value, $"{name}-by {e.From.Value}"));
        }

        return adjacent;
    }

    private static void Link(Dictionary<string, List<(string Node, string Hop)>> adjacent, string from, (string Node, string Hop) step)
    {
        if (!adjacent.TryGetValue(from, out var list))
        {
            adjacent[from] = list = [];
        }

        list.Add(step);
    }

    private static IntentBand Intent(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, List<string> order, Dictionary<string, Hit> reached, HashSet<string> all)
    {
        var records = order.Where(k => nodes[k].Key.Kind == NodeKind.Record).Select(k => Record(graph, nodes[k], reached[k].Depth, all)).ToList();
        var decisions = order.Where(k => nodes[k].Key.Kind == NodeKind.Decision).Select(k => Decision(graph, nodes[k], reached[k].Depth, all)).ToList();
        return new IntentBand(records, decisions, new IntentCounts(records.Count, decisions.Count, Orphaned: null));
    }

    private static List<NodeKey> Targets(GraphSnapshot graph, NodeKey from, EdgeType type, HashSet<string> all) =>
        [.. graph.Edges.Where(e => e.Type == type && e.From.Value == from.Value && all.Contains(e.To.Value)).Select(e => e.To)
            .DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];

    private static LensRecord Record(GraphSnapshot graph, GraphNode n, int depth, HashSet<string> all) =>
        new(n.Key, n.Label, LensIntent.Source(n.Key), Targets(graph, n.Key, EdgeType.Mentions, all), Results.Trust.Untrusted, CitationState.Unchecked, [], NodeKind.Record) { Depth = depth };

    private static LensDecision Decision(GraphSnapshot graph, GraphNode n, int depth, HashSet<string> all) =>
        LensIntent.Decision(graph, n, Targets(graph, n.Key, EdgeType.Decides, all)) with { Depth = depth };

    private static ProductBand Product(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, List<string> order, Dictionary<string, Hit> reached)
    {
        var product = order.Where(k => IsProduct(nodes[k].Key.Kind)).ToList();
        var own = product.Where(k => reached[k].Depth == 0).Select(k => LensScope.Item(nodes[k]) with { Depth = 0 }).ToList();
        var neighbors = product.Where(k => reached[k].Depth > 0).Select(k => Reach(nodes[k], reached[k])).ToList();
        return LensProduct.Band(own, neighbors, [], LensProduct.Edges(graph, product.ToHashSet(StringComparer.Ordinal)));
    }

    private static LensReach Reach(GraphNode n, Hit hit)
    {
        var item = LensScope.Item(n);
        return new LensReach(item.Node, item.Label, item.Kind, item.Provenance, item.Confidence, item.Confirmed, item.Trust, hit.Depth, hit.Path);
    }

    private static CodeBand Code(GraphSnapshot graph, Dictionary<string, GraphNode> nodes, List<string> order, Dictionary<string, Hit> reached, HashSet<string> all)
    {
        var codes = order.Where(k => nodes[k].Key.Kind == NodeKind.Code).ToList();
        var linked = LensCode.Files(graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tests && all.Contains(e.To.Value) && nodes.ContainsKey(e.From.Value) && nodes[e.From.Value].Key.Kind == NodeKind.Code && reached.ContainsKey(e.From.Value)))
            .ToDictionary(f => "code:" + f.Path, StringComparer.Ordinal);
        var files = codes.Select(k => (linked.TryGetValue(k, out var f) ? f : Bare(nodes[k])) with { Depth = reached[k].Depth }).ToList();
        var own = order.Where(k => reached[k].Depth == 0).Select(NodeKey.From);
        return LensCode.Band(files, LensCode.NoCode(graph, own));
    }

    private static LensFile Bare(GraphNode n)
    {
        var confirmed = LensScope.Confirmed(n.Provenance);
        return new LensFile(n.Key.Name, [], [], n.Provenance, n.Confidence, confirmed, Suggested: false, LinkId: null, n.Key.Name, NodeKind.Code, TrustPolicy.ForNode(n, confirmed));
    }
}
