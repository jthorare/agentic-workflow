using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.ScopeGraph;

/// <summary>
/// Builds the scoping graph (issue 1005) from the lenses over the anchors and the graph they were cut from: nodes ranked product first (the brief only re-ranks inside a band),
/// the edges with both ends in the set, and a limit row for whatever was cut. Pure; the role globs of #938 are applied when the map is extracted, so the map holds no shared-file implements edge for this to filter.
/// </summary>
public static class ScopeGraphBuilder
{
    private const int NodeOverheadBytes = 150;
    private const int EdgeOverheadBytes = 40;
    private const int MaxCitations = 5;

    /// <summary>Builds the graph.</summary>
    /// <param name="query">The anchors, range, brief and caps.</param>
    /// <param name="lenses">The (cleaned) lenses over the anchors.</param>
    /// <param name="graph">The graph the lenses were cut from, for labels, citations and edges.</param>
    /// <param name="hops">The cascade hops that were walked.</param>
    /// <param name="text">Cleans a label.</param>
    /// <returns>The view.</returns>
    public static ScopeGraphView Build(ScopeGraphQuery query, LensesSetView lenses, GraphSnapshot graph, CascadeHops hops, MapText text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(lenses);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        var byKey = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        var ordered = Order(Candidates(lenses, byKey, text), query.Brief);
        var kept = ordered.Take(Math.Max(query.MaxNodes, 1)).ToList();
        var nodes = Number(kept, byKey);
        var edges = EdgesAmong(graph, nodes);
        while (nodes.Count > 1 && Bytes(nodes, edges) > query.MaxBytes)
        {
            nodes = [.. nodes.Take(nodes.Count - 1)];
            edges = EdgesAmong(graph, nodes);
        }

        var limits = new List<ScopeGraphLimit>();
        if (nodes.Count < ordered.Count)
        {
            limits.Add(new ScopeGraphLimit("cap", string.Create(CultureInfo.InvariantCulture, $"{ordered.Count - nodes.Count} lowest-ranked of {ordered.Count} nodes (cap: {query.MaxNodes} nodes, {query.MaxBytes} bytes)")));
        }

        var outside = OutsideEdges(graph, nodes);
        if (outside > 0)
        {
            limits.Add(new ScopeGraphLimit("edges-outside", string.Create(CultureInfo.InvariantCulture, $"{outside} edges to nodes outside this graph; context_neighbors on a node shows them")));
        }

        return new ScopeGraphView(1, lenses.MapVersion, [.. lenses.Anchors.Select(a => a.Key.Value)], lenses.Depth, HopNames(hops), nodes, edges, limits);
    }

    private static List<ScopeGraphCandidate> Candidates(LensesSetView lenses, Dictionary<string, GraphNode> byKey, MapText text)
    {
        var found = new List<ScopeGraphCandidate>();
        found.AddRange(lenses.Anchors.Select(a => new ScopeGraphCandidate(a.Key.Value, a.Label, "product", "own", 0, 0)));
        found.AddRange(lenses.Bands.Product.Own.Select(i => new ScopeGraphCandidate(i.Node.Value, i.Label, "product", "own", 1, 0)));
        found.AddRange(lenses.Bands.Product.Neighbors.Select(r => new ScopeGraphCandidate(r.Node.Value, r.Label, "product", "neighbors", 2, r.Depth)));
        found.AddRange(lenses.Bands.Product.Affects.Select(r => new ScopeGraphCandidate(r.Node.Value, r.Label, "product", "affects", 3, r.Depth)));
        found.AddRange(lenses.Bands.Intent.Records.Select(r => new ScopeGraphCandidate(r.Key.Value, r.Label, "related", "intent", 4, 0)));
        found.AddRange(lenses.Bands.Intent.Decisions.Select(d => new ScopeGraphCandidate(d.Node.Value, d.Label, "related", "intent", 4, 0)));
        found.AddRange(lenses.Bands.Code.Files.Select(f => CodeCandidate(f, byKey, text)));
        return [.. found.DistinctBy(c => c.Id, StringComparer.Ordinal).Select(c => ConceptSummary(c, byKey, text))];
    }

    // A concept node is summarized by its summary attribute (issue 1315); without one it keeps the label.
    private static ScopeGraphCandidate ConceptSummary(ScopeGraphCandidate candidate, Dictionary<string, GraphNode> byKey, MapText text)
    {
        if (!byKey.TryGetValue(candidate.Id, out var node) || node.Key.Kind != NodeKind.Concept || NodeAttributes.String(node, "summary") is not { Length: > 0 } written)
        {
            return candidate;
        }

        return candidate with { Summary = text.Excerpt(written, TrustPolicy.ForNode(node, node.Provenance == Provenance.Human), NodeAttributes.SummaryMax) };
    }

