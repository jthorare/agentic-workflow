using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A search is the strongest highlight state on the whole map: it beats a hover or a pin, lights its matches and their links, dims the rest, and Esc or an empty box returns to the full map.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapSearchTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeMapViewModel Nodes()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        return map;
    }

    private static WholeMapViewModel Tiles()
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        return map;
    }

    private static MapSearchHit Hit(string key) => new(key, "feature", key, Area: null);

    private static (double X, double Y) Centre(WholeRect rect) => (rect.CenterX, rect.CenterY);

    [Fact]
    public void A_search_after_a_pin_clears_the_pin_and_the_highlight_is_the_search()
    {
        var map = Nodes();
        var edge = map.Geometry!.Edges[0];
        var other = map.Geometry.Nodes.First(n => n.Key != edge.From && n.Key != edge.To);
        var (x, y) = Centre(other.Rect);
        map.PointerClicked(x, y);

        map.ApplySearch([Hit(edge.From)], hitAreas: [], zoomed: false);

        Assert.Null(map.Pinned);
        Assert.Null(map.Focus);
        Assert.Contains(edge.From, map.Emphasis!);
        Assert.Contains(edge.To, map.Emphasis!);
        Assert.DoesNotContain(other.Key, map.Emphasis!);
    }

    [Fact]
    public void While_a_search_is_active_hovering_changes_nothing()
    {
        var map = Nodes();
        var edge = map.Geometry!.Edges[0];
        map.ApplySearch([Hit(edge.From)], hitAreas: [], zoomed: false);
        var before = map.Emphasis;
        var other = map.Geometry.Nodes.First(n => n.Key != edge.From && n.Key != edge.To);
        var (x, y) = Centre(other.Rect);

        map.PointerMoved(x, y);

        Assert.Same(before, map.Emphasis);
        Assert.Null(map.Focus);
    }

    [Fact]
    public void A_search_lights_its_matches_with_their_links_and_asks_the_camera_to_fit_them()
    {
        var map = Nodes();
        var edge = map.Geometry!.Edges[0];
        var asked = new List<WholeRect>();
        map.ZoomRequested += (_, e) => asked.Add(e.Value);

        map.ApplySearch([Hit(edge.From)], hitAreas: [], zoomed: false);

        var node = map.Geometry.Nodes.First(n => n.Key == edge.From);
        Assert.Equal(WholeZoomBox.Around([node.Rect]), Assert.Single(asked));
        Assert.False(map.NoMatches);
    }

    [Fact]
    public void A_hit_inside_a_collapsed_feature_lights_its_tile()
    {
        var map = Tiles();
        var tile = map.Geometry!.Tiles[0];

        map.ApplySearch([new MapSearchHit("item:deep", "file", "deep", tile.Key)], hitAreas: [tile.Key], zoomed: false);

        Assert.Contains(tile.Key, map.Emphasis!);
        Assert.False(map.NoMatches);
    }

    [Fact]
    public void No_matches_says_so_and_dims_the_whole_graph()
    {
        var map = Nodes();
        var moved = false;
        map.ZoomRequested += (_, _) => moved = true;

        map.ApplySearch([], hitAreas: [], zoomed: false);

        Assert.True(map.NoMatches);
        Assert.True(map.SearchActive);
        Assert.Empty(map.Emphasis!);
        Assert.False(moved);
    }

    [Fact]
    public void Ending_the_search_lights_everything_again_and_fits_all()
    {
        var map = Nodes();
        map.ApplySearch([], hitAreas: [], zoomed: false);
        var fits = 0;
        map.FitRequested += (_, _) => fits++;

        map.EndSearch();

        Assert.Equal((false, false, 1), (map.SearchActive, map.NoMatches, fits));
        Assert.Null(map.Emphasis);
    }

    [Fact]
    public void A_second_click_on_the_pinned_node_lets_the_pin_go_so_the_highlight_cannot_stick()
    {
        var map = Nodes();
        var (x, y) = Centre(map.Geometry!.Nodes[0].Rect);
        map.PointerClicked(x, y);

        map.PointerClicked(x, y);
        map.PointerMoved(-50, -50);

        Assert.Null(map.Pinned);
        Assert.Null(map.Emphasis);
    }
}
