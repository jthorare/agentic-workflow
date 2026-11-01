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
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(edges);
        if (node.Key.Kind != NodeKind.Surface)
        {
            return false;
        }

        return SurfaceKindOf(node.Key) switch
        {
            "screen" or "dialog" => true,
            "route" => edges.Any(e => e.Type == EdgeType.Implements && e.To.Value == node.Key.Value && _componentExtensions.Any(x => e.From.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
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
