using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>An opened area's canvas (#3674): Entry points, Core with its sub-groups, Data and effects; the bar and the fade from what is on screen.</summary>
[Lane(TestLane.Unit)]
public sealed class AreaCanvasLayoutTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeNode Node(string label, string kind) => new($"code:{label}", label, kind, "area", [], "trusted", "deterministic", 1, Confirmed: false);

    private static WholeEdge Link(string from, string to) => new($"code:{from}", $"code:{to}", "calls", "l", "deterministic", 1, Confirmed: true);

    [Fact]
    public void A_route_sits_in_Entry_points_a_table_in_Data_and_effects_and_the_rest_in_Core()
    {
        var map = AreaCanvasLayout.Place([Node("GetRoute", "route"), Node("Orders", "table"), Node("OrderService", "code")], [], 1440);

        var x = map.Nodes.ToDictionary(n => n.Label, n => n.Rect.X, StringComparer.Ordinal);
        Assert.True(x["GetRoute"] < x["OrderService"]);
        Assert.True(x["OrderService"] < x["Orders"]);
        Assert.True(map.AreaBands!.Where(b => !b.Sub).Select(b => b.Title).SequenceEqual(["Entry points", "Core", "Data and effects"], StringComparer.Ordinal));
    }

    [Fact]
    public void A_band_with_no_nodes_is_as_wide_as_its_heading_and_says_none_in_this_area()
    {
        var map = AreaCanvasLayout.Place([Node("OrderService", "code")], [], 1440);

        var entry = map.AreaBands!.Single(b => string.Equals(b.Title, "Entry points", StringComparison.Ordinal));
        Assert.True(entry.IsEmpty);
        Assert.InRange(entry.Rect.Width, AreaCanvasLayout.EmptyWidth, 200);   // the title, its hint under it, never a full column
    }

    [Fact]
    public void The_arrangement_is_the_one_that_fits_the_stage_biggest()
    {
        var nodes = PaymentsLike();

        var map = AreaCanvasLayout.Place(nodes, [], 1280, 1030, 400);

        var scale = Math.Min(1, Math.Min(1030 / map.World.Width, 400 / map.World.Height));
        Assert.True(scale >= 0.9, $"fit {scale.ToString("0.00", CultureInfo.InvariantCulture)}");
        AssertNoOverlap(map);
    }

    private static List<WholeNode> PaymentsLike() => [.. Enumerable.Range(0, 20).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"Svc{i:00}Service"), "code")),
        Node("PayRequest", "code"), Node("RefundRequest", "code"), Node("VoidRequest", "code"),
        Node("PayResponse", "code"), Node("RefundResponse", "code"), Node("VoidResponse", "code")];

    private static void AssertNoOverlap(WholeMapGeometry map)
    {
        for (var i = 0; i < map.Nodes.Count; i++)
        {
            for (var j = i + 1; j < map.Nodes.Count; j++)
            {
                Assert.False(map.Nodes[i].Rect.Intersects(map.Nodes[j].Rect), $"{map.Nodes[i].Label} overlaps {map.Nodes[j].Label}");
            }
        }
    }

    [Fact]
    public void A_group_of_twenty_wraps_into_columns_and_the_payments_area_fits_a_1024_by_540_stage()
    {
        var nodes = Enumerable.Range(0, 20).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"Svc{i:00}Service"), "code")).Concat(
            [Node("PayRequest", "code"), Node("RefundRequest", "code"), Node("VoidRequest", "code")]).ToList();

        var map = AreaCanvasLayout.Place(nodes, [], 1280, 1024, 540);

        Assert.True(map.World.Width <= 1100 && map.World.Height <= 560, "fits a 1024 by 540 stage");
        for (var i = 0; i < map.Nodes.Count; i++)
        {
            for (var j = i + 1; j < map.Nodes.Count; j++)
            {
                Assert.False(map.Nodes[i].Rect.Intersects(map.Nodes[j].Rect), $"{map.Nodes[i].Label} overlaps {map.Nodes[j].Label}");
            }
        }

        Assert.True(map.Nodes.Max(n => n.Rect.Bottom) <= map.World.Height);
        Assert.True(map.Nodes.Where(n => n.Label.EndsWith("Service", StringComparison.Ordinal)).Select(n => n.Rect.X).Distinct().Skip(1).Any());
    }

    [Fact]
    public void A_wrapped_group_fills_column_by_column_in_label_order()
    {
        var nodes = Enumerable.Range(0, 10).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"Item{i:00}Service"), "code")).ToList();

        var map = AreaCanvasLayout.Place(nodes, [], 1440);

        var first = map.Nodes.OrderBy(n => n.Rect.X).ThenBy(n => n.Rect.Y).Select(n => n.Label).ToList();
        Assert.True(first.SequenceEqual(first.Order(StringComparer.Ordinal), StringComparer.Ordinal));
        Assert.Equal(2, map.Nodes.Select(n => n.Rect.X).Distinct().Count());
    }

    [Fact]
    public void An_empty_band_and_Core_are_as_wide_as_their_heading_words()
    {
        var map = AreaCanvasLayout.Place([Node("OrderService", "code")], [], 1440);

        foreach (var band in map.AreaBands!.Where(b => !b.Sub))
        {
            Assert.True(band.Rect.Width >= Math.Max(band.Title.Length * 8, band.Caption.Length * 7.2) - 0.001, band.Title);
        }
    }

    [Fact]
    public void A_link_runs_from_one_node_s_edge_to_the_other_s_and_a_link_to_a_missing_node_is_left_out()
    {
        var map = AreaCanvasLayout.Place([Node("InitRequest", "code"), Node("InitService", "code")], [Link("InitRequest", "InitService"), Link("InitRequest", "Gone")], 1440, 3000, 100);

        var edge = Assert.Single(map.Edges);
        var (a, b) = (map.Nodes.Single(n => string.Equals(n.Label, "InitRequest", StringComparison.Ordinal)).Rect, map.Nodes.Single(n => string.Equals(n.Label, "InitService", StringComparison.Ordinal)).Rect);
        Assert.True(Math.Abs(a.X - b.X) >= 1, "a wide, short stage puts the two groups in two columns");
        Assert.Equal((a.Right, a.CenterY, b.X, b.CenterY), (edge.X1, edge.Y1, edge.X2, edge.Y2));
    }

    [Fact]
    public void A_link_inside_one_column_runs_from_the_lower_edge_of_the_upper_node_to_the_top_of_the_lower()
    {
        var map = AreaCanvasLayout.Place([Node("AService", "code"), Node("BService", "code")], [Link("AService", "BService")], 1440, 3000, 3000);

        var edge = Assert.Single(map.Edges);
        var (a, b) = (map.Nodes.Single(n => string.Equals(n.Label, "AService", StringComparison.Ordinal)).Rect, map.Nodes.Single(n => string.Equals(n.Label, "BService", StringComparison.Ordinal)).Rect);
        Assert.True(Math.Abs(a.X - b.X) < 1);
        var (upper, lower) = a.Y < b.Y ? (a, b) : (b, a);
        Assert.Equal(upper.Bottom, Math.Min(edge.Y1, edge.Y2));
        Assert.Equal(lower.Y, Math.Max(edge.Y1, edge.Y2));
    }

    [Fact]
    public async Task The_fade_counts_every_node_out_of_view_not_only_the_ones_below()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        var draft = WholeMapLayoutTests.Draft("area-open");
        var rows = Enumerable.Range(0, 10).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"Thing{i:00}"), "code") with { Area = draft.Area?.Key ?? string.Empty }).ToList();
        map.ShowArea(draft with { Nodes = rows, Edges = [], Area = draft.Area! with { Counts = draft.Area.Counts with { Nodes = 10 } } });
        await map.AreaLaidOut;

        map.ViewportChanged(new WholeRect(-5000, -5000, 100, 100));

        Assert.Equal("10 nodes · 0 in view", map.AreaBarText);
        Assert.True(map.ShowMoreBelow);
        Assert.Equal("10 more below", map.MoreBelowText);
    }

    [Fact]
    public async Task The_bar_counts_the_nodes_in_view_and_the_fade_the_ones_below_the_screen()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        var draft = WholeMapLayoutTests.Draft("area-open");
        var rows = Enumerable.Range(0, 30).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"Thing{i:00}"), "code") with { Area = draft.Area?.Key ?? string.Empty }).ToList();
        map.ShowArea(draft with { Nodes = rows, Edges = [], Area = draft.Area! with { Counts = draft.Area.Counts with { Nodes = 30 } } });
        await map.AreaLaidOut;

        var view = new WholeRect(0, 0, 2000, 150);
        map.ViewportChanged(view);

        var inView = map.Geometry!.Nodes.Count(n => n.Rect.Intersects(view));
        Assert.InRange(inView, 1, 29);
        Assert.True(map.ShowAreaBar);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"30 nodes · {inView} in view"), map.AreaBarText);
        Assert.True(map.ShowMoreBelow);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{30 - inView} more below"), map.MoreBelowText);
    }
}

