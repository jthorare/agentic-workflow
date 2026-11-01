using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>The pages (<see cref="PageKinds.IsPage"/>) that relate to no concept: no 'expresses' edge from the page or from any action 'on' it (pages spec §3.4).</summary>
public static class PagesInNoConcept
{
    /// <summary>The keys of those pages in the Pages section's order (<see cref="PageOrder"/>): owner-named pages first in the profile's order, then suggested ones by number of actions, then key.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="rank">Each profile page's index by page id, or null to keep owner pages in node order.</param>
    /// <returns>The page keys; the count is the list's length.</returns>
    public static IReadOnlyList<string> Of(GraphSnapshot graph, IReadOnlyDictionary<string, int>? rank = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var expressing = graph.Edges.Where(e => e.Type == EdgeType.Expresses).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal);
        var related = graph.Edges.Where(e => e is { Type: EdgeType.On, From.Kind: NodeKind.Action } && expressing.Contains(e.From.Value)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        var pages = graph.Nodes.DistinctBy(n => n.Key.Value, StringComparer.Ordinal)
            .Where(n => !expressing.Contains(n.Key.Value) && !related.Contains(n.Key.Value) && PageKinds.IsPage(n, graph.Edges));
        return PageOrder.Of(pages, graph.Edges, rank);
    }
}
