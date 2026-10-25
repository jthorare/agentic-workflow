using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>UX pass C-3: the map has an accessible list of its items, in step with the pin, and Enter on an item does what a click on the map does.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapItemsTests
{
    private static WholeMapViewModel Nodes()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.Show(WholeMapLayoutTests.Draft("nodes-small"));
        return map;
    }

    private static WholeMapViewModel Tiles()
    {
        var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
        map.Show(WholeMapLayoutTests.Draft("areas-over-budget"));
        return map;
    }

    [Fact]
    public void A_node_level_map_lists_every_node_with_its_label_in_the_name()
    {
        var map = Nodes();

        Assert.Equal(map.Geometry!.Nodes.Select(n => n.Key), map.Items.Where(i => !i.IsEdge).Select(i => i.Key), StringComparer.Ordinal);
        Assert.All(map.Items.Where(i => !i.IsEdge), i => Assert.Contains(i.Label, i.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void The_overview_lists_its_features_and_choosing_one_asks_the_page_to_open_it()
    {
        var map = Tiles();
        var opened = new List<string>();
        map.AreaOpenRequested += (_, e) => opened.Add(e.Value);
        var item = map.Items[0];

        map.ChooseItem(item);

        Assert.True(item.IsTile);
        Assert.Equal([item.Key], opened);
    }

    [Fact]
    public void Choosing_a_node_pins_it_and_the_list_follows_the_pin()
    {
        var map = Nodes();
        var item = map.Items[1];

        map.ChooseItem(item);

        Assert.Equal(item.Key, map.Pinned);
        Assert.Equal(item, map.SelectedItem);
    }

    [Fact]
    public void A_pin_made_on_the_map_selects_its_item()
    {
        var map = Nodes();
        var node = map.Geometry!.Nodes[2];

        map.PointerClicked(node.Rect.CenterX, node.Rect.CenterY);

        Assert.Equal(node.Key, map.SelectedItem?.Key);
    }

    [Fact]
    public void A_node_level_map_lists_every_edge_after_the_nodes_named_for_both_ends()
    {
        var map = Nodes();
        var edges = map.Items.Where(i => i.IsEdge).ToList();
        var nodes = map.Geometry!.Nodes.ToDictionary(n => n.Key, n => n.Label, StringComparer.Ordinal);

        Assert.Equal(map.Geometry.Edges.Count, edges.Count);
        Assert.NotEmpty(edges);
        var first = map.Geometry.Edges[0];
        Assert.Contains(nodes[first.From], edges[0].Name, StringComparison.Ordinal);
        Assert.Contains(nodes[first.To], edges[0].Name, StringComparison.Ordinal);
        Assert.Equal(first.From, edges[0].Target);
        Assert.All(map.Items.Skip(map.Geometry.Nodes.Count), i => Assert.True(i.IsEdge));
    }

    [Fact]
    public void Choosing_an_edge_goes_to_the_node_it_starts_from_and_opens_its_card()
    {
        var map = Nodes();
        var edge = map.Items.First(i => i.IsEdge);

        map.ChooseItem(edge);

        Assert.Equal(edge.Target, map.Pinned);
        Assert.True(map.Panel.IsOpen);
    }

    [Fact]
    public void The_overview_lists_the_links_between_features_and_choosing_one_opens_the_feature_it_starts_from()
    {
        var map = Tiles();
        var opened = new List<string>();
        map.AreaOpenRequested += (_, e) => opened.Add(e.Value);
        var edge = map.Items.FirstOrDefault(i => i.IsEdge);

        Assert.NotNull(edge);
        map.ChooseItem(edge);

        Assert.Equal([edge.Target], opened);
    }
}
