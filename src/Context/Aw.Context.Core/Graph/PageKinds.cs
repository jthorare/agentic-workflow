namespace Aw.Context.Core.Graph;

/// <summary>Which surfaces are pages (pages spec §3.2).</summary>
public static class PageKinds
{
    private static readonly string[] _componentExtensions = [".tsx", ".jsx"];

    /// <summary>Whether <paramref name="node"/> is a page, judged from its kind and the paths of the 'implements' edges into it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="edges">The graph's edges; only 'implements' edges into the node are read.</param>
    /// <returns>True for a 'screen' or 'dialog' surface, and for a 'route' implemented from a '.tsx' or '.jsx' file; never a 'pane', 'api' or any other kind.</returns>
    public static bool IsPage(GraphNode node, IEnumerable<GraphEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        return IsPageIn(node, ComponentImplemented(edges));
    }

    /// <summary>The keys of the nodes some '.tsx' or '.jsx' file implements: one pass over the edges, to ask <see cref="IsPageIn(GraphNode, IReadOnlySet{string})"/> per node without a scan each.</summary>
    /// <param name="edges">The graph's edges; only 'implements' edges are read.</param>
    /// <returns>The node keys, ordinal.</returns>
    public static IReadOnlySet<string> ComponentImplemented(IEnumerable<GraphEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        return edges.Where(e => e.Type == EdgeType.Implements && _componentExtensions.Any(x => e.From.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="node"/> is a page: the same answer as <see cref="IsPage(GraphNode, IEnumerable{GraphEdge})"/>, given the set from <see cref="ComponentImplemented"/>.</summary>
    /// <param name="node">The node.</param>
    /// <param name="componentImplemented">The keys a '.tsx' or '.jsx' file implements.</param>
    /// <returns>True for a 'screen' or 'dialog' surface, and for a 'route' in the set; never any other kind.</returns>
    public static bool IsPageIn(GraphNode node, IReadOnlySet<string> componentImplemented)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(componentImplemented);
        if (node.Key.Kind != NodeKind.Surface)
        {
            return false;
        }

        return SurfaceKindOf(node.Key) switch
        {
            "screen" or "dialog" => true,
            "route" => componentImplemented.Contains(node.Key.Value),
            _ => false,
        };
    }

    private static string SurfaceKindOf(NodeKey key)
    {
        var name = key.Name;
        var slash = name.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? name : name[..slash];
    }
}
