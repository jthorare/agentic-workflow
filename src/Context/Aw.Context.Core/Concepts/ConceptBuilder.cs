using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>Builds the concepts answers from a graph (the read side of the concept layer). Pure apart from the text policy it writes names with.</summary>
public static class ConceptBuilder
{
    private const string OtherName = "Other";
    private const int SummaryMax = 300;

    /// <summary>One page of the concepts of a graph.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="query">The filter and the page.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, or SND-CONTEXT-006 for a bad page.</returns>
    public static Result<ConceptsView> Build(ConceptsBasis basis, ConceptsQuery query, GraphSnapshot graph, MapText text)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        var slice = ConceptPages.Slice(basis.MapVersion, query.Page);
        if (slice.IsError)
        {
            return slice.Error;
        }

        var (known, edges) = Known(graph);
        var index = ConceptIndex.Of(known, edges);
        var matching = index.Concepts.Where(n => Matches(index, n, query.Q)).ToList();
        var (offset, size) = slice.Value;
        var page = matching.Skip(offset).Take(size).ToList();
        var end = offset + page.Count;
        var other = new ConceptOther(NodeKey.From(ConceptKeys.Other), OtherName, Size(index, index.MembersOf(ConceptKeys.Other)));
        var rows = page.Select(n => Row(index, n, text)).ToList();
        return new ConceptsView(1, basis.Repo, basis.MapVersion, index.Concepts.Count == 0 ? ConceptStates.Empty : ConceptStates.Ok, Code: null, rows, other, Links(index, edges, page), ConceptPages.Next(basis.MapVersion, end, matching.Count));
    }

    /// <summary>One page of one concept's members.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="query">The concept and the page.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, SND-CONTEXT-006 for a bad page, or SND-CONTEXT-003 for a concept the map lacks.</returns>
    public static Result<ConceptMembersView> Members(ConceptsBasis basis, ConceptMembersQuery query, GraphSnapshot graph, MapText text)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        var slice = ConceptPages.Slice(basis.MapVersion, query.Page);
        if (slice.IsError)
        {
            return slice.Error;
        }

        var (known, edges) = Known(graph);
        var index = ConceptIndex.Of(known, edges);
        if (!index.Has(query.Concept.Value))
        {
            return ContextErrors.UnknownAnchor.With("that concept isn't in this map (fix: read the concepts first, and name one of their ids)");
        }

        var all = index.MembersOf(query.Concept.Value);
        var (offset, size) = slice.Value;
        var page = all.Skip(offset).Take(size).Select(k => Member(known[k], text)).ToList();
        return new ConceptMembersView(1, basis.Repo, basis.MapVersion, query.Concept, page, ConceptPages.Next(basis.MapVersion, offset + page.Count, all.Count));
    }

    private static (Dictionary<string, GraphNode> Known, List<GraphEdge> Edges) Known(GraphSnapshot graph)
    {
        var known = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return (known, [.. graph.Edges.Where(e => known.ContainsKey(e.From.Value) && known.ContainsKey(e.To.Value))]);
    }

    private static bool Matches(ConceptIndex index, GraphNode concept, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return true;
        }

        var needle = q.Trim();
        return Has(concept.Label, needle) || Has(Attribute(concept, "summary"), needle)
            || index.MembersOf(concept.Key.Value).Any(m => Has(index.Known[m].Label, needle));
    }

    private static bool Has(string? haystack, string needle) => haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    private static string? Attribute(GraphNode node, string name) =>
        node.Attributes is { ValueKind: JsonValueKind.Object } attributes && attributes.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ConceptRow Row(ConceptIndex index, GraphNode node, MapText text)
    {
        var trust = TrustPolicy.ForNode(node, node.Provenance == Provenance.Human);
        var summary = Attribute(node, "summary") is { Length: > 0 } written ? text.Excerpt(written, trust, SummaryMax) : string.Empty;
        var source = string.Equals(Attribute(node, "source"), "override", StringComparison.Ordinal) ? "override" : "derived";
        var citations = node.EvidenceOrEmpty.Select(e => new ConceptCitation(text.Line(e.Key.Value), e.Span)).ToList();
        return new ConceptRow(node.Key, text.Label(node.Label, trust), summary, source, Size(index, index.MembersOf(node.Key.Value)), citations);
    }

    private static ConceptSize Size(ConceptIndex index, IReadOnlyList<string> members) =>
        new(Count(index, members, NodeKind.Feature), Count(index, members, NodeKind.Record), Count(index, members, NodeKind.Decision), index.CodeOf(members));

    private static int Count(ConceptIndex index, IReadOnlyList<string> members, NodeKind kind) => members.Count(m => index.Known[m].Key.Kind == kind);

    // The member edges between two concepts, summed, for every pair with an end on the page; 'concept:other' takes part like any concept.
    private static List<ConceptLink> Links(ConceptIndex index, List<GraphEdge> edges, List<GraphNode> page)
    {
        var onPage = page.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var weights = new Dictionary<(string From, string To), int>();
        foreach (var edge in edges.Where(e => e.Type != EdgeType.BelongsTo))
        {
            foreach (var (from, to) in index.ConceptsOf(edge.From.Value).SelectMany(a => index.ConceptsOf(edge.To.Value).Where(b => a != b).Select(b => (a, b))).Where(p => onPage.Contains(p.a) || onPage.Contains(p.b)))
            {
                weights[(from, to)] = weights.GetValueOrDefault((from, to)) + 1;
            }
        }

        return [.. weights.OrderBy(w => w.Key.From, StringComparer.Ordinal).ThenBy(w => w.Key.To, StringComparer.Ordinal).Select(w => new ConceptLink(NodeKey.From(w.Key.From), NodeKey.From(w.Key.To), w.Value))];
    }

    private static ConceptMember Member(GraphNode node, MapText text) =>
        new(text.Line(node.Key.Value), node.Key.Kind.ToString().ToLowerInvariant(), text.Label(node.Label, TrustPolicy.ForNode(node, node.Provenance == Provenance.Human)));
}
