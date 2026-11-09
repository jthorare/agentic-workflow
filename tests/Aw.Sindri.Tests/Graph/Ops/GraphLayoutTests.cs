using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Layout;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph.Ops;

[Lane(TestLane.Unit)]
public sealed class GraphLayoutTests
{
    [Fact]
    public void A_chain_gets_one_layer_per_step_and_a_node_sits_in_the_layer_of_its_longest_path()
    {
        // daily -> a -> b -> c, and daily -> c directly: c is in layer 3, not 1
        var spec = Spec([Trigger("daily"), Search("a"), Search("b"), Search("c")], [Edge("daily", "a"), Edge("a", "b"), Edge("b", "c"), Edge("daily", "c")]);

        var layout = GraphLayout.Compute(spec);

        Assert.Equal([0, 1, 2, 3], new[] { "daily", "a", "b", "c" }.Select(id => layout[id].Layer));
        Assert.All(layout.Values, p => Assert.Equal(0, p.Row));
    }

    [Fact]
    public void Nodes_of_one_layer_take_rows_in_spec_order_whatever_the_edge_order()
    {
        // spec order is daily, z, a, m; the edges name them m, a, z
        var spec = Spec([Trigger("daily"), Search("z"), Search("a"), Search("m")], [Edge("daily", "m"), Edge("daily", "a"), Edge("daily", "z")]);

        var layout = GraphLayout.Compute(spec);

        Assert.Equal([1, 1, 1], new[] { "z", "a", "m" }.Select(id => layout[id].Layer));
        Assert.Equal([0, 1, 2], new[] { "z", "a", "m" }.Select(id => layout[id].Row));
    }

    [Fact]
    public void Positions_follow_the_layer_and_row()
    {
        var spec = Spec([Trigger("daily"), Search("a"), Search("b")], [Edge("daily", "a"), Edge("daily", "b")]);

        var layout = GraphLayout.Compute(spec);

        Assert.Equal(new GraphPosition(0, 0, 0, 0), layout["daily"]);
        Assert.Equal(new GraphPosition(1, 1, GraphLayout.LayerWidth, GraphLayout.RowHeight), layout["b"]);
    }

    [Fact]
    public void A_node_nothing_feeds_starts_layer_zero_and_a_loop_still_gets_positions()
    {
        var loose = Spec([Trigger("daily"), Search("orphan")], []);
        var loop = Spec([Trigger("daily"), Search("a"), Search("b")], [Edge("daily", "a"), Edge("a", "b"), Edge("b", "a")]);

        Assert.Equal(0, GraphLayout.Compute(loose)["orphan"].Layer);
        var looped = GraphLayout.Compute(loop);
        Assert.Equal(3, looped.Count);
        Assert.Equal(0, looped["daily"].Layer);
    }

    [Fact]
    public void The_layout_is_the_same_every_time()
    {
        var spec = Spec([Trigger("daily"), Search("a"), Search("b")], [Edge("daily", "a"), Edge("daily", "b")]);

        Assert.Equal(GraphLayout.Compute(spec), GraphLayout.Compute(spec));
    }
}
