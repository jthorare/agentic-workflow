using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

public sealed class GraphWalkTests
{
    [Fact, Trait("Lane", "unit")]
    public void Neighbors_at_depth_1_are_the_nodes_one_edge_away_either_way_with_their_edges()
    {
        var hood = GraphWalk.Neighbors(FixtureGraph.Applied(), FixtureGraph.Key("thing:pane-layout"), depth: 1);

        Assert.Equal(
            [("thing:pane-layout", 0), ("action:pane-resize.resize-focused", 1), ("action:split-pane.toggle-orientation", 1), ("action:tab-manager.restore-layout", 1)],
            hood.Nodes.Select(n => (n.Node.Key.Value, n.Depth)));
        Assert.Equal(3, hood.Edges.Count);
        Assert.All(hood.Edges, e => Assert.Equal("thing:pane-layout", e.To.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Neighbors_at_depth_0_are_the_center_alone()
    {
        var hood = GraphWalk.Neighbors(FixtureGraph.Applied(), FixtureGraph.Key("thing:pane-layout"), depth: 0);

        Assert.Equal(["thing:pane-layout"], hood.Nodes.Select(n => n.Node.Key.Value), StringComparer.Ordinal);
        Assert.Empty(hood.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shortest_path_walks_edges_either_way()
    {
        var path = GraphWalk.ShortestPath(FixtureGraph.Applied(), FixtureGraph.Key("feature:layout.split-pane.orientation"), FixtureGraph.Key("feature:tabs.tab-manager"));

        Assert.NotNull(path);
        Assert.Equal(6, path.Steps.Count);
        Assert.Equal("feature:tabs.tab-manager", path.Steps[^1].Node.Value);
        Assert.False(path.Steps[0].Forward);   // the first step goes back along a part-of or an implements edge
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(40, true)]
    public void A_path_of_any_length_is_found(int length, bool found)
    {
        var builder = new GraphBuilder();
        for (var i = 0; i < length; i++)
        {
            _ = builder.Edge(Key(i + 1), EdgeType.DependsOn, Key(i));
        }

        var path = GraphWalk.ShortestPath(builder.Build(), NodeKey.From("feature:n0"), NodeKey.From(Key(length)));

        Assert.Equal(found, path is not null);
        Assert.Equal(found ? length : 0, path?.Steps.Count ?? 0);
    }

    private static string Key(int i) => "feature:n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public void A_path_to_itself_is_empty_and_none_between_unconnected_nodes()
    {
        var graph = new GraphBuilder().Node("feature:a").Node("feature:b").Build();

        Assert.Empty(GraphWalk.ShortestPath(graph, NodeKey.From("feature:a"), NodeKey.From("feature:a"))!.Steps);
        Assert.Null(GraphWalk.ShortestPath(graph, NodeKey.From("feature:a"), NodeKey.From("feature:b")));
    }
}
