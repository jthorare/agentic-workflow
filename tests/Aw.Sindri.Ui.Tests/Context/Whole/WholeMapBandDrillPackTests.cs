using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A drilled band packs the columns it keeps side by side (#1645), so its items stay readable instead of dots.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandDrillPackTests
{
    private const int Areas = 17;
    private const double CanvasWidth = 1344;

    /// <summary>A concept with 17 area columns: a decision in the first area decides a feature in the last, unlinked fillers keep the columns between.</summary>
    private static WholeMapGeometry FarApart(string linkType, string firstKind, string lastKind)
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var template = map.Areas[0];
        var areas = Enumerable.Range(0, Areas).Select(i => template with { Key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"area:a{i:00}"), Label = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Area {i:00}"), Unassigned = false }).ToList();
        WholeNode Node(string key, string kind, int area) => new(key, key, kind, areas[area].Key, [], "trusted", "human", 1, Confirmed: true);
        var nodes = new List<WholeNode> { Node("n:first", firstKind, 0), Node("n:last", lastKind, Areas - 1) };
        nodes.AddRange(Enumerable.Range(1, Areas - 2).Select(i => Node(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"filler:{i}"), "code", i)));
        return WholeMapLayout.Place(map with
        {
            Areas = areas,
            AreaEdges = [],
            Nodes = nodes,
            Edges = [new WholeEdge("n:first", "n:last", linkType, "edge:first>last", "human", 1, Confirmed: true)],
        });
    }

    private static double ColumnWidth(WholeMapGeometry whole) => whole.Columns[1].Rect.X - whole.Columns[0].Rect.X;

    private static void ExpectPacked(WholeMapGeometry whole, WholeMapGeometry shown)
    {
        var first = shown.Nodes.Single(n => n.Key == "n:first");
        var last = shown.Nodes.Single(n => n.Key == "n:last");
        Expect.True(whole.Nodes.Single(n => n.Key == "n:last").AreaIndex - whole.Nodes.Single(n => n.Key == "n:first").AreaIndex > 1);
        Expect.True(first.Rect.X < last.Rect.X);
        Expect.Equal(ColumnWidth(whole), last.Rect.X - first.Rect.X);
        Expect.Equal(ColumnWidth(whole), shown.Columns[1].Rect.X - shown.Columns[0].Rect.X);
        foreach (var node in shown.Nodes)
        {
            Expect.Equal(whole.Nodes.Single(n => n.Key == node.Key).Rect.Y, node.Rect.Y);
        }

        var edge = shown.Edges.Single();
        Expect.Equal(first.Rect.CenterX, edge.X1);
        Expect.Equal(first.Rect.CenterY, edge.Y1);
        Expect.Equal(last.Rect.CenterX, edge.X2);
        Expect.Equal(last.Rect.CenterY, edge.Y2);
        Expect.True(CanvasWidth / WholeMapFilter.FitBox(shown, shown.World).Width >= WholeMapViewModel.DotsBelow);
    }

    [Fact]
    public void Why_across_packs_far_apart_columns_side_by_side()
    {
        var whole = FarApart("decides", "decision", "feature");

        ExpectPacked(whole, WholeMapBandDrill.Cross(whole, WholeBand.Intent));
    }

    [Fact]
    public void Product_inside_packs_far_apart_columns_side_by_side()
    {
        var whole = FarApart("part-of", "feature", "feature");

        ExpectPacked(whole, WholeMapBandDrill.Own(whole, WholeBand.Product));
    }
}
