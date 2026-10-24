using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What an extraction found: nodes and edges, ready to join a draft.</summary>
/// <param name="Nodes">The nodes, by key.</param>
/// <param name="Edges">The edges, by ends and type.</param>
public sealed record ExtractedMap(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)
{
    /// <summary>Other keys that name one of the nodes (a glossary term's aliases).</summary>
    public IReadOnlyList<NodeAlias> Aliases { get; init; } = [];

    /// <summary>Nothing found.</summary>
    public static ExtractedMap Empty { get; } = new([], []);

    /// <summary>Both extractions together: a node or an edge that both hold is kept once, the first one's.</summary>
    /// <param name="other">The other extraction.</param>
    /// <returns>The union, ordered by key.</returns>
    public ExtractedMap Plus(ExtractedMap other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ExtractedMap(
            [.. Nodes.Concat(other.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal).OrderBy(n => n.Key.Value, StringComparer.Ordinal)],
            [.. Edges.Concat(other.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type)).OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type)])
        {
            Aliases = [.. Aliases.Concat(other.Aliases).DistinctBy(a => a.Alias.Value, StringComparer.Ordinal).OrderBy(a => a.Alias.Value, StringComparer.Ordinal)],
        };
    }
}
