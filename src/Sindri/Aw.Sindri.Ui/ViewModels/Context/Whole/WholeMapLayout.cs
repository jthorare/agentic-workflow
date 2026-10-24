using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// Places the whole map (whole-map spec §3.2). Pure, like ContextMapLayout: the same answer gives the same numbers. A node-level
/// answer is areas as columns (ordered by <see cref="WholeAreaOrder"/>) with the three bands across them, the same bands in every column;
/// an areas-level answer is one tile per area on a grid, in the same order.
/// </summary>
internal static partial class WholeMapLayout
{
    /// <summary>A node's width.</summary>
    internal const double NodeWidth = 120;

    /// <summary>A node's height.</summary>
    internal const double NodeHeight = 28;

    /// <summary>The space between nodes.</summary>
    internal const double NodeGap = 6;

    /// <summary>How many nodes sit side by side in a band of one column.</summary>
    internal const int Across = 2;

    /// <summary>The width of the space around a column.</summary>
    internal const double ColumnGutter = 24;

    /// <summary>The height of an area's header.</summary>
    internal const double HeaderHeight = 32;

    /// <summary>The height of the space between bands.</summary>
    internal const double BandGap = 40;

    /// <summary>The height of the strip over a band's rows, where its title sits.</summary>
    internal const double LaneLabel = 24;

    /// <summary>The space under a band's last row, inside its lane.</summary>
    internal const double LanePad = 6;

    /// <summary>The height of one line of kind counts in an overview column.</summary>
    internal const double KindLine = 20;

    /// <summary>The width of an area's column on the overview.</summary>
    internal const double OverviewWidth = 144;

    /// <summary>The space between the overview's columns and the open columns beside them.</summary>
    internal const double TileGap = 32;

    /// <summary>The margin around the world.</summary>
    internal const double Margin = 24;

    private const double ColumnWidth = (Across * (NodeWidth + NodeGap)) + ColumnGutter;

    /// <summary>The band a node kind sits in (<see cref="NodeBands"/>).</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>Its band.</returns>
    internal static WholeBand BandOf(string kind) => NodeBands.Of(kind);

    /// <summary>Places an answer.</summary>
    /// <param name="map">The answer.</param>
    /// <returns>Where everything sits.</returns>
    internal static WholeMapGeometry Place(WholeMapView map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Area is { } opened)
        {
            // One feature opened in full: its nodes alone, in one column (the others stay tiles in the overview it came from).
            map = map with { Areas = [.. map.Areas.Where(a => string.Equals(a.Key, opened.Key, StringComparison.Ordinal))], AreaEdges = [] };
        }

