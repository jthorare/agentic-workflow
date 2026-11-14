using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Where a condition's missing branches draw "→ done" (#3655): a branch with no edge gets one, a branch the spec has an edge for never does, and a crowded card falls back to the lowered place.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphEndsTests
{
    private static readonly IReadOnlySet<(string, string)> _none = new HashSet<(string, string)>();

    private static GraphNodeViewModel Condition(double x = 0, double y = 0) => new("c", "Check", "condition", x, y, status: null);

    private static GraphNodeViewModel Step(string id, double x, double y) => new(id, id, "run_agent", x, y, status: null);

    private static GraphEdgeViewModel Edge(GraphNodeViewModel from, string label) =>
        new(from.Id, "next", label, new GraphPoint(from.X + WorkflowGraphViewModel.CardWidth, from.Y), new GraphPoint(from.X + 400, from.Y));

    private static string[] Texts(List<GraphEnd> ends) => [.. ends.Select(static e => e.Text)];

    [Fact]
    public void A_missing_true_edge_gets_a_true_to_done_end()
    {
        var c = Condition();

        Assert.Equal(["true \u2192 done"], Texts(GraphEnds.Build([c], [Edge(c, "false")], _none)));
    }

    [Fact]
    public void A_missing_false_edge_gets_a_false_to_done_end()
    {
        var c = Condition();

        Assert.Equal(["false \u2192 done"], Texts(GraphEnds.Build([c], [Edge(c, "true")], _none)));
    }

    [Fact]
    public void Both_edges_missing_get_both_ends()
    {
        Assert.Equal(["true \u2192 done", "false \u2192 done"], Texts(GraphEnds.Build([Condition()], [], _none)));
    }

    [Fact]
    public void Both_edges_present_get_no_end()
    {
        var c = Condition();

        Assert.Empty(GraphEnds.Build([c], [Edge(c, "true"), Edge(c, "false")], _none));
    }

    [Fact]
    public void A_graph_with_no_condition_gets_no_end()
    {
        Assert.Empty(GraphEnds.Build([Step("a", 0, 0)], [], _none));
    }

    [Fact]
    public void An_empty_graph_gets_no_end()
    {
        Assert.Empty(GraphEnds.Build([], [], _none));
    }

    [Fact]
    public void A_branch_the_spec_has_an_edge_for_gets_no_end_even_when_its_target_is_not_drawn()
    {
        var c = Condition();
        HashSet<(string, string)> spec = [("c", "true"), ("c", "false")];

        var ends = GraphEnds.Build([c], [Edge(c, "true")], spec);

        Assert.Empty(ends);   // the false edge is in the spec; its target has no position, so it is not drawn, and no "false -> done" is made up
    }

    [Fact]
    public void A_crowded_card_lowers_the_words_past_the_card_in_the_way()
    {
        var c = Condition(0, 0);
        var below = Step("below", 0, WorkflowGraphViewModel.CardHeight);
        var right = Step("right", WorkflowGraphViewModel.CardWidth, 0);

        var end = Assert.Single(GraphEnds.Build([c, below, right], [Edge(c, "true")], _none));

        Assert.Equal("false \u2192 done", end.Text);
        Assert.True(end.Label.Y >= below.Y + WorkflowGraphViewModel.CardHeight, "the words were not lowered past the card below");
    }
}
