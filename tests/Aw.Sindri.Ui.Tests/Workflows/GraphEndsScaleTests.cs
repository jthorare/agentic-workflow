using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3231: the ends' words grow with the text size setting, so their boxes, stubs and gaps are laid out at it like the cards.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphEndsScaleTests
{
    private const double Large = 1.3;

    private static Task<WorkflowGraphViewModel> ConditionGraph() => WorkflowGraphViewModelTests.GraphOf(
        new WorkflowsFixture(),
        """[{"id":"a","type":"code","title":"A","config":{}},{"id":"check","type":"condition","title":"Check","config":{}},{"id":"b","type":"code","title":"B","config":{}}]""",
        """[{"from":"a","to":"check"},{"from":"check","to":"b","output":"true"}]""",
        """[{"node":"a","x":0,"y":0},{"node":"check","x":280,"y":0},{"node":"b","x":560,"y":0}]""");

    private static GraphEnd EndOf(WorkflowGraphViewModel graph, string node) => graph.Ends.Single(e => string.Equals(e.Node, node, StringComparison.Ordinal));

    [Fact]
    public async Task At_130_percent_a_missing_branchs_word_box_and_stub_are_1_3_times_as_large()
    {
        var graph = await ConditionGraph();
        _ = graph.Relayout(1440, 728, 1);
        var normal = EndOf(graph, "check");
        _ = graph.Relayout(1440, 728, Large);
        var large = EndOf(graph, "check");

        Assert.Equal(normal.Label.Width * Large, large.Label.Width, 3);
        Assert.Equal(normal.Label.Height * Large, large.Label.Height, 3);
        Assert.Equal(Stub(normal) * Large, Stub(large), 3);
    }

    [Fact]
    public async Task At_130_percent_the_done_word_box_and_its_gap_to_the_card_are_1_3_times_as_large()
    {
        var graph = await ConditionGraph();
        _ = graph.Relayout(1440, 728, 1);
        var (normal, normalGap) = Done(graph);
        _ = graph.Relayout(1440, 728, Large);
        var (large, largeGap) = Done(graph);

        Assert.Equal(normal.Width * Large, large.Width, 3);
        Assert.Equal(normal.Height * Large, large.Height, 3);
        Assert.Equal(normalGap * Large, largeGap, 3);
    }

    private static double Stub(GraphEnd end) => Math.Abs(end.To.X - end.At.X) + Math.Abs(end.To.Y - end.At.Y);

    private static (GraphBox Label, double Gap) Done(WorkflowGraphViewModel graph)
    {
        var end = EndOf(graph, "b");
        var card = graph.Nodes.Single(n => string.Equals(n.Id, "b", StringComparison.Ordinal));
        return (end.Label, end.Label.X - (card.X + card.Width));
    }
}
