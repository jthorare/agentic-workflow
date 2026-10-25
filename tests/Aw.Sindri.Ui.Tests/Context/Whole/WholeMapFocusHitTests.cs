using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A hit chosen in the list: a drawn node is pinned and centred; one that is not drawn goes to its feature's tile, as unknown keys do in a search.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapFocusHitTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static WholeMapViewModel Map(string golden)
    {
        var map = new WholeMapViewModel(() => _utc);
        map.Show(WholeMapLayoutTests.Draft(golden));
        return map;
    }

    [Fact]
    public void A_drawn_node_is_pinned_and_centred()
    {
        var map = Map("nodes-small");
        var node = map.Geometry!.Nodes[0];
        WholeRect? centred = null;
        map.CentreRequested += (_, e) => centred = e.Value;

        var focused = map.FocusHit(new MapSearchHit(node.Key, "feature", node.Label, Area: null));

        Assert.Equal((true, node.Key, node.Rect), (focused, map.Pinned, centred));
    }

    [Fact]
    public void A_hit_that_is_not_drawn_goes_to_its_features_tile()
    {
        var map = Map("areas-over-budget");
        var tile = map.Geometry!.Tiles[0];
        WholeRect? centred = null;
        map.CentreRequested += (_, e) => centred = e.Value;

        var focused = map.FocusHit(new MapSearchHit("item:deep", "file", "deep", tile.Key));

        Assert.Equal((true, tile.Key, tile.Rect), (focused, map.Pinned, centred));
    }

    [Fact]
    public void A_hit_the_map_does_not_hold_changes_nothing()
    {
        var map = Map("nodes-small");

        var focused = map.FocusHit(new MapSearchHit("item:nowhere", "file", "nowhere", Area: "area:none"));

        Assert.False(focused);
        Assert.Null(map.Pinned);
    }
}
