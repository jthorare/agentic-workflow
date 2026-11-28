namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Where the link labels of the whole map end up (#3678): each on a free stretch between its two nodes, then spread so none lies on another.</summary>
internal static class WholePillLayout
{
    /// <summary>Decides the labels the canvas draws.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="drawn">The edges drawn.</param>
    /// <param name="focus">The hovered or pinned node, or null.</param>
    /// <param name="scale">The zoom.</param>
    /// <param name="box">The size of a label's box from its words, in world units.</param>
    /// <returns>The labels that have a place, each centred where it is drawn.</returns>
    internal static IReadOnlyList<WholePill> Place(WholeMapGeometry geometry, IReadOnlyList<PlacedEdge> drawn, string? focus, double scale, Func<string, (double Width, double Height)> box)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(drawn);
        ArgumentNullException.ThrowIfNull(box);
        // Obstacles (#4442): every node and every heading strip, so a label never covers a layer's title.
        var rects = geometry.Nodes.Select(n => n.Rect).ToList();
        rects.AddRange(geometry.Bands.Select(b => new WholeRect(0, b.LaneTop, geometry.World.Width, WholeMapLayout.LaneLabel)));
        rects.AddRange((geometry.AreaBands ?? []).Where(b => !b.Sub).Select(b => b.Rect));
        var byKey = new Dictionary<string, WholeRect>(StringComparer.Ordinal);
        foreach (var node in geometry.Nodes)
        {
            _ = byKey.TryAdd(node.Key, node.Rect);
        }

        var pills = new List<WholePill>();
        foreach (var edge in drawn.Where(e => WholeEdgeSelection.ShowsPill(e, drawn.Count, focus)))
        {
            var style = WholeEdgeStyle.Of(edge);
            var (width, height) = box(style.Pill);
            if (byKey.TryGetValue(edge.From, out var from) && byKey.TryGetValue(edge.To, out var to) && LinkLabelPlacer.Place(from, to, width, height, rects) is { } at)
            {
                pills.Add(new WholePill(style, at.CenterX, at.CenterY));
            }
        }

        return WholePillPlacer.Place(pills, scale, rects);
    }
}
