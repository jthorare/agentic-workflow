using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The nodes one concept opens on the whole map: its members, then two levels below them, each level the nodes on the other end of an edge (either way, 'belongs-to' aside) from the level before. A
/// feature, record or decision found on the way is left out unless it is a member (those of other concepts have their own), and so is every concept node but the one opened.
/// </summary>
internal static class ConceptScope
{
    private const int Levels = 2;

    /// <summary>The keys of the nodes <paramref name="concept"/> opens, the concept's own node first when the map has one.</summary>
    /// <param name="index">The graph's concepts.</param>
    /// <param name="edges">The graph's edges between known nodes.</param>
    /// <param name="concept">A concept key, or 'concept:other'.</param>
    /// <returns>The keys (ordinal), empty for a concept with no members and no node.</returns>
    internal static HashSet<string> Nodes(ConceptIndex index, IReadOnlyList<GraphEdge> edges, string concept)
    {
        var near = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges.Where(e => e.Type != EdgeType.BelongsTo))
        {
            Link(near, edge.From.Value, edge.To.Value);
            Link(near, edge.To.Value, edge.From.Value);
        }

        var members = index.MembersOf(concept);
        var all = new HashSet<string>(members, StringComparer.Ordinal);
        var level = members;
        for (var depth = 0; depth < Levels; depth++)
        {
            level = [.. level.SelectMany(k => near.TryGetValue(k, out var others) ? others : []).Where(k => !ConceptIndex.IsMember(index.Known[k].Key.Kind) && index.Known[k].Key.Kind != NodeKind.Concept && all.Add(k))];
        }

        if (index.Known.ContainsKey(concept))
        {
            _ = all.Add(concept);
        }

        return all;
    }

    private static void Link(Dictionary<string, List<string>> near, string from, string to)
    {
        if (!near.TryGetValue(from, out var list))
        {
            near[from] = list = [];
        }

        list.Add(to);
    }
}
