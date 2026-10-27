namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A search's filter on the whole map: the pure decision of what stays drawn. The kept nodes and tiles, the edges between them, and the columns and bands that still hold one; the rest is gone, not dimmed.</summary>
internal static class WholeMapFilter
{
    /// <summary>The geometry cut down to some keys. The world stays whole, so lanes and coordinates do not move.</summary>
    /// <param name="geometry">The whole map's geometry.</param>
    /// <param name="keep">The keys to keep: the matches and the keys one edge away.</param>
    /// <returns>The geometry holding only the kept nodes and tiles, and the edges with both ends kept.</returns>
    internal static WholeMapGeometry Keep(WholeMapGeometry geometry, IReadOnlySet<string> keep)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(keep);
        var nodes = geometry.Nodes.Where(n => keep.Contains(n.Key)).ToList();
        var areas = nodes.Select(n => n.AreaIndex).ToHashSet();
        var tiles = geometry.Tiles.Where(t => keep.Contains(t.Key)).ToList();
        var tileBands = tiles.SelectMany(t => t.BandCells).Select(c => c.Band).ToHashSet();   // a kept tile's cells sit in their bands too
        return geometry with
        {
            Nodes = nodes,
            Tiles = tiles,
            Edges = [.. geometry.Edges.Where(e => keep.Contains(e.From) && keep.Contains(e.To))],
            AreaEdges = [.. geometry.AreaEdges.Where(e => keep.Contains(e.From) && keep.Contains(e.To))],
            Columns = [.. geometry.Columns.Where(c => areas.Contains(c.AreaIndex))],
            Bands = [.. geometry.Bands.Where(b => tileBands.Contains(b.Band) || nodes.Exists(n => n.Rect.CenterY >= b.Top && n.Rect.CenterY <= b.Top + b.Height))],
        };
    }

    /// <summary>The box the camera and the minimap fit: the kept nodes and tiles, padded; the whole world when nothing is kept.</summary>
    /// <param name="shown">The filtered geometry.</param>
    /// <param name="world">The whole world, for when nothing is kept.</param>
    /// <returns>The box.</returns>
    internal static WholeRect FitBox(WholeMapGeometry shown, WholeRect world)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var rects = shown.Nodes.Select(n => n.Rect).Concat(shown.Tiles.Select(t => t.Rect)).ToList();
        return rects.Count == 0 ? world : WholeZoomBox.Around(rects);
    }
}