    private static ScopeGraphCandidate CodeCandidate(LensFile file, Dictionary<string, GraphNode> byKey, MapText text)
    {
        var id = "code:" + file.Path;
        var label = byKey.TryGetValue(id, out var node) ? text.Label(node.Label, file.Trust) : file.Label;
        return new ScopeGraphCandidate(id, label, "code", "code", 5, file.Depth ?? 0);
    }

    // Product first: the lens order decides, and the brief only re-orders inside a group (anchors keep their place).
    private static List<ScopeGraphCandidate> Order(List<ScopeGraphCandidate> found, string? brief)
    {
        var terms = Terms(brief);
        return [.. found
            .OrderBy(c => c.Group)
            .ThenBy(c => c.Group == 0 ? 0 : c.Distance)
            .ThenByDescending(c => c.Group == 0 ? 0 : Score(c.Summary, terms))
            .ThenBy(c => c.Id, StringComparer.Ordinal)];
    }

    private static List<string> Terms(string? brief) =>
        brief is null ? [] : [.. brief.Split([' ', '\n', '\t', ',', '.', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 4).Select(t => t.ToLowerInvariant()).Distinct(StringComparer.Ordinal)];

    private static int Score(string summary, List<string> terms) => terms.Count(t => summary.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static List<ScopeGraphNode> Number(List<ScopeGraphCandidate> kept, Dictionary<string, GraphNode> byKey)
    {
        var perLens = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodes = new List<ScopeGraphNode>(kept.Count);
        foreach (var (candidate, index) in kept.Select((c, i) => (c, i)))
        {
            perLens[candidate.Lens] = perLens.GetValueOrDefault(candidate.Lens) + 1;
            var item = string.Create(CultureInfo.InvariantCulture, $"L-{candidate.Lens}-{perLens[candidate.Lens]}");
            var scheme = candidate.Id[..Math.Max(candidate.Id.IndexOf(':', StringComparison.Ordinal), 0)];
            var path = candidate.Id.StartsWith("code:", StringComparison.Ordinal) ? candidate.Id["code:".Length..] : null;
            nodes.Add(new ScopeGraphNode(ScopeGraphNodeId.From(candidate.Id), scheme, path, Symbol: null, candidate.Summary, index + 1, candidate.Lens, item, candidate.Band, Citations(candidate.Id, byKey)));
        }

        return nodes;
    }

    private static List<ScopeGraphCitation> Citations(string id, Dictionary<string, GraphNode> byKey) =>
        byKey.TryGetValue(id, out var node) ? [.. node.EvidenceOrEmpty.Take(MaxCitations).Select(e => new ScopeGraphCitation(e.Key.Value, e.Span))] : [];

    private static List<ScopeGraphEdge> EdgesAmong(GraphSnapshot graph, List<ScopeGraphNode> nodes)
    {
        var ids = nodes.Select(n => n.Id.Value).ToHashSet(StringComparer.Ordinal);
        return [.. graph.Edges
            .Where(e => ids.Contains(e.From.Value) && ids.Contains(e.To.Value))
            .Select(e => new ScopeGraphEdge(ScopeGraphNodeId.From(e.From.Value), ScopeGraphNodeId.From(e.To.Value), EdgeRules.Name(e.Type)))
            .Distinct()];
    }

    private static int OutsideEdges(GraphSnapshot graph, List<ScopeGraphNode> nodes)
    {
        var ids = nodes.Select(n => n.Id.Value).ToHashSet(StringComparer.Ordinal);
        return graph.Edges.Count(e => ids.Contains(e.From.Value) != ids.Contains(e.To.Value));
    }

    // The JSON size, estimated from the text each node and edge carries.
    private static int Bytes(List<ScopeGraphNode> nodes, List<ScopeGraphEdge> edges) =>
        nodes.Sum(n => NodeOverheadBytes + n.Id.Value.Length + n.Kind.Length + (n.Path?.Length ?? 0) + n.Summary.Length + n.Citations.Sum(c => c.Key.Length + (c.Span?.Length ?? 0) + 20))
        + edges.Sum(e => EdgeOverheadBytes + e.From.Value.Length + e.To.Value.Length + e.Type.Length);

    private static List<string> HopNames(CascadeHops hops) =>
        [.. new[] { CascadeHops.State, CascadeHops.Flow, CascadeHops.Dependency, CascadeHops.Component }.Where(h => hops.HasFlag(h)).Select(h => h.ToString().ToLowerInvariant())];
}
