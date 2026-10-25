using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Filtering the whole map zooms to what the filter keeps (#1096): one camera move to the box the map fits, the matches and their links, and no refit that undoes it.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapFilterCameraTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;
    private static readonly string[] _areas = ["a", "b", "c", "d", "e"];

    // Five features; the match sits in the first column and the one node it links to in the last, far enough apart that a box round the match alone leaves the link out.
    private static (WholeMapViewModel Map, string Match, string Link) Spread()
    {
        var areas = _areas.Select(a => new WholeArea(a, a, "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), Unassigned: false)).ToList();
        var order = WholeMapLayout.Place(WholeMapLayoutTests.Draft("nodes-small") with { Areas = areas, AreaEdges = [], Nodes = [], Edges = [] }).Order;
        var nodes = new List<WholeNode>
        {
            Node(0, "decision", order[0]),
            Node(1, "code", order[^1]),
            Node(2, "feature", order[2]),
        };
        var edge = new WholeEdge(nodes[0].Key, nodes[1].Key, "implements", "link:0", "human", 1, Confirmed: true);
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small") with { Areas = areas, AreaEdges = [], Nodes = nodes, Edges = [edge] });
        return (map, nodes[0].Key, nodes[1].Key);
    }

    private static WholeNode Node(int i, string kind, string area) =>
        new("n:" + i.ToString(CultureInfo.InvariantCulture), "Node " + i.ToString(CultureInfo.InvariantCulture), kind, area, [], "trusted", "human", 1, Confirmed: true);

    [Fact]
    public void A_search_asks_the_camera_for_the_box_the_map_fits_so_the_links_are_framed_with_the_matches()
    {
        var (map, match, link) = Spread();
        var asked = new List<WholeRect>();
        map.ZoomRequested += (_, e) => asked.Add(e.Value);

        map.ApplySearch([new MapSearchHit(match, "decision", "Node 0", Area: null)], hitAreas: [], zoomed: false);

        var box = Assert.Single(asked);
        Assert.Equal(map.FitBox, box);
        Assert.All(map.Shown!.Nodes, n => Assert.True(box.Contains(n.Rect.CenterX, n.Rect.CenterY), n.Key));
        Assert.Contains(map.Shown.Nodes, n => n.Key == link);
    }

    [Fact]
    public void A_zoom_onto_an_answer_asks_the_camera_for_the_box_the_map_fits()
    {
        var (map, match, link) = Spread();
        var asked = new List<WholeRect>();
        map.ZoomRequested += (_, e) => asked.Add(e.Value);

        Assert.True(map.Zoom([match]));

        var box = Assert.Single(asked);
        Assert.Equal(map.FitBox, box);
        Assert.Contains(map.Shown!.Nodes, n => n.Key == link && box.Contains(n.Rect.CenterX, n.Rect.CenterY));
    }

    [Theory]
    [InlineData(false, false, false, true)]    // the first open fits the map
    [InlineData(true, true, false, true)]      // a resize while the person has not moved the view fits it again
    [InlineData(true, false, false, false)]    // a view the person moved is kept
    [InlineData(false, false, true, false)]    // a filter has moved the camera itself: no refit puts it back to a floor-clipped fit
    [InlineData(true, true, true, false)]
    public void A_refit_never_runs_while_the_map_is_filtered(bool fitted, bool viewUnmoved, bool filtering, bool expected) =>
        Assert.Equal(expected, WholeMapViewModel.RefitsOnArrange(fitted, viewUnmoved, filtering));
}
