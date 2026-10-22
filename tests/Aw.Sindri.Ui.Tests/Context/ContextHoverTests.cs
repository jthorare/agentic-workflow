using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Hover and the highlight around a node (F3 spec 1 §2, "Hover rule"): the neighbour set, the dim set, and a cite lighting the keys it names.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHoverTests
{
    private const string Groups = "feature:tabs.tab-groups";
    private const string Manager = "feature:tabs.tab-manager";
    private const string ManagerFile = "code:src/tabs/TabManager.ts";
    private static readonly MapLink[] _links = [new("e1", "a", "b", "part-of"), new("e2", "b", "c", "on"), new("e3", "c", "d", "reads")];

    private static ContextMapViewModel Map()
    {
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(GoldenFiles.Node("context-lenses", "map-tabs").ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess);
        return new ContextMapViewModel(read.Value);
    }

    [Fact]
    public void A_node_lights_itself_its_neighbours_and_the_edges_between()
    {
        var around = MapHighlight.Around(_links, ["b"]);

        Assert.Equal(["a", "b", "c"], around.Nodes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["e1", "e2"], around.Edges.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void Everything_outside_the_set_is_dimmed()
    {
        var around = MapHighlight.Around(_links, ["a"]);

        Assert.False(around.Dims("b"));
        Assert.True(around.Dims("c"));
        Assert.True(around.DimsEdge("e2"));
        Assert.False(around.DimsEdge("e1"));
    }

    [Fact]
    public void A_cite_lights_the_keys_it_names_and_the_edges_among_them_without_their_neighbours()
    {
        var cited = MapHighlight.Among(_links, ["a", "b", "d"]);

        Assert.Equal(["a", "b", "d"], cited.Nodes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["e1"], cited.Edges.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.True(cited.Dims("c"));
    }

    [Fact]
    public void Hovering_a_node_dims_the_rest_of_the_map_and_leaving_restores_it()
    {
        var map = Map();

        map.Hover([Groups]);

        Assert.Equal(1.0, map.Node(Groups)!.Opacity);
        Assert.Equal(1.0, map.Node(Manager)!.Opacity);   // part of the groups feature: a neighbour
        Assert.Equal(0.42, map.Node(ManagerFile)!.Opacity);
        Assert.NotEmpty(map.HighlightedEdges);
        Assert.NotEmpty(map.DimmedEdges);

        map.ClearHover();

        Assert.All(map.Nodes, n => Assert.Equal(1.0, n.Opacity));
        Assert.Empty(map.DimmedEdges);
    }

    [Fact]
    public void A_hover_only_label_shows_while_one_end_of_its_edge_is_hovered_or_selected()
    {
        var map = Map();
        var partOf = map.Pills.Single(p => p.Edge.From == Groups && p.Edge.To == Manager);
        Assert.False(partOf.Always);
        Assert.Equal(0.0, partOf.Opacity);

        map.Hover([Groups]);
        Assert.Equal(1.0, partOf.Opacity);

        map.ClearHover();
        Assert.Equal(0.0, partOf.Opacity);

        Assert.True(map.Select(Groups));
        Assert.Equal(1.0, partOf.Opacity);
    }

    [Fact]
    public void A_selection_keeps_the_highlight_when_the_hover_leaves()
    {
        var map = Map();
        Assert.True(map.Select(Manager));

        map.Hover([Groups]);
        map.ClearHover();

        Assert.Equal(1.0, map.Node(Manager)!.Opacity);
        Assert.Equal(1.0, map.Node(Groups)!.Opacity);   // a neighbour of the selection stays lit
        Assert.True(map.Node(Manager)!.IsSelected);
    }

    [Fact]
    public void A_cite_hover_lights_only_the_cited_keys()
    {
        var map = Map();

        map.Hover([Manager, ManagerFile], neighbours: false);

        Assert.Equal(1.0, map.Node(Manager)!.Opacity);
        Assert.Equal(1.0, map.Node(ManagerFile)!.Opacity);
        Assert.Equal(0.42, map.Node(Groups)!.Opacity);
    }
}
