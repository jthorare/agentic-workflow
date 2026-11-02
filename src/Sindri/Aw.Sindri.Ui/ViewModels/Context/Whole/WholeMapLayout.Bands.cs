using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's layout (<see cref="WholeMapLayout"/>, continued): the bands across the columns, and the overview's columns.</summary>
internal static partial class WholeMapLayout
{
    private const double NodeCell = NodeHeight + NodeGap;

    // The bands stacked under the headers, each as tall as its fullest column needs (or the floor asks), so a band lines up across every column and
    // across the open columns and the overview's beside them. Why, Product and Code always have a lane; Concepts and Other only when something is in it.
    private static List<PlacedBand> Extents(Dictionary<WholeBand, double> need, Dictionary<WholeBand, string> meta, IReadOnlyList<PlacedBand>? floor = null)
    {
        var bands = new List<PlacedBand>();
        var top = Margin + HeaderHeight + LaneLabel;
        foreach (var band in Enum.GetValues<WholeBand>())
        {
            var below = floor?.FirstOrDefault(f => f.Band == band);
            var height = Math.Max(need.GetValueOrDefault(band), below?.Height ?? 0);
            var words = meta.GetValueOrDefault(band, string.Empty);   // the label counts what is drawn; a floor gives height only

            if (band is WholeBand.Concept or WholeBand.Other && height <= 0 && words.Length == 0)
            {
                continue;
            }

            height = Math.Max(height, NodeCell);
            bands.Add(new PlacedBand(band, top, height, words));
            top += height + BandGap;
        }

        return bands;
    }

    private static Dictionary<WholeBand, string> KindMeta(IEnumerable<KeyValuePair<string, int>> kinds) =>
        NodeBands.ByBand(kinds.GroupBy(k => k.Key, StringComparer.Ordinal).Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(k => k.Value))))
            .ToDictionary(b => b.Key, b => NodeBands.Meta(b.Value));

    // The overview: a tall column per area, in the order, with its kinds counted in the band each belongs to.
    private static WholeMapGeometry Tiles(WholeMapView map, IReadOnlyList<string> order, IReadOnlyList<PlacedBand>? floor = null)
    {
        var byKey = map.Areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        var perArea = order.ToDictionary(key => key, key => NodeBands.ByBand(byKey[key].ByKind), StringComparer.Ordinal);
        var need = new Dictionary<WholeBand, double>();
        foreach (var bands in perArea.Values)
        {
            foreach (var (band, kinds) in bands)
            {
                need[band] = Math.Max(need.GetValueOrDefault(band), kinds.Count * KindLine);
            }
        }

        var extents = Extents(need, KindMeta(map.Areas.SelectMany(a => a.ByKind)), floor);
        var height = extents[^1].Top + extents[^1].Height + Margin;
        var tiles = order.Select((key, i) =>
        {
            var area = byKey[key];
            var rect = new WholeRect(Margin + (i * (OverviewWidth + ColumnGutter)), Margin, OverviewWidth, height - (2 * Margin));
            var cells = extents.Where(b => perArea[key].ContainsKey(b.Band)).Select(b => new PlacedTileCell(b.Band, new WholeRect(rect.X, b.Top, rect.Width, b.Height), perArea[key][b.Band])).ToList();
            var kinds = area.ByKind.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).ToList();
            return new PlacedTile(key, area.Label, i, rect, area.Members, kinds, area.Unassigned, cells);
        }).ToList();
        var edges = map.AreaEdges.Where(e => byKey.ContainsKey(e.From) && byKey.ContainsKey(e.To)).Select(e =>
        {
            var (from, to) = (tiles.First(t => string.Equals(t.Key, e.From, StringComparison.Ordinal)).Rect, tiles.First(t => string.Equals(t.Key, e.To, StringComparison.Ordinal)).Rect);
            return new PlacedAreaEdge(e.From, e.To, e.Type, e.Count, e.Confirmed, from.CenterX, Margin / 2, to.CenterX, Margin / 2);
        }).ToList();
        var world = new WholeRect(0, 0, (2 * Margin) + (Math.Max(1, order.Count) * (OverviewWidth + ColumnGutter)) - ColumnGutter, height);
        return new WholeMapGeometry(world, ByArea: true, order, [], [], tiles, [], edges, extents);
    }

    // An overview column moved right to stand beside open columns: its cells go with it, and it counts on from the open ones.
    private static PlacedTile Shifted(PlacedTile tile, double dx, int openCount) => tile with
    {
        AreaIndex = tile.AreaIndex + openCount,
        Rect = tile.Rect with { X = tile.Rect.X + dx },
        Cells = [.. tile.BandCells.Select(c => c with { Rect = c.Rect with { X = c.Rect.X + dx } })],
    };
}
