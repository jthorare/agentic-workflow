using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// Builds the whole-map answer from a graph and its areas (Part D, §2.1). Pure apart from the text policy it writes labels with. Concept nodes are a band of their own: no area lists them,
/// no area edge reaches them and no area counts them, but the counts and the node list hold them.
/// </summary>
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
        if (ask.Area is not null && ask.Concept is not null)
        {
            return ContextErrors.QueryInvalid.With("'area' and 'concept' each open one part of the map (fix: ask for one of them)");
        }

        var known = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => known.ContainsKey(e.From.Value) && known.ContainsKey(e.To.Value)).ToList();
        var concepts = ConceptIndex.Of(known, edges);
        var overview = Overview(ask, (known, edges), (areas, concepts), (rules, text));
        if (ask.Area is not null)
        {
            return Opened(ask.Area, overview, (known, edges), (areas, rules, text, ask.RepoOf));
        }

        if (ask.Concept is not null)
        {
            return OpenedConcept(ask.Concept, overview, (concepts, edges), (areas, rules, text, ask.RepoOf));
        }

        if (ask.Detail == WholeMapDetail.Areas)
        {
            return overview;
        }

        return known.Count > rules.NodeBudget
            ? overview with { Fallback = OverBudget }
            : overview with { Detail = WholeMapDetail.Nodes, Nodes = Nodes(known.Values, areas, text, ask.RepoOf), Edges = Edges(edges) };
    }

    private static WholeMapView Overview(WholeMapAsk ask, (Dictionary<string, GraphNode> Known, List<GraphEdge> Edges) graph, (AreaIndex Areas, ConceptIndex Concepts) map, (WholeMapRules Rules, MapText Text) how)
    {
        var (listed, truncated) = ListAreas(graph.Known, map.Areas, how.Text);
        var basis = ask.Basis;
        var listedKeys = listed.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        return new WholeMapView(1, basis.MapVersion, basis.Draft, basis.AsOf, basis.Repo, WholeMapDetail.Areas, Counts(graph.Known.Values, graph.Edges.Count), listed, AreaEdges(graph.Edges, map.Areas, listedKeys), truncated)
        {
            MapCommit = basis.MapCommit,
            Located = Located(ask.Locate, map, how.Text),
            Repos = ask.Repos,
        };
    }

    // One area on its own: all its nodes in kind and key order, and the edges among them. Nothing is cut.
    private static Result<WholeMapView> Opened(string key, WholeMapView overview, (Dictionary<string, GraphNode> Known, List<GraphEdge> Edges) graph, (AreaIndex Areas, WholeMapRules Rules, MapText Text, IReadOnlyDictionary<string, string?>? RepoOf) how)
    {
        var members = graph.Known.Values.Where(n => n.Key.Kind != NodeKind.Concept && how.Areas.ByNode[n.Key.Value].Area == key).ToList();
        if (members.Count == 0)
        {
            return ContextErrors.AreaUnknown.With("that area isn't in this map (fix: read the areas from 'context map whole' first, and name one of their keys)");
        }

        var shown = members.OrderBy(n => n.Key.Kind).ThenBy(n => n.Key.Value, StringComparer.Ordinal).ToList();
        var shownKeys = shown.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var memberKeys = members.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return overview with
        {
            Detail = WholeMapDetail.Nodes,
            Nodes = Nodes(shown, how.Areas, how.Text, how.RepoOf),
            Edges = Edges([.. graph.Edges.Where(e => shownKeys.Contains(e.From.Value) && shownKeys.Contains(e.To.Value))]),
            Area = new WholeMapAreaInfo(key, Truncated: false, Counts(members, graph.Edges.Count(e => memberKeys.Contains(e.From.Value) && memberKeys.Contains(e.To.Value)))),
        };
    }

    // One concept on its own: its members and what lies two levels below them (see ConceptScope), in band, kind and key order, and the edges among them. Nothing is cut.
    private static Result<WholeMapView> OpenedConcept(string key, WholeMapView overview, (ConceptIndex Concepts, List<GraphEdge> Edges) graph, (AreaIndex Areas, WholeMapRules Rules, MapText Text, IReadOnlyDictionary<string, string?>? RepoOf) how)
    {
        if (!graph.Concepts.Has(key))
        {
            return ContextErrors.AreaUnknown.With("that concept isn't in this map (fix: read the concepts first, and name one of their ids)");
        }

        var scoped = ConceptScope.Nodes(graph.Concepts, graph.Edges, key);
        var shown = scoped.Select(k => graph.Concepts.Known[k]).OrderBy(n => n.Key.Kind == NodeKind.Concept ? 0 : 1).ThenBy(n => n.Key.Kind).ThenBy(n => n.Key.Value, StringComparer.Ordinal).ToList();
        var shownKeys = shown.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return overview with
        {
            Detail = WholeMapDetail.Nodes,
            Nodes = Nodes(shown, how.Areas, how.Text, how.RepoOf),
            Edges = Edges([.. graph.Edges.Where(e => shownKeys.Contains(e.From.Value) && shownKeys.Contains(e.To.Value))]),
        };
    }

    private static string KindName(NodeKind kind) => kind.ToString().ToLowerInvariant();

    private static WholeMapCounts Counts(IEnumerable<GraphNode> nodes, int edges)
    {
        var list = nodes.ToList();
        return new WholeMapCounts(list.Count, edges, Enum.GetValues<NodeKind>().ToDictionary(KindName, k => list.Count(n => n.Key.Kind == k), StringComparer.Ordinal));
    }

    private static (IReadOnlyList<WholeMapArea> Areas, bool Truncated) ListAreas(Dictionary<string, GraphNode> known, AreaIndex areas, MapText text)
    {
        List<WholeMapArea> all = [.. known.Values.Where(n => n.Key.Kind != NodeKind.Concept).GroupBy(n => areas.ByNode[n.Key.Value].Area, StringComparer.Ordinal).Select(g => Area(g.Key, [.. g], known, text))];
        return ([.. all.OrderBy(a => a.Key, StringComparer.Ordinal)], Truncated: false);
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
            .Where(e => e.From.Kind != NodeKind.Concept && e.To.Kind != NodeKind.Concept)
            .Select(e => (From: areas.ByNode[e.From.Value].Area, To: areas.ByNode[e.To.Value].Area, e.Type, Confirmed: e.Provenance == Provenance.Human))
            .Where(e => e.From != e.To && listed.Contains(e.From) && listed.Contains(e.To))
            .GroupBy(e => (e.From, e.To, e.Type))
            .Select(g => new WholeMapAreaEdge(g.Key.From, g.Key.To, g.Key.Type, g.Count(), g.Count(e => e.Confirmed)))
            .OrderBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal).ThenBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal)];

    /// <summary>The repos a node index names, with how many nodes each holds, sorted by name; nodes of no single repo count for none.</summary>
    /// <param name="repoOf">Each node's repo by node key (<see cref="NodeRepos.Index"/>).</param>
    /// <returns>The repos.</returns>
    public static IReadOnlyList<WholeMapRepo> Repos(IReadOnlyDictionary<string, string?> repoOf)
    {
        ArgumentNullException.ThrowIfNull(repoOf);
        return [.. repoOf.Values.OfType<string>().GroupBy(r => r, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new WholeMapRepo(g.Key, g.Count()))];
    }

    private static List<WholeMapNode> Nodes(IEnumerable<GraphNode> nodes, AreaIndex areas, MapText text, IReadOnlyDictionary<string, string?>? repoOf) =>
        [.. nodes
            .Select(n => (Node: n, Area: AreaOf(areas, n)))
            .OrderBy(p => p.Area.Area, StringComparer.Ordinal).ThenBy(p => p.Node.Key.Kind).ThenBy(p => p.Node.Key.Value, StringComparer.Ordinal)
            .Select(p => Node(p.Node, p.Area, text, repoOf?.GetValueOrDefault(p.Node.Key.Value)))];

    private static NodeArea AreaOf(AreaIndex areas, GraphNode node) => node.Key.Kind == NodeKind.Concept ? new NodeArea(AreaIndex.ConceptBandKey, []) : areas.ByNode[node.Key.Value];

    private static WholeMapNode Node(GraphNode node, NodeArea area, MapText text, string? repo)
    {
        var confirmed = node.Provenance == Provenance.Human;
        var trust = TrustPolicy.ForNode(node, confirmed);
        return new WholeMapNode(node.Key, text.Label(node.Label, trust), node.Key.Kind, area.Area, area.AlsoIn, trust, node.Provenance, node.Confidence, confirmed) { Repo = repo };
    }

    private static List<WholeMapEdge> Edges(List<GraphEdge> edges) =>
        [.. edges
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => EdgeRules.Name(e.Type), StringComparer.Ordinal)
            .Select(e => new WholeMapEdge(e.From, e.To, e.Type, LinkIds.For(e), e.Provenance, e.Confidence, e.Provenance == Provenance.Human))];

    private static List<WholeMapLocated>? Located(IReadOnlyList<string> keys, (AreaIndex Areas, ConceptIndex Concepts) map, MapText text) =>
        keys.Count == 0 ? null : [.. keys.Select(k => new WholeMapLocated(text.Line(k), map.Areas.ByNode.TryGetValue(k, out var found) ? found.Area : null) { Concept = [.. map.Concepts.ConceptsOf(k).Select(text.Line)] })];
}
