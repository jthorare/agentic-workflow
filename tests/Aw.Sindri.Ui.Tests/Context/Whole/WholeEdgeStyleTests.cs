using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's edges (q86): confirmed solid, suggested dashed with the percentage, one bundled line per pair of areas, node edges only inside an area or around the focus.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeEdgeStyleTests
{
    private static PlacedEdge Edge(string from, string to, bool confirmed = true, double confidence = 1, string type = "part-of") => new(from, to, type, confirmed, 0, 0, 10, 10, confidence);

    private static PlacedNode Node(string key, int area) => new(key, key, "feature", area, new WholeRect(0, 0, 10, 10), Confirmed: true);

    [Fact]
    public void A_confirmed_edge_is_solid_and_its_pill_is_the_words()
    {
        var style = WholeEdgeStyle.Of(Edge("a", "b"));

        Assert.False(style.Dashed);
        Assert.Equal("part of", style.Pill);
    }

    [Fact]
    public void A_suggested_edge_is_dashed_and_its_pill_has_the_confidence()
    {
        var style = WholeEdgeStyle.Of(Edge("a", "b", confirmed: false, confidence: 0.62, type: "implements"));

        Assert.True(style.Dashed);
        Assert.Equal("implemented in · suggested 62%", style.Pill);
    }

    [Fact]
    public void Node_edges_draw_inside_an_area_and_across_areas_only_around_the_focus()
    {
        PlacedNode[] nodes = [Node("a", 0), Node("b", 0), Node("c", 1)];
        PlacedEdge[] edges = [Edge("a", "b"), Edge("b", "c")];

        Assert.Equal([edges[0]], WholeEdgeSelection.Visible(edges, nodes, focus: null));
        Assert.Equal(edges, WholeEdgeSelection.Visible(edges, nodes, "c"));
    }

    [Fact]
    public void Pills_show_on_an_uncluttered_drawing_and_otherwise_only_around_the_focus()
    {
        var edge = Edge("a", "b");

        Assert.True(WholeEdgeSelection.ShowsPill(edge, WholeEdgeSelection.PillLimit, focus: null));
        Assert.False(WholeEdgeSelection.ShowsPill(edge, WholeEdgeSelection.PillLimit + 1, focus: null));
        Assert.True(WholeEdgeSelection.ShowsPill(edge, WholeEdgeSelection.PillLimit + 1, "b"));
    }
}
