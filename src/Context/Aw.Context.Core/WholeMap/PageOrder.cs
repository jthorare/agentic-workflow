using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>The order the Pages sections list pages in (pages spec §3.4): owner-named pages first in profile order (a 'page:' evidence ref, in node order), then the suggested ones by number of actions, then key.</summary>
internal static class PageOrder
{
    /// <summary>The keys of <paramref name="pages"/> in the section's order.</summary>
    /// <param name="pages">Page surfaces, in graph node order.</param>
    /// <param name="edges">The graph's edges; 'on' edges count the actions of a page.</param>
    /// <returns>The page keys, owner-named first.</returns>
    internal static List<string> Of(IEnumerable<GraphNode> pages, IReadOnlyList<GraphEdge> edges)
    {
        var actions = edges.Where(e => e is { Type: EdgeType.On, From.Kind: NodeKind.Action }).GroupBy(e => e.To.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
        var list = pages.ToList();
        var owner = list.Where(IsOwner).Select(n => n.Key.Value);
        var suggested = list.Where(n => !IsOwner(n)).Select(n => n.Key.Value)
            .OrderByDescending(k => actions.GetValueOrDefault(k)).ThenBy(k => k, StringComparer.Ordinal);
        return [.. owner, .. suggested];
    }

    /// <summary>Whether the owner named <paramref name="node"/> in the profile: it carries a 'page:' evidence ref.</summary>
    /// <param name="node">The node.</param>
    /// <returns>True for an owner-named page or action.</returns>
    internal static bool IsOwner(GraphNode node) => node.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith("page:", StringComparison.Ordinal));
}
