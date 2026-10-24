namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Which node edges are drawn (q86): the ones inside one area, and every edge of the hovered or pinned node. The rest would be a hairball.</summary>
internal static class WholeEdgeSelection
{
    /// <summary>Above this many drawn edges, a pill shows only on the focused node's edges.</summary>
    internal const int PillLimit = 12;

    /// <summary>The edges to draw.</summary>
    /// <param name="edges">Every node edge.</param>
    /// <param name="nodes">The placed nodes.</param>
    /// <param name="focus">The hovered or pinned node, or null.</param>
    /// <returns>The edges inside an area, plus the focused node's.</returns>
    internal static IReadOnlyList<PlacedEdge> Visible(IReadOnlyList<PlacedEdge> edges, IReadOnlyList<PlacedNode> nodes, string? focus)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(nodes);
        var area = nodes.ToDictionary(n => n.Key, n => n.AreaIndex, StringComparer.Ordinal);
        return [.. edges.Where(e => Touches(e, focus) || (area.TryGetValue(e.From, out var a) && area.TryGetValue(e.To, out var b) && a == b))];
    }

    /// <summary>Whether an edge's pill shows: always when the drawing is uncluttered, else only on the focused node's edges.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="drawn">How many edges are drawn.</param>
    /// <param name="focus">The hovered or pinned node, or null.</param>
    /// <returns>True to show the pill.</returns>
    internal static bool ShowsPill(PlacedEdge edge, int drawn, string? focus) => Touches(edge, focus) || drawn <= PillLimit;

    /// <summary>Whether the focused node is an end of the edge.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="focus">The node, or null.</param>
    /// <returns>True when it is.</returns>
    internal static bool Touches(PlacedEdge edge, string? focus) =>
        focus is not null && (string.Equals(edge.From, focus, StringComparison.Ordinal) || string.Equals(edge.To, focus, StringComparison.Ordinal));
}
