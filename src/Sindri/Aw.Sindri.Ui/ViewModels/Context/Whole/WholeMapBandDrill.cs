namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Drilling into one band of a concept's map (#1289): the pure decision of what stays drawn, and what each band that stays says it holds.</summary>
internal static class WholeMapBandDrill
{
    /// <summary>The links between a band and the other bands: the band's nodes that have one, the nodes on their other ends, and only those links.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="band">The band drilled into.</param>
    /// <returns>The geometry cut down; the bands that still hold a node count what is drawn.</returns>
    internal static WholeMapGeometry Cross(WholeMapGeometry geometry, WholeBand band)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var inside = Inside(geometry, band);
        var edges = geometry.Edges.Where(e => inside.Contains(e.From) != inside.Contains(e.To)).ToList();
        return Cut(geometry, band, edges, [.. edges.SelectMany(e => new[] { e.From, e.To })]);
    }

    /// <summary>The band's own nodes and only the links with both ends in it.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="band">The band drilled into.</param>
    /// <returns>The geometry cut down.</returns>
    internal static WholeMapGeometry Own(WholeMapGeometry geometry, WholeBand band)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var inside = Inside(geometry, band);
        return Cut(geometry, band, [.. geometry.Edges.Where(e => inside.Contains(e.From) && inside.Contains(e.To))], inside);
    }

    private static HashSet<string> Inside(WholeMapGeometry geometry, WholeBand band) =>
        [.. geometry.Nodes.Where(n => NodeBands.Of(n.Kind) == band).Select(n => n.Key)];

    // The bands stay where they are; the kept columns close up side by side, so a drilled band's few items are not spread across the whole map (#1645). Each band that stays counts what is drawn in it, and the drilled band stays even when it is empty.
    private static WholeMapGeometry Cut(WholeMapGeometry geometry, WholeBand band, List<PlacedEdge> edges, HashSet<string> keep)
    {
        var nodes = geometry.Nodes.Where(n => keep.Contains(n.Key)).ToList();
        var areas = nodes.Select(n => n.AreaIndex).ToHashSet();
        var kept = geometry.Columns.Where(c => areas.Contains(c.AreaIndex)).OrderBy(c => c.Rect.X).ToList();
        var shift = new Dictionary<int, double>();
        var columns = new List<AreaColumn>(kept.Count);
        foreach (var (column, k) in kept.Select((c, k) => (c, k)))
        {
            var dx = geometry.Columns[0].Rect.X + (k * WholeMapLayout.ColumnPitch) - column.Rect.X;
            shift[column.AreaIndex] = dx;
            columns.Add(column with { Rect = column.Rect with { X = column.Rect.X + dx } });
        }

        double Dx(int area) => shift.GetValueOrDefault(area);
        var moved = nodes.Select(n => n with { Rect = n.Rect with { X = n.Rect.X + Dx(n.AreaIndex) } }).ToList();
        var areaOf = nodes.ToDictionary(n => n.Key, n => n.AreaIndex, StringComparer.Ordinal);
        var shifted = edges.Select(e => e with { X1 = e.X1 + Dx(areaOf[e.From]), X2 = e.X2 + Dx(areaOf[e.To]) }).ToList();
        var width = (2 * WholeMapLayout.Margin) + (Math.Max(1, kept.Count) * WholeMapLayout.ColumnPitch) - WholeMapLayout.ColumnGutter;
        var bands = geometry.Bands
            .Select(b => b with { Meta = Meta(nodes, b.Band) })
            .Where(b => b.Band == band || nodes.Exists(n => NodeBands.Of(n.Kind) == b.Band))
            .ToList();
        return geometry with { Nodes = moved, Tiles = [], Edges = shifted, AreaEdges = [], Columns = columns, Bands = bands, World = geometry.World with { Width = width } };
    }

    private static string Meta(List<PlacedNode> nodes, WholeBand band) =>
        NodeBands.Meta(nodes.Where(n => NodeBands.Of(n.Kind) == band).GroupBy(n => n.Kind, StringComparer.Ordinal).Select(g => new KeyValuePair<string, int>(g.Key, g.Count())));
}
