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
    public void A_band_with_no_nodes_is_thin_and_says_none_in_this_area()
    {
        var map = AreaCanvasLayout.Place([Node("OrderService", "code")], [], 1440);

        var entry = map.AreaBands!.Single(b => string.Equals(b.Title, "Entry points", StringComparison.Ordinal));
        Assert.True(entry.IsEmpty);
        Assert.Equal(AreaCanvasLayout.EmptyWidth, entry.Rect.Width);
    }

    [Fact]
    public void Core_draws_Requests_Services_and_Responses_side_by_side_from_1390_px_and_stacked_below()
    {
        WholeNode[] nodes = [Node("InitRequest", "code"), Node("InitService", "code"), Node("InitResponse", "code")];

        var wide = AreaCanvasLayout.Place(nodes, [], 1440).AreaBands!.Where(b => b.Sub).Select(b => (b.Title, b.Rect.X, b.Rect.Y)).ToList();
        var narrow = AreaCanvasLayout.Place(nodes, [], 1200).AreaBands!.Where(b => b.Sub).Select(b => (b.Title, b.Rect.X, b.Rect.Y)).ToList();

        Assert.True(wide.Select(w => w.Title).SequenceEqual(["Requests", "Services and rules", "Responses"], StringComparer.Ordinal));
        Assert.True(wide[0].X < wide[1].X && wide[1].X < wide[2].X);
        Assert.All(wide, w => Assert.Equal(wide[0].Y, w.Y));
        _ = Assert.Single(narrow.Select(w => w.X).Distinct());
        Assert.True(narrow[0].Y < narrow[1].Y && narrow[1].Y < narrow[2].Y);
    }

    [Fact]
    public void A_link_runs_from_one_node_s_edge_to_the_other_s_and_a_link_to_a_missing_node_is_left_out()
    {
        var map = AreaCanvasLayout.Place([Node("InitRequest", "code"), Node("InitService", "code")], [Link("InitRequest", "InitService"), Link("InitRequest", "Gone")], 1440);

        var edge = Assert.Single(map.Edges);
        var (a, b) = (map.Nodes.Single(n => string.Equals(n.Label, "InitRequest", StringComparison.Ordinal)).Rect, map.Nodes.Single(n => string.Equals(n.Label, "InitService", StringComparison.Ordinal)).Rect);
        Assert.Equal((a.Right, b.X), (edge.X1, edge.X2));
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

        map.ViewportChanged(new WholeRect(0, 0, 2000, 150));

        Assert.True(map.ShowAreaBar);
        Assert.Equal("30 nodes · 3 in view", map.AreaBarText);
        Assert.True(map.ShowMoreBelow);
        Assert.Equal("27 more below", map.MoreBelowText);
    }
}