        var order = WholeAreaOrder.Of(map.Areas, map.AreaEdges);
        return map.Nodes is { Count: > 0 } nodes && !string.Equals(map.Detail, "areas", StringComparison.Ordinal)
            ? Columns(map, order, nodes)
            : Tiles(map, order);
    }

    /// <summary>
    /// Places an overview with some features opened in full (a query zoomed into an answer by feature): the opened ones as columns of nodes, the rest still as
    /// tiles in a block to their right, in the same order.
    /// </summary>
    /// <param name="overview">The answer by area.</param>
    /// <param name="opened">The answers for the opened features (each read with area=).</param>
    /// <returns>Where everything sits.</returns>
    internal static WholeMapGeometry PlaceMixed(WholeMapView overview, IReadOnlyList<WholeMapView> opened)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(opened);
        var openKeys = opened.Where(o => o.Area is not null).Select(o => o.Area!.Key).ToHashSet(StringComparer.Ordinal);   // why: filtered on Area
        var inOrder = WholeAreaOrder.Of(overview.Areas, overview.AreaEdges);
        var rest = overview.Areas.Where(a => !openKeys.Contains(a.Key)).ToList();
        var restKeys = rest.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var nodes = opened.SelectMany(o => o.Nodes ?? []).GroupBy(n => n.Key, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var edges = opened.SelectMany(o => o.Edges ?? []).GroupBy(e => e.Link, StringComparer.Ordinal).Select(g => g.First()).ToList();
        List<string> open = [.. inOrder.Where(openKeys.Contains)];
        var floor = Extents([], KindMeta(overview.Areas.SelectMany(a => a.ByKind)));
        var restEdges = overview.AreaEdges.Where(e => restKeys.Contains(e.From) && restKeys.Contains(e.To)).ToList();
        var restOrder = WholeAreaOrder.Of(rest, restEdges);
        var restMap = overview with { Areas = rest, AreaEdges = restEdges };
        var columns = Columns(overview with { Areas = [.. overview.Areas.Where(a => openKeys.Contains(a.Key))], AreaEdges = [], Nodes = nodes, Edges = edges, Detail = "nodes", Area = null }, open, nodes, Tiles(restMap, restOrder, floor).Bands);
        var tiles = Tiles(restMap, restOrder, columns.Bands);
        var shift = columns.World.Right + TileGap;
        var placedTiles = tiles.Tiles.Select(t => Shifted(t, shift, open.Count)).ToList();
        var areaEdges = tiles.AreaEdges.Select(e => e with { X1 = e.X1 + shift, X2 = e.X2 + shift }).ToList();
        var world = new WholeRect(0, 0, shift + tiles.World.Width, Math.Max(columns.World.Height, tiles.World.Height));
        return new WholeMapGeometry(world, ByArea: false, [.. columns.Order, .. tiles.Order], columns.Columns, columns.Nodes, placedTiles, columns.Edges, areaEdges, columns.Bands);
    }

    private static WholeMapGeometry Columns(WholeMapView map, IReadOnlyList<string> order, IReadOnlyList<WholeNode> nodes, IReadOnlyList<PlacedBand>? floor = null)
    {
        var index = order.Select((k, i) => (k, i)).ToDictionary(p => p.k, p => p.i, StringComparer.Ordinal);
        var labels = map.Areas.ToDictionary(a => a.Key, a => a.Label, StringComparer.Ordinal);
        var kept = nodes.Where(n => index.ContainsKey(n.Area)).ToList();
        var groups = kept.GroupBy(n => (Area: index[n.Area], Band: BandOf(n.Kind))).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Kind, StringComparer.Ordinal).ThenBy(n => n.Key, StringComparer.Ordinal).ToList());
        var need = groups.GroupBy(g => g.Key.Band).ToDictionary(g => g.Key, g => g.Max(x => Math.Ceiling(x.Value.Count / (double)Across)) * NodeCell);
        var bands = Extents(need, KindMeta(kept.GroupBy(n => n.Kind, StringComparer.Ordinal).Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))), floor);
        var placed = new List<PlacedNode>(kept.Count);
        foreach (var ((area, band), members) in groups.OrderBy(g => g.Key.Area).ThenBy(g => g.Key.Band))
        {
            var top = bands.First(b => b.Band == band).Top;
            for (var i = 0; i < members.Count; i++)
            {
                var rect = new WholeRect(
                    Margin + (area * ColumnWidth) + (i % Across * (NodeWidth + NodeGap)),
                    top + (i / Across * NodeCell),
                    NodeWidth,
                    NodeHeight);
                placed.Add(new PlacedNode(members[i].Key, members[i].Label, members[i].Kind, area, rect, members[i].Confirmed));
            }
        }

        var height = bands[^1].Top + bands[^1].Height + Margin;
        var columns = order.Select((key, i) => new AreaColumn(key, labels.GetValueOrDefault(key, key), i, new WholeRect(Margin + (i * ColumnWidth), Margin, ColumnWidth - ColumnGutter, height - (2 * Margin)))).ToList();
        var position = placed.ToDictionary(p => p.Key, StringComparer.Ordinal);
        var edges = (map.Edges ?? []).Where(e => position.ContainsKey(e.From) && position.ContainsKey(e.To)).Select(e =>
            new PlacedEdge(e.From, e.To, e.Type, e.Confirmed, position[e.From].Rect.CenterX, position[e.From].Rect.CenterY, position[e.To].Rect.CenterX, position[e.To].Rect.CenterY, e.Confidence)).ToList();
        var world = new WholeRect(0, 0, (2 * Margin) + (Math.Max(1, order.Count) * ColumnWidth) - ColumnGutter, height);
        return new WholeMapGeometry(world, ByArea: false, order, columns, placed, [], edges, [], bands);
    }
}
