using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Tests.Graph.Loops;

[Lane(TestLane.Unit)]
public sealed class LoopBodyTests
{
    private static GraphNode Node(string id, string type) => new(NodeId.From(id), type, Title: null, []);

    private static GraphEdge Edge(string from, string to, string? output = null) => new(NodeId.From(from), output, NodeId.From(to));

    private static GraphSpec Spec(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) => new(1, "loops", Description: null, Concurrency: null, Resources: null, nodes, edges);

    private static readonly GraphSpec _linear = Spec(
        [Node("start", "manual"), Node("each", "for_each"), Node("a", "echo"), Node("b", "echo"), Node("gather", "collect"), Node("after", "echo")],
        [Edge("start", "each"), Edge("each", "a"), Edge("a", "b"), Edge("b", "gather"), Edge("gather", "after")]);

    [Fact]
    public void The_body_is_every_node_between_the_for_each_and_its_collect_in_spec_order()
    {
        var found = LoopBody.Find(_linear, NodeId.From("each"));

        Assert.True(found.IsSuccess);
        Assert.Equal(["a", "b"], found.Value.Body.Select(static n => n.Id.Value), StringComparer.Ordinal);
        Assert.Equal("gather", found.Value.Collect.Id.Value);
        Assert.Equal(["a", "b", "gather"], found.Value.PerItem.Select(static n => n.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_for_each_wired_straight_to_its_collect_has_an_empty_body()
    {
        var spec = Spec([Node("start", "manual"), Node("each", "for_each"), Node("gather", "collect")], [Edge("start", "each"), Edge("each", "gather")]);

        var found = LoopBody.Find(spec, NodeId.From("each"));

        Assert.True(found.IsSuccess);
        Assert.Empty(found.Value.Body);
    }

    [Fact]
    public void A_branch_inside_the_body_is_part_of_it()
    {
        var spec = Spec(
            [Node("start", "manual"), Node("each", "for_each"), Node("check", "condition"), Node("yes", "echo"), Node("no", "echo"), Node("gather", "collect")],
            [Edge("start", "each"), Edge("each", "check"), Edge("check", "yes", "true"), Edge("check", "no", "false"), Edge("yes", "gather"), Edge("no", "gather")]);

        var found = LoopBody.Find(spec, NodeId.From("each"));

        Assert.Equal(["check", "yes", "no"], found.Value.Body.Select(static n => n.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_for_each_with_no_collect_after_it_is_refused_and_names_the_node()
    {
        var spec = Spec([Node("start", "manual"), Node("each", "for_each"), Node("a", "echo")], [Edge("start", "each"), Edge("each", "a")]);

        var found = LoopBody.Find(spec, NodeId.From("each"));

        Assert.True(found.IsError);
        Assert.Contains("collect", found.Error.Message, StringComparison.Ordinal);
        Assert.Contains("'each'", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_fed_by_both_the_body_and_the_collect_is_refused()
    {
        var spec = Spec(
            [Node("start", "manual"), Node("each", "for_each"), Node("a", "echo"), Node("gather", "collect"), Node("both", "echo")],
            [Edge("start", "each"), Edge("each", "a"), Edge("a", "gather"), Edge("a", "both"), Edge("gather", "both")]);

        var found = LoopBody.Find(spec, NodeId.From("each"));

        Assert.True(found.IsError);
        Assert.Contains("'both'", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_body_node_fed_from_outside_the_loop_is_refused()
    {
        var spec = Spec(
            [Node("start", "manual"), Node("each", "for_each"), Node("a", "echo"), Node("gather", "collect")],
            [Edge("start", "each"), Edge("start", "a"), Edge("each", "a"), Edge("a", "gather")]);

        var found = LoopBody.Find(spec, NodeId.From("each"));

        Assert.True(found.IsError);
        Assert.Contains("'a'", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_for_each_inside_a_loop_is_refused()
    {
        var spec = Spec(
            [Node("start", "manual"), Node("outer", "for_each"), Node("inner", "for_each"), Node("g1", "collect"), Node("g2", "collect")],
            [Edge("start", "outer"), Edge("outer", "inner"), Edge("inner", "g1"), Edge("g1", "g2")]);

        var found = LoopBody.Find(spec, NodeId.From("outer"));

        Assert.True(found.IsError);
        Assert.Contains("inside", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enclosing_names_the_loop_of_a_body_node_and_the_collect_but_not_the_nodes_around_them()
    {
        Assert.Equal("each", LoopBody.Enclosing(_linear, NodeId.From("a"))?.ForEach.Id.Value);
        Assert.Equal("each", LoopBody.Enclosing(_linear, NodeId.From("gather"))?.ForEach.Id.Value);
        Assert.Null(LoopBody.Enclosing(_linear, NodeId.From("each")));
        Assert.Null(LoopBody.Enclosing(_linear, NodeId.From("after")));
        Assert.Null(LoopBody.Enclosing(_linear, NodeId.From("start")));
    }
}
