using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// Who belongs to which concept in one graph: each feature, record and decision with its concepts (a member may be in several), each concept with its members, and the reserved
/// 'concept:other' with every member no concept takes, so nothing is dropped. An edge to a node the graph lacks, or to 'concept:other' itself, is not read.
/// </summary>
internal sealed class ConceptIndex
{
    private readonly Dictionary<string, List<string>> _conceptsOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _membersOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _implementedBy = new(StringComparer.Ordinal);

    private ConceptIndex(IReadOnlyDictionary<string, GraphNode> known, IReadOnlyList<GraphEdge> edges)
    {
        Known = known;
        Concepts = SortedConcepts(known);
        _membersOf[ConceptKeys.Other] = [];
        foreach (var concept in Concepts)
        {
            _membersOf[concept.Key.Value] = [];
        }

        JoinMembers(edges);
        JoinOther();
        IndexImplementers(edges);
    }

    /// <summary>The nodes by key.</summary>
    internal IReadOnlyDictionary<string, GraphNode> Known { get; }

    /// <summary>The concept nodes other than 'concept:other', by name then key.</summary>
    internal IReadOnlyList<GraphNode> Concepts { get; }

    /// <summary>The index of a graph's nodes (by key) and edges.</summary>
    internal static ConceptIndex Of(IReadOnlyDictionary<string, GraphNode> known, IReadOnlyList<GraphEdge> edges) => new(known, edges);

    /// <summary>Whether a node of this kind can belong to a concept.</summary>
    internal static bool IsMember(NodeKind kind) => kind is NodeKind.Feature or NodeKind.Record or NodeKind.Decision;

    /// <summary>The keys of the concepts holding a member (ordinal), or none for any other node.</summary>
    internal IReadOnlyList<string> ConceptsOf(string key) => _conceptsOf.TryGetValue(key, out var list) ? list : [];

    /// <summary>The member keys of a concept or of 'concept:other' (by kind, then key), or none for a key that is neither.</summary>
    internal IReadOnlyList<string> MembersOf(string concept) => _membersOf.TryGetValue(concept, out var list) ? list : [];

    /// <summary>Whether <paramref name="key"/> names a concept the map has, or the reserved 'concept:other' (which needs no node).</summary>
    internal bool Has(string key) => _membersOf.ContainsKey(key);

    /// <summary>The distinct code nodes the member features of <paramref name="members"/> are implemented by.</summary>
    internal int CodeOf(IEnumerable<string> members) =>
        members.Where(m => Known[m].Key.Kind == NodeKind.Feature).SelectMany(m => _implementedBy.TryGetValue(m, out var code) ? code : []).Distinct(StringComparer.Ordinal).Count();

    private static List<GraphNode> SortedConcepts(IReadOnlyDictionary<string, GraphNode> known) =>
        [.. known.Values.Where(n => n.Key.Kind == NodeKind.Concept && n.Key.Value != ConceptKeys.Other)
            .OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Key.Value, StringComparer.Ordinal)];

    private void JoinMembers(IReadOnlyList<GraphEdge> edges)
    {
        foreach (var edge in edges.Where(IsMemberEdge))
        {
            Join(edge.From.Value, edge.To.Value);
        }
    }

    private bool IsMemberEdge(GraphEdge edge) =>
        edge.Type == EdgeType.BelongsTo && IsMember(edge.From.Kind) && Known.ContainsKey(edge.From.Value) && _membersOf.ContainsKey(edge.To.Value) && edge.To.Value != ConceptKeys.Other;

    private void JoinOther()
    {
        foreach (var key in Known.Values.Where(n => IsMember(n.Key.Kind) && !_conceptsOf.ContainsKey(n.Key.Value)).Select(n => n.Key.Value))
        {
            Join(key, ConceptKeys.Other);
        }
    }

    private void IndexImplementers(IReadOnlyList<GraphEdge> edges)
    {
        foreach (var edge in edges.Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature && Known.ContainsKey(e.From.Value)))
        {
            Append(_implementedBy, edge.To.Value, edge.From.Value);
        }
    }

    private void Join(string member, string concept)
    {
        if (_conceptsOf.TryGetValue(member, out var held) && held.Contains(concept, StringComparer.Ordinal))
        {
            return;
        }

        Append(_conceptsOf, member, concept);
        Append(_membersOf, concept, member);
        _conceptsOf[member].Sort(StringComparer.Ordinal);
        _membersOf[concept].Sort((a, b) => Compare(Known[a].Key, Known[b].Key));
    }

    private static int Compare(NodeKey a, NodeKey b)
    {
        var byKind = a.Kind.CompareTo(b.Kind);
        return byKind != 0 ? byKind : string.CompareOrdinal(a.Value, b.Value);
    }

    private static void Append(Dictionary<string, List<string>> index, string key, string value)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(value);
    }
}
