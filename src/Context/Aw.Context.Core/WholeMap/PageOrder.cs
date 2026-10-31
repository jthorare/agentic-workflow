using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>The order the Pages sections list pages in (pages spec §3.4): owner-named pages first in the profile's order (the index of a page's id among the profile's pages, read from its 'page:&lt;repo&gt;/&lt;id&gt;' evidence ref), then the suggested ones by number of actions, then key.</summary>
internal static class PageOrder
{
    /// <summary>The keys of <paramref name="pages"/> in the section's order.</summary>
    /// <param name="pages">Page surfaces, in graph node order.</param>
    /// <param name="edges">The graph's edges; 'on' edges count the actions of a page.</param>
    /// <param name="rank">Each profile page's index by page id, or null or empty to keep owner pages in node order. An owner page whose id is not in it comes after the ranked ones, by key.</param>
    /// <returns>The page keys, owner-named first.</returns>
    internal static List<string> Of(IEnumerable<GraphNode> pages, IReadOnlyList<GraphEdge> edges, IReadOnlyDictionary<string, int>? rank = null)
    {
        var actions = edges.Where(e => e is { Type: EdgeType.On, From.Kind: NodeKind.Action }).GroupBy(e => e.To.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
        var list = pages.ToList();
        var owner = rank is { Count: > 0 }
            ? list.Where(IsOwner).OrderBy(n => RankOf(n, rank)).ThenBy(n => n.Key.Value, StringComparer.Ordinal).Select(n => n.Key.Value)
            : list.Where(IsOwner).Select(n => n.Key.Value);
        var suggested = list.Where(n => !IsOwner(n)).Select(n => n.Key.Value)
            .OrderByDescending(k => actions.GetValueOrDefault(k)).ThenBy(k => k, StringComparer.Ordinal);
        return [.. owner, .. suggested];
    }

    /// <summary>Whether the owner named <paramref name="node"/> in the profile: it carries a 'page:' evidence ref.</summary>
    /// <param name="node">The node.</param>
    /// <returns>True for an owner-named page or action.</returns>
    internal static bool IsOwner(GraphNode node) => node.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith("page:", StringComparison.Ordinal));

    /// <summary>The id of the first owner page that named <paramref name="node"/>: the part after the last '/' of its 'page:&lt;repo&gt;/&lt;id&gt;' evidence ref (the same parse as <see cref="RankOf"/>). PageOverlay lays pages in the profile's order, so the first ref is also the lowest-ranked page and a surface two pages name keeps its id and its rank on one page.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The page id, or null when no page named the node.</returns>
    internal static PageId? PageIdOf(GraphNode node) =>
        node.EvidenceOrEmpty.Select(r => r.Key.Value).Where(v => v.StartsWith("page:", StringComparison.Ordinal))
            .Select(v => v[(v.LastIndexOf('/') + 1)..]).Select(id => PageId.TryFrom(id, out var page) ? page : (PageId?)null).FirstOrDefault(page => page is not null);

    // The lowest index among the profile pages the node carries a ref of: two pages naming one surface both mark it.
    private static int RankOf(GraphNode node, IReadOnlyDictionary<string, int> rank) =>
        node.EvidenceOrEmpty.Select(r => r.Key.Value).Where(v => v.StartsWith("page:", StringComparison.Ordinal))
            .Select(v => v[(v.IndexOf('/', StringComparison.Ordinal) + 1)..])
            .Select(id => rank.TryGetValue(id, out var at) ? at : int.MaxValue).DefaultIfEmpty(int.MaxValue).Min();
}
