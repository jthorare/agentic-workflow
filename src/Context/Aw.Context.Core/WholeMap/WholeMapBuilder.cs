using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.WholeMap;

/// <summary>Builds the whole-map answer from a graph and its areas (Part D, §2.1). Pure apart from the text policy it writes labels with.</summary>
public static class WholeMapBuilder
{
    private const string OverBudget = "over-budget";

    /// <summary>The answer for one graph.</summary>
    /// <param name="ask">The map version, the detail, the area to open and the keys to place.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="areas">Its areas.</param>
    /// <param name="rules">The limits.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, or SND-CONTEXT-200 when the area to open is not in the map.</returns>
    public static Result<WholeMapView> Build(WholeMapAsk ask, GraphSnapshot graph, AreaIndex areas, WholeMapRules rules, MapText text)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(text);
        var known = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => known.ContainsKey(e.From.Value) && known.ContainsKey(e.To.Value)).ToList();
        var (listed, truncated) = ListAreas(known, areas, rules, text);
        var basis = ask.Basis;
        var overview = new WholeMapView(1, basis.MapVersion, basis.Draft, basis.AsOf, basis.Repo, WholeMapDetail.Areas, Counts(known.Values, edges.Count), listed, AreaEdges(edges, areas, listed.Select(a => a.Key).ToHashSet(StringComparer.Ordinal)), truncated)
        {
            MapCommit = basis.MapCommit,
            Located = Located(ask.Locate, areas, text),
        };
        if (ask.Area is not null)
        {
            return Opened(ask.Area, overview, (known, edges), (areas, rules, text));
        }

        if (ask.Detail == WholeMapDetail.Areas)
        {
            return overview;
        }

        return known.Count > rules.NodeBudget
            ? overview with { Fallback = OverBudget }
            : overview with { Detail = WholeMapDetail.Nodes, Nodes = Nodes(known.Values, areas, text), Edges = Edges(edges) };
    }

    // One area on its own: its nodes in kind and key order up to the cap, and the edges among those.
    private static Result<WholeMapView> Opened(string key, WholeMapView overview, (Dictionary<string, GraphNode> Known, List<GraphEdge> Edges) graph, (AreaIndex Areas, WholeMapRules Rules, MapText Text) how)
    {
        var members = graph.Known.Values.Where(n => how.Areas.ByNode[n.Key.Value].Area == key).ToList();
        if (members.Count == 0)
        {
            return ContextErrors.AreaUnknown.With("that area isn't in this map (fix: read the areas from 'context map whole' first, and name one of their keys)");
        }

        var cut = members.Count > how.Rules.AreaCap;
        var shown = members.OrderBy(n => n.Key.Kind).ThenBy(n => n.Key.Value, StringComparer.Ordinal).Take(how.Rules.AreaCap).ToList();
        var shownKeys = shown.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var memberKeys = members.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return overview with
        {
            Detail = WholeMapDetail.Nodes,
            Nodes = Nodes(shown, how.Areas, how.Text),
            Edges = Edges([.. graph.Edges.Where(e => shownKeys.Contains(e.From.Value) && shownKeys.Contains(e.To.Value))]),
            Area = new WholeMapAreaInfo(key, cut, Counts(members, graph.Edges.Count(e => memberKeys.Contains(e.From.Value) && memberKeys.Contains(e.To.Value)))),
            Truncated = overview.Truncated || cut,
        };
    }

    private static string KindName(NodeKind kind) => kind.ToString().ToLowerInvariant();

    private static WholeMapCounts Counts(IEnumerable<GraphNode> nodes, int edges)
    {
        var list = nodes.ToList();
        return new WholeMapCounts(list.Count, edges, Enum.GetValues<NodeKind>().ToDictionary(KindName, k => list.Count(n => n.Key.Kind == k), StringComparer.Ordinal));
    }

    private static (IReadOnlyList<WholeMapArea> Areas, bool Truncated) ListAreas(Dictionary<string, GraphNode> known, AreaIndex areas, WholeMapRules rules, MapText text)
    {
        List<WholeMapArea> all = [.. known.Values.GroupBy(n => areas.ByNode[n.Key.Value].Area, StringComparer.Ordinal).Select(g => Area(g.Key, [.. g], known, text))];
        var truncated = all.Count > rules.AreasCap;
        var chosen = truncated ? all.OrderByDescending(a => a.Members).ThenBy(a => a.Key, StringComparer.Ordinal).Take(rules.AreasCap) : all;
        return ([.. chosen.OrderBy(a => a.Key, StringComparer.Ordinal)], truncated);
    }

    private static WholeMapArea Area(string key, List<GraphNode> members, Dictionary<string, GraphNode> known, MapText text)
    {
        var kinds = Enum.GetValues<NodeKind>().Select(k => (Name: KindName(k), Count: members.Count(n => n.Key.Kind == k))).Where(k => k.Count > 0).ToDictionary(k => k.Name, k => k.Count, StringComparer.Ordinal);
        if (key == AreaIndex.UnassignedKey || !known.TryGetValue(key, out var root))
        {
            return new WholeMapArea(key, AreaIndex.UnassignedLabel, Results.Trust.Trusted, members.Count, kinds, key == AreaIndex.UnassignedKey);
        }

        var trust = TrustPolicy.ForNode(root, root.Provenance == Provenance.Human);
        return new WholeMapArea(key, text.Label(root.Label, trust), trust, members.Count, kinds, Unassigned: false);
    }

    private static List<WholeMapAreaEdge> AreaEdges(List<GraphEdge> edges, AreaIndex areas, HashSet<string> listed) =>
        [.. edges
            .Select(e => (From: areas.ByNode[e.From.Value].Area, To: areas.ByNode[e.To.Value].Area, e.Type, Confirmed: e.Provenance == Provenance.Human))
            .Where(e => e.From != e.To && listed.Contains(e.From) && listed.Contains(e.To))
            .GroupBy(e => (e.From, e.To, e.Type))
            .Select(g => new WholeMapAreaEdge(g.Key.From, g.Key.To, g.Key.Type, g.Count(), g.Count(e => e.Confirmed)))
            .OrderBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal).ThenBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal)];

    private static List<WholeMapNode> Nodes(IEnumerable<GraphNode> nodes, AreaIndex areas, MapText text) =>
        [.. nodes
            .Select(n => (Node: n, Area: areas.ByNode[n.Key.Value]))
            .OrderBy(p => p.Area.Area, StringComparer.Ordinal).ThenBy(p => p.Node.Key.Kind).ThenBy(p => p.Node.Key.Value, StringComparer.Ordinal)
            .Select(p => Node(p.Node, p.Area, text))];

    private static WholeMapNode Node(GraphNode node, NodeArea area, MapText text)
    {
        var confirmed = node.Provenance == Provenance.Human;
        var trust = TrustPolicy.ForNode(node, confirmed);
        return new WholeMapNode(node.Key, text.Label(node.Label, trust), node.Key.Kind, area.Area, area.AlsoIn, trust, node.Provenance, node.Confidence, confirmed);
    }

    private static List<WholeMapEdge> Edges(List<GraphEdge> edges) =>
        [.. edges
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal)
            .Select(e => new WholeMapEdge(e.From, e.To, e.Type, LinkIds.For(e), e.Provenance, e.Confidence, e.Provenance == Provenance.Human))];

    private static List<WholeMapLocated>? Located(IReadOnlyList<string> keys, AreaIndex areas, MapText text) =>
        keys.Count == 0 ? null : [.. keys.Select(k => new WholeMapLocated(text.Line(k), areas.ByNode.TryGetValue(k, out var found) ? found.Area : null))];
}
