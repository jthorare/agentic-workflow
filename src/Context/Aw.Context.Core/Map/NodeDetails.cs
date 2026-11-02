using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Map;

/// <summary>
/// The extras 'context show' adds to a node and its edges, read from the map the caller sees and nothing else: where the node
/// came from (a record node and a decision's quoted record by key, a code node by path, any other node by its first evidence),
/// a short description (a decision's quote) and the kind and label of the other end of each edge. The map keeps no record text
/// and no links, so a record node has no description, and this type never makes a url: <see cref="TargetOf"/> says what a node's source points at and the caller, which can read the record store and the repo's
/// remote, passes the link it found to <see cref="Node"/> (#1702).
/// </summary>
public sealed class NodeDetails
{
    /// <summary>The most characters a description keeps.</summary>
    public const int MaxDescription = 280;

    private readonly MapText _text;
    private readonly Dictionary<string, GraphNode> _nodes;
    private readonly Dictionary<string, DecisionRow> _decisions;
    private readonly IReadOnlyDictionary<string, string?> _repos;
    private readonly string? _mapRepo;

    /// <summary>Reads the graph's nodes and decisions once, so each edge costs a lookup.</summary>
    /// <param name="graph">The graph the caller sees.</param>
    /// <param name="text">This call's text rules.</param>
    /// <param name="mapRepo">The repo the map version was built for, or null (<see cref="NodeRepos"/>).</param>
    public NodeDetails(GraphSnapshot graph, MapText text, string? mapRepo = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _mapRepo = mapRepo;
        _repos = NodeRepos.Index(graph, mapRepo);
        _nodes = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _decisions = graph.Decisions.GroupBy(d => d.ToKey().Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>The node with its source and description.</summary>
    /// <param name="node">The node.</param>
    /// <param name="sourceUrl">The link to the node's source, already checked by the caller, or null.</param>
    /// <returns>The detail.</returns>
    public NodeDetail Node(GraphNode node, Uri? sourceUrl = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        var decision = _decisions.GetValueOrDefault(node.Key.Value);
        var description = decision is null ? null : _text.Excerpt(decision.Quote, Results.Trust.Untrusted, MaxDescription);
        return NodeDetail.Of(_text.Fact(node) with { Repo = _repos.GetValueOrDefault(node.Key.Value) }, SourceOf(node, decision) is { } source ? source with { Url = sourceUrl?.OriginalString ?? source.Url } : null, description);
    }

    /// <summary>The edge with the kind and label of the node at its other end.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="other">The key at the other end.</param>
    /// <returns>The detail.</returns>
    public EdgeDetail Edge(EdgeFact edge, NodeKey other) =>
        EdgeDetail.Of(edge, other.Kind, _nodes.TryGetValue(other.Value, out var node) ? _text.Fact(node).Label : null);

    /// <summary>What the node's source points at: the record a record node, a decision or a node's first evidence names, or the file of a code node or a 'repo:' evidence.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The target; null when the node has no source.</returns>
    public SourceTarget? TargetOf(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var decision = _decisions.GetValueOrDefault(node.Key.Value);
        return node.Key.Kind switch
        {
            NodeKind.Record => TargetOfRecord(node.Key.Name),
            NodeKind.Code => SourceTarget.OfFile(_repos.GetValueOrDefault(node.Key.Value) ?? _mapRepo, node.Key.Name),
            NodeKind.Decision when decision is not null => TargetOfRecord(decision.Record.Value),
            NodeKind.Decision or NodeKind.Feature or NodeKind.Surface or NodeKind.Action or NodeKind.Thing or NodeKind.Flow or NodeKind.Concept =>
                node.EvidenceOrEmpty.Count > 0 ? TargetOfRecord(node.EvidenceOrEmpty[0].Key.Value) : null,
            _ => null,
        };
    }

    private static SourceTarget? TargetOfRecord(string record)
    {
        if (record.StartsWith("repo:", StringComparison.Ordinal))
        {
            var slash = record.IndexOf('/', "repo:".Length);
            return slash > "repo:".Length && slash < record.Length - 1 ? SourceTarget.OfFile(record["repo:".Length..slash], record[(slash + 1)..]) : null;
        }

        return RecordKey.TryFrom(record, out var key) ? SourceTarget.OfRecord(key) : null;
    }

    private NodeSource? SourceOf(GraphNode node, DecisionRow? decision) => node.Key.Kind switch
    {
        NodeKind.Record => ForRecord(node.Key.Name, withTitle: false),
        NodeKind.Code => new NodeSource("file", _text.Line(node.Key.Name)),
        NodeKind.Decision when decision is not null => ForRecord(decision.Record.Value, withTitle: true),
        NodeKind.Decision or NodeKind.Feature or NodeKind.Surface or NodeKind.Action or NodeKind.Thing or NodeKind.Flow or NodeKind.Concept => FromEvidence(node),
        _ => null,
    };

    private NodeSource? FromEvidence(GraphNode node) =>
        node.EvidenceOrEmpty.Count > 0 ? ForRecord(node.EvidenceOrEmpty[0].Key.Value, withTitle: true) : null;

    private NodeSource ForRecord(string record, bool withTitle)
    {
        var title = withTitle && _nodes.TryGetValue("record:" + record, out var node) ? _text.Fact(node).Label : null;
        return new NodeSource(KindOf(record), _text.Line(record), title);
    }

    private static string KindOf(string record) =>
        record.StartsWith("linear:issue/", StringComparison.Ordinal) ? "ticket"
        : record.StartsWith("linear:doc/", StringComparison.Ordinal) || record.StartsWith("repo:", StringComparison.Ordinal) ? "doc"
        : record.StartsWith("linear:project/", StringComparison.Ordinal) ? "project"
        : record.StartsWith("linear:update/", StringComparison.Ordinal) ? "update"
        : record[..record.IndexOf(':', StringComparison.Ordinal)];
}
