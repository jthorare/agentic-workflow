namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Drilling into one band of a concept's map (#1289): the pure decision of what stays drawn, and what each band that stays says it holds.</summary>
internal static class WholeMapBandDrill
{
    /// <summary>The most Product nodes the Why drill adds by following product structure from the ones a decision reaches (#1701).</summary>
    internal const int ProductReachCap = 200;

    // The links that make up the product's structure: pages under features, actions on pages, what an action reads or writes, steps and dependencies.
    private static readonly HashSet<string> _productStructure = new(["part-of", "on", "reads", "writes", "step-of", "depends-on", "moves-to"], StringComparer.Ordinal);

    /// <summary>The links between a band and the other bands: the band's nodes that have one, the nodes on their other ends, and only those links. Drilling Why also follows product structure from the Product nodes it reaches (up to <see cref="ProductReachCap"/>), and keeps the Product band even when none is reached.</summary>
    /// <param name="geometry">The map's geometry.</param>
    /// <param name="band">The band drilled into.</param>
    /// <returns>The geometry cut down; the bands that still hold a node count what is drawn.</returns>
    internal static WholeMapGeometry Cross(WholeMapGeometry geometry, WholeBand band)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var inside = Inside(geometry, band);
        var edges = geometry.Edges.Where(e => inside.Contains(e.From) != inside.Contains(e.To)).ToList();
        HashSet<string> keep = [.. edges.SelectMany(e => new[] { e.From, e.To })];
        if (band != WholeBand.Intent)
        {
            return Cut(geometry, band, edges, keep);
        }

        edges.AddRange(ProductClosure(geometry, keep));
        if (!geometry.Nodes.Any(n => keep.Contains(n.Key) && NodeBands.Of(n.Kind) == WholeBand.Product))
        {
            // Nothing in Product is reached: the Why band's own records and decisions stay drawn beside the empty Product band, not an empty Why band.
            keep.UnionWith(Inside(geometry, band));
        }
        var cut = Cut(geometry, band, edges, keep, alsoKeep: WholeBand.Product);
        return cut.Nodes.Any(n => NodeBands.Of(n.Kind) == WholeBand.Product)
            ? cut
            : cut with { Bands = [.. cut.Bands.Select(b => b.Band == WholeBand.Product ? b with { Meta = Labels.ContextDrillWhyNoProduct } : b)] };
    }

    // From the Product nodes already kept, the product-structure links with both ends in Product, either way round, transitively; adds the reached nodes to keep.
    private static List<PlacedEdge> ProductClosure(WholeMapGeometry geometry, HashSet<string> keep)
    {
        var product = geometry.Nodes.Where(n => NodeBands.Of(n.Kind) == WholeBand.Product).Select(n => n.Key).ToHashSet(StringComparer.Ordinal);
        var links = geometry.Edges.Where(e => _productStructure.Contains(e.Type) && product.Contains(e.From) && product.Contains(e.To)).ToList();
        var frontier = new SortedSet<string>(keep.Where(product.Contains), StringComparer.Ordinal);
        var added = 0;
        while (frontier.Count > 0 && added < ProductReachCap)
        {
            var current = frontier.First();
            _ = frontier.Remove(current);
            foreach (var next in Neighbours(links, current).Where(keep.Add).Take(ProductReachCap - added))
            {
                added++;
                _ = frontier.Add(next);
            }
        }

        return [.. links.Where(e => keep.Contains(e.From) && keep.Contains(e.To))];
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

    // The other end of every link at the node, in key order.
    private static IEnumerable<string> Neighbours(List<PlacedEdge> links, string node) =>
        links.Where(e => e.From == node || e.To == node).Select(e => e.From == node ? e.To : e.From).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static HashSet<string> Inside(WholeMapGeometry geometry, WholeBand band) =>
        [.. geometry.Nodes.Where(n => NodeBands.Of(n.Kind) == band).Select(n => n.Key)];

    // The bands stay where they are; the kept columns close up side by side, so a drilled band's few items are not spread across the whole map (#1645). Each band that stays counts what is drawn in it, and the drilled band stays even when it is empty.
    private static WholeMapGeometry Cut(WholeMapGeometry geometry, WholeBand band, List<PlacedEdge> edges, HashSet<string> keep, WholeBand? alsoKeep = null)
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
            .Where(b => b.Band == band || b.Band == alsoKeep || nodes.Exists(n => NodeBands.Of(n.Kind) == b.Band))
            .ToList();
        return geometry with { Nodes = moved, Tiles = [], Edges = shifted, AreaEdges = [], Columns = columns, Bands = bands, World = geometry.World with { Width = width } };
    }

    private static string Meta(List<PlacedNode> nodes, WholeBand band) =>
        NodeBands.Meta(nodes.Where(n => NodeBands.Of(n.Kind) == band).GroupBy(n => n.Kind, StringComparer.Ordinal).Select(g => new KeyValuePair<string, int>(g.Key, g.Count())));
}
