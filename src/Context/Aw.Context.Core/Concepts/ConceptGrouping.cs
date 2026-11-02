using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Merges the groups that belong together; there is no fixed number of concepts. A group under 3 features joins the group it shares the most edges with, smallest first. A bigger group joins another only when, on average, every one of its features links into it. The glossary adds one more pull: the terms the two groups' code shares. Ties go to the smaller group, and no concept passes 3x the average starting area.</summary>
internal static class ConceptGrouping
{
    // A group whose code carries all the terms of another pulls as hard as this many shared edges.
    private const double GlossaryWeight = 4.0;

    // A merged concept may hold at most this many times the average size of the open groups the merge started with.
    private const double SizeCapFactor = 3.0;

    // The groups in 'frozen' (the scan's hinted groups) are the hint's own: nothing merges into them and they merge into nothing, however small.
    internal static Dictionary<string, List<string>> Merge(GraphSnapshot graph, Dictionary<string, List<string>> groups, GlossaryTermIndex? terms = null, IReadOnlySet<string>? frozen = null)
    {
        var neighbors = Neighbors(graph);
        var start = Open(groups, frozen);
        var cap = start.Count == 0 ? 0 : Math.Max(2 * ConceptPlan.MinFeatures, (int)Math.Ceiling(SizeCapFactor * start.Sum(k => groups[k].Count) / start.Count));
        while (Total(groups) >= ConceptPlan.WholeBelow && Open(groups, frozen) is { Count: > 1 } open && NextMerge(groups, open, neighbors, terms, cap) is { } merge)
        {
            groups[merge.Target] = [.. groups[merge.Target].Concat(groups[merge.Source]).Order(StringComparer.Ordinal)];
            _ = groups.Remove(merge.Source);
        }

        return groups;
    }

    // The smallest group that has a merge worth making, with its best target under the cap; null when none has.
    private static (string Source, string Target)? NextMerge(Dictionary<string, List<string>> groups, List<string> open, Dictionary<string, HashSet<string>> neighbors, GlossaryTermIndex? terms, int cap)
    {
        foreach (var source in open.OrderBy(k => groups[k].Count).ThenBy(k => k, StringComparer.Ordinal))
        {
            var scored = open.Where(k => k != source && groups[k].Count + groups[source].Count <= cap)
                .Select(k => (Key: k, Score: Connection(groups[source], groups[k], neighbors) + (terms is null ? 0 : GlossaryWeight * terms.Between(groups[source], groups[k]))))
                .OrderByDescending(c => c.Score)
                .ThenBy(c => groups[c.Key].Count)
                .ThenBy(c => c.Key, StringComparer.Ordinal)
                .ToList();
            if (scored.Count > 0 && (groups[source].Count < ConceptPlan.MinFeatures || scored[0].Score >= groups[source].Count))
            {
                return (source, scored[0].Key);
            }
        }

        return null;
    }

    private static List<string> Open(Dictionary<string, List<string>> groups, IReadOnlySet<string>? frozen) =>
        [.. groups.Keys.Where(k => frozen is null || !frozen.Contains(k))];

    private static int Total(Dictionary<string, List<string>> groups) => groups.Values.Sum(g => g.Count);

    // Each node's neighbours over every edge but belongs-to and tags (a tag is no link between modules), in both directions.
    private static Dictionary<string, HashSet<string>> Neighbors(GraphSnapshot graph)
    {
        var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type is not (EdgeType.BelongsTo or EdgeType.Tags)))
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
