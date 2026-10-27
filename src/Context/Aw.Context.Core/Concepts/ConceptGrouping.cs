using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Merges the smallest groups into the group they share the most edges with, until there are at most 15 and none holds under 3 features. The glossary adds one more pull: the terms the two groups' code shares.</summary>
internal static class ConceptGrouping
{
    // A group whose code carries all the terms of another pulls as hard as this many shared edges.
    private const double GlossaryWeight = 4.0;

    // The groups in 'frozen' (the scan's hinted groups) are the hint's own: nothing merges into them and they merge into nothing, however small.
    internal static Dictionary<string, List<string>> Merge(GraphSnapshot graph, Dictionary<string, List<string>> groups, GlossaryTermIndex? terms = null, IReadOnlySet<string>? frozen = null)
    {
        var neighbors = Neighbors(graph);
        while (Open(groups, frozen) is { Count: > 1 } open && (groups.Count > ConceptPlan.MaxConcepts || open.Exists(k => groups[k].Count < ConceptPlan.MinFeatures)) && Total(groups) >= ConceptPlan.WholeBelow)
        {
            var smallest = open.OrderBy(k => groups[k].Count).ThenBy(k => k, StringComparer.Ordinal).First();
            var target = open.Where(k => k != smallest)
                .OrderByDescending(k => Connection(groups[smallest], groups[k], neighbors) + (terms is null ? 0 : GlossaryWeight * terms.Between(groups[smallest], groups[k])))
                .ThenByDescending(k => groups[k].Count)
                .ThenBy(k => k, StringComparer.Ordinal).First();
            groups[target] = [.. groups[target].Concat(groups[smallest]).Order(StringComparer.Ordinal)];
            _ = groups.Remove(smallest);
        }

        return groups;
    }

    private static List<string> Open(Dictionary<string, List<string>> groups, IReadOnlySet<string>? frozen) =>
        [.. groups.Keys.Where(k => frozen is null || !frozen.Contains(k))];

    private static int Total(Dictionary<string, List<string>> groups) => groups.Values.Sum(g => g.Count);

    // Each node's neighbours over every edge but belongs-to, in both directions.
    private static Dictionary<string, HashSet<string>> Neighbors(GraphSnapshot graph)
    {
        var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type != EdgeType.BelongsTo))
        {
            Add(neighbors, edge.From.Value, edge.To.Value);
            Add(neighbors, edge.To.Value, edge.From.Value);
        }

        return neighbors;
    }

    private static void Add(Dictionary<string, HashSet<string>> neighbors, string key, string other)
    {
        if (!neighbors.TryGetValue(key, out var set))
        {
            neighbors[key] = set = new HashSet<string>(StringComparer.Ordinal);
        }

        _ = set.Add(other);
    }

    // The direct links between the two groups' features, plus the other nodes both reach (a shared surface or file, say).
    private static int Connection(List<string> from, List<string> to, Dictionary<string, HashSet<string>> neighbors)
    {
        var fromSet = from.ToHashSet(StringComparer.Ordinal);
        var toSet = to.ToHashSet(StringComparer.Ordinal);
        var direct = from.Sum(f => neighbors.TryGetValue(f, out var near) ? near.Count(toSet.Contains) : 0);
        var reachedFrom = from.SelectMany(f => neighbors.TryGetValue(f, out var near) ? near : []).Where(n => !toSet.Contains(n) && !fromSet.Contains(n)).ToHashSet(StringComparer.Ordinal);
        var shared = to.SelectMany(f => neighbors.TryGetValue(f, out var near) ? near : []).Distinct(StringComparer.Ordinal).Count(reachedFrom.Contains);
        return direct + shared;
    }
}
