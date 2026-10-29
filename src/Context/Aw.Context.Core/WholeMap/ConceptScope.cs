using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The nodes one concept opens on the whole map: its members, then two levels below them, each level the nodes on the other end of an edge (either way, 'belongs-to' aside) from the level before. A
/// feature, record or decision found on the way is left out unless it is a member (those of other concepts have their own), and so is every concept node but the one opened. After those levels comes the product
/// structure under what was reached: the surfaces and actions that read or write a reached thing, then everything those, and the reached features, things and flows, point to forward through 'on', 'part-of',
/// 'step-of', 'reads', 'writes', 'depends-on' and 'moves-to' (never backwards, so a hub surface does not pull in its other actions), up to <see cref="ClosureCap"/> nodes. Code nodes are never added by it.
/// </summary>
internal static class ConceptScope
{
    private const int Levels = 2;

    // The most nodes the product-structure closure adds, in ordinal key order, so a thing written by hundreds of actions cannot turn a concept's map into the whole map.
    private const int ClosureCap = 200;

    private static readonly HashSet<EdgeType> _forward = [EdgeType.On, EdgeType.PartOf, EdgeType.StepOf, EdgeType.Reads, EdgeType.Writes, EdgeType.DependsOn, EdgeType.MovesTo];

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

        AddProductClosure(index, edges, all);

        if (index.Known.ContainsKey(concept))
        {
            _ = all.Add(concept);
        }

        return all;
    }

    private static bool IsProduct(NodeKind kind) => kind is NodeKind.Surface or NodeKind.Action or NodeKind.Thing or NodeKind.Flow;

    private static void AddProductClosure(ConceptIndex index, IReadOnlyList<GraphEdge> edges, HashSet<string> all)
    {
        var room = ClosureCap;

        // One hop back from each reached thing to the surfaces and actions that read or write it.
        var touching = edges.Where(e => e.Type is EdgeType.Reads or EdgeType.Writes && e.From.Kind is NodeKind.Surface or NodeKind.Action && e.To.Kind == NodeKind.Thing && all.Contains(e.To.Value))
            .Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Where(k => !all.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var frontier = TakeUpTo(touching, all, ref room);

        // Then forward only, until nothing is added; a member feature is already in, a non-member one is never entered.
        var forward = edges.Where(e => _forward.Contains(e.Type) && IsProduct(e.To.Kind)).ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        frontier.AddRange(all.Where(k => IsProduct(index.Known[k].Key.Kind)));
        while (frontier.Count > 0 && room > 0)
        {
            var next = frontier.Distinct(StringComparer.Ordinal).SelectMany(k => forward[k]).Distinct(StringComparer.Ordinal).Where(k => !all.Contains(k)).Order(StringComparer.Ordinal).ToList();
            frontier = TakeUpTo(next, all, ref room);
        }
    }

    private static List<string> TakeUpTo(List<string> sorted, HashSet<string> all, ref int room)
    {
        var taken = sorted.Take(room).ToList();
        taken.ForEach(k => all.Add(k));
        room -= taken.Count;
        return taken;
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
