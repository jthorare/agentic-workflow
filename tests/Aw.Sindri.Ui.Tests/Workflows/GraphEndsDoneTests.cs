using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Every step with no out port gets the "done" end word, 8 px or more from its own card (#3684).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphEndsDoneTests
{
    [Fact]
    public async Task Every_step_with_no_out_port_gets_the_done_end_word()
    {
        // a feeds b, b feeds c and d: c and d have no out port; a and b do.
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"code","title":"A","config":{}},{"id":"b","type":"code","title":"B","config":{}},{"id":"c","type":"code","title":"C","config":{}},{"id":"d","type":"code","title":"D","config":{}}]""",
            """[{"from":"a","to":"b"},{"from":"b","to":"c"},{"from":"b","to":"d"}]""",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":280,"y":0},{"node":"c","x":560,"y":0},{"node":"d","x":560,"y":140}]""");

        var done = graph.Ends.Where(e => string.Equals(e.Text, "done", StringComparison.Ordinal)).Select(e => e.Node).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["c", "d"], done);
    }

    [Fact]
    public async Task Done_word_is_at_least_8_px_from_its_own_card()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(
            new WorkflowsFixture(),
            """[{"id":"a","type":"code","title":"A","config":{}},{"id":"b","type":"code","title":"B","config":{}}]""",
            """[{"from":"a","to":"b"}]""",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":280,"y":0}]""");

        var end = Assert.Single(graph.Ends);
        var card = graph.Nodes.Single(n => string.Equals(n.Id, "b", StringComparison.Ordinal));

        Assert.True(end.Label.X - (card.X + card.Width) >= 8, "the done word touches its card");
    }
}
