using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map reacts to the pointer as the mockup does: hovering keeps a node and what it links to at full strength and dims the rest, a click keeps that until empty canvas, another node or Esc.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapHighlightTests
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

    private static (double X, double Y) Centre(WholeRect rect) => (rect.CenterX, rect.CenterY);

    [Fact]
    public void Hovering_a_node_dims_everything_but_it_and_its_neighbours()
    {
        var map = Nodes();
        var edge = map.Geometry!.Edges[0];
        var (x, y) = Centre(map.Geometry.Nodes.First(n => n.Key == edge.From).Rect);

        map.PointerMoved(x, y);

        var linked = map.Geometry.Edges.Where(e => e.From == edge.From || e.To == edge.From).SelectMany(e => new[] { e.From, e.To }).Append(edge.From).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(edge.From, map.Focus);
        Assert.True(linked.SetEquals(map.Emphasis!));
        Assert.True(map.Emphasis!.Count < map.Geometry.Nodes.Count);
    }

    [Fact]
    public void Moving_off_every_node_lets_the_highlight_go()
    {
        var map = Nodes();
        var (x, y) = Centre(map.Geometry!.Nodes[0].Rect);
        map.PointerMoved(x, y);

        map.PointerMoved(-50, -50);

        Assert.Null(map.Emphasis);
    }

    [Fact]
    public void Hovering_a_tile_keeps_the_features_it_links_to()
    {
        var map = Tiles();
        var edge = map.Geometry!.AreaEdges[0];
        var (x, y) = Centre(map.Geometry.Tiles.First(t => t.Key == edge.From).Rect);

        map.PointerMoved(x, y);

        Assert.Contains(edge.From, map.Emphasis!);
        Assert.Contains(edge.To, map.Emphasis!);
    }

    [Fact]
    public void Clicking_a_node_pins_the_highlight_until_Esc()
    {
        var map = Nodes();
        var (x, y) = Centre(map.Geometry!.Nodes[0].Rect);

        map.PointerClicked(x, y);
        map.PointerMoved(-50, -50);

        Assert.Equal(map.Geometry.Nodes[0].Key, map.Pinned);
        Assert.NotNull(map.Emphasis);
        Assert.True(map.ClearPin());
        Assert.Null(map.Emphasis);
        Assert.False(map.ClearPin());
    }

    [Fact]
    public void A_click_on_empty_canvas_lets_the_pin_go_and_a_click_on_another_node_moves_it()
    {
        var map = Nodes();
        var (a, b) = (Centre(map.Geometry!.Nodes[0].Rect), Centre(map.Geometry.Nodes[^1].Rect));
        map.PointerClicked(a.X, a.Y);

        map.PointerClicked(b.X, b.Y);
        Assert.Equal(map.Geometry.Nodes[^1].Key, map.Pinned);

        map.PointerClicked(-50, -50);
        Assert.Null(map.Pinned);
    }

    [Fact]
    public void While_pinned_hovering_another_node_shows_it_and_leaving_goes_back_to_the_pin()
    {
        var map = Nodes();
        var (a, b) = (Centre(map.Geometry!.Nodes[0].Rect), Centre(map.Geometry.Nodes[^1].Rect));
        map.PointerClicked(a.X, a.Y);

        map.PointerMoved(b.X, b.Y);
        Assert.Equal(map.Geometry.Nodes[^1].Key, map.Focus);

        map.PointerMoved(-50, -50);
        Assert.Equal(map.Geometry.Nodes[0].Key, map.Focus);
    }

    [Fact]
    public void A_second_click_on_the_pinned_tile_opens_its_feature()
    {
        var map = Tiles();
        var tile = map.Geometry!.Tiles[0];
        var opened = new List<string>();
        map.AreaOpenRequested += (_, e) => opened.Add(e.Value);
        var (x, y) = Centre(tile.Rect);

        map.PointerClicked(x, y);
        Assert.Empty(opened);
        map.PointerClicked(x, y);

        Assert.Equal([tile.Key], opened);
    }

    [Fact]
    public void A_dot_is_hit_a_little_outside_its_box_when_zoomed_out()
    {
        var map = Nodes();
        var node = map.Geometry!.Nodes.First(n => map.KeyAt(n.Rect.Right + 5, n.Rect.CenterY) is null && map.KeyAt(n.Rect.Right + 5, n.Rect.CenterY, slop: 6) is not null);

        Assert.NotNull(map.KeyAt(node.Rect.Right + 5, node.Rect.CenterY, slop: 6));
    }
}
