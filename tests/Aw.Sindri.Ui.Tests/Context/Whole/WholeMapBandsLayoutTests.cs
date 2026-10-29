using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The map in why / product / code bands across area columns (q86), for the whole map and for an opened area.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandsLayoutTests
{
    private static readonly string[] _kinds = ["record", "decision", "feature", "surface", "action", "thing", "flow", "code", "mystery"];

    private static WholeMapView Nodes(params (string Area, string Kind)[] placed)
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var areas = placed.Select(p => p.Area).Distinct(StringComparer.Ordinal).Select(a => new WholeArea(a, a, "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), Unassigned: false)).ToList();
        var nodes = placed.Select((p, i) => new WholeNode("n:" + i.ToString(CultureInfo.InvariantCulture), "Node " + i.ToString(CultureInfo.InvariantCulture), p.Kind, p.Area, [], "trusted", "human", 1, Confirmed: true)).ToList();
        return map with { Areas = areas, AreaEdges = [], Nodes = nodes, Edges = [] };
    }

    [Fact]
    public void Each_kind_sits_inside_the_extent_of_its_own_band()
    {
        var geometry = WholeMapLayout.Place(Nodes([.. _kinds.Select(k => ("a", k))]));

        Assert.All(geometry.Nodes, node =>
        {
            var band = geometry.Bands.Single(b => b.Band == NodeBands.Of(node.Kind));
            Assert.InRange(node.Rect.Y, band.Top, band.Top + band.Height);
            Assert.InRange(node.Rect.Bottom, band.Top, band.Top + band.Height);
        });
    }

    [Fact]
    public void The_four_bands_stack_why_product_code_other_and_never_overlap()
    {
        var geometry = WholeMapLayout.Place(Nodes([.. _kinds.Select(k => ("a", k))]));

        Assert.Equal([WholeBand.Intent, WholeBand.Product, WholeBand.Code, WholeBand.Other], geometry.Bands.Select(b => b.Band));
        for (var i = 1; i < geometry.Bands.Count; i++)
        {
            Assert.True(geometry.Bands[i - 1].Top + geometry.Bands[i - 1].Height <= geometry.Bands[i].LaneTop);
        }
    }

    [Fact]
    public void Why_product_and_code_always_have_a_lane_and_other_only_when_something_is_in_it()
    {
        var geometry = WholeMapLayout.Place(Nodes(("a", "feature")));

        Assert.Equal([WholeBand.Intent, WholeBand.Product, WholeBand.Code], geometry.Bands.Select(b => b.Band));
    }

    [Fact]
    public void A_concept_node_renders_in_the_concept_row_not_product()
    {
        var geometry = WholeMapLayout.Place(Nodes(("area:concepts", "concept"), ("a", "feature"), ("a", "decision")));

        Assert.Equal([WholeBand.Concept, WholeBand.Intent, WholeBand.Product, WholeBand.Code], geometry.Bands.Select(b => b.Band));
        var concept = geometry.Nodes.Single(n => n.Kind == "concept");
        var row = geometry.Bands.Single(b => b.Band == WholeBand.Concept);
        Assert.InRange(concept.Rect.Y, row.Top, row.Top + row.Height);
        var product = geometry.Bands.Single(b => b.Band == WholeBand.Product);
        Assert.False(concept.Rect.Y >= product.Top && concept.Rect.Y <= product.Top + product.Height);
    }

    [Fact]
    public void Each_area_keeps_its_own_column_and_the_bands_line_up_across_columns()
    {
        var geometry = WholeMapLayout.Place(Nodes(("a", "decision"), ("b", "decision"), ("a", "code"), ("b", "surface")));

        foreach (var band in geometry.Bands)
        {
            var inBand = geometry.Nodes.Where(n => NodeBands.Of(n.Kind) == band.Band).ToList();
            Assert.All(inBand, n => Assert.InRange(n.Rect.Y, band.Top, band.Top + band.Height));
        }

        Assert.Equal(2, geometry.Nodes.Select(n => n.AreaIndex).Distinct().Count());
        Assert.All(geometry.Nodes, n => Assert.InRange(n.Rect.X, geometry.Columns[n.AreaIndex].Rect.X, geometry.Columns[n.AreaIndex].Rect.Right));
    }

    [Fact]
    public void A_band_is_as_tall_as_its_fullest_column_needs()
    {
        var many = Enumerable.Range(0, 9).Select(_ => ("a", "code"));
        var geometry = WholeMapLayout.Place(Nodes([.. many, ("b", "code")]));

        var code = geometry.Bands.Single(b => b.Band == WholeBand.Code);
        var rows = (int)Math.Ceiling(9 / (double)WholeMapLayout.Across);
        Assert.True(code.Height >= rows * (WholeMapLayout.NodeHeight + WholeMapLayout.NodeGap));
    }

    [Fact]
    public void An_opened_area_keeps_the_bands_in_one_column()
    {
        var map = Nodes(("a", "decision"), ("a", "feature"), ("a", "code"), ("b", "code"));
        var opened = map with { Area = new WholeAreaInfo("a", Truncated: false, new WholeCounts(3, 0, new Dictionary<string, int>(StringComparer.Ordinal))) };

        var geometry = WholeMapLayout.Place(opened);

        _ = Assert.Single(geometry.Columns);
        Assert.Equal([WholeBand.Intent, WholeBand.Product, WholeBand.Code], geometry.Bands.Select(b => b.Band));
        Assert.All(geometry.Nodes, n => Assert.Equal(NodeBands.Of(n.Kind), geometry.Bands.Single(b => n.Rect.Y >= b.Top && n.Rect.Bottom <= b.Top + b.Height).Band));
    }

    [Fact]
    public void The_overview_is_a_column_per_area_with_its_kinds_counted_in_the_band_they_belong_to()
    {
        var map = WholeMapLayoutTests.Draft("areas-over-budget");

        var geometry = WholeMapLayout.Place(map);

        Assert.True(geometry.ByArea);
        Assert.Equal([WholeBand.Intent, WholeBand.Product, WholeBand.Code], geometry.Bands.Select(b => b.Band).Take(3));
        Assert.All(geometry.Tiles, tile =>
        {
            Assert.All(tile.BandCells, cell =>
            {
                Assert.All(cell.Kinds, k => Assert.Equal(cell.Band, NodeBands.Of(k.Key)));
                var band = geometry.Bands.Single(b => b.Band == cell.Band);
                Assert.Equal(band.Top, cell.Rect.Y);
            });
            Assert.Equal(map.Areas.Single(a => a.Key == tile.Key).ByKind.Values.Sum(), tile.BandCells.Sum(c => c.Kinds.Sum(k => k.Value)));
        });
        Assert.Equal(geometry.Tiles.Count, geometry.Tiles.Select(t => t.Rect.X).Distinct().Count());
        Assert.All(geometry.Tiles, t => Assert.Equal(geometry.Tiles[0].Rect.Y, t.Rect.Y));
    }
}
