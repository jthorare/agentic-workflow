using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowGraphViewModelTests;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>W12, screen reader (spec 13 row 7): each step is named "title, kind, n of m", in reading order, with the status word after it when a run is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowStepNamesTests
{
    private const string Nodes = """[{"id":"start","type":"manual","title":"Start","config":{}},{"id":"check","type":"prompt","title":"Check","config":{}},{"id":"gate","type":"condition","title":"Flagged?","config":{}}]""";

    private readonly WorkflowsFixture _f = new();

    [Fact]
    public async Task Screen_reader__each_step_is_named_title_kind_n_of_m()
    {
        var graph = await GraphOf(_f, Nodes, "[]", """[{"node":"start","x":0,"y":0},{"node":"check","x":300,"y":0},{"node":"gate","x":600,"y":0}]""");

        Assert.Equal(["Start, When called step, 1 of 3", "Check, Prompt step, 2 of 3", "Flagged?, If step, 3 of 3"], graph.Nodes.Select(n => n.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_place_follows_the_reading_order_not_the_order_of_the_spec()
    {
        var graph = await GraphOf(_f, Nodes, "[]", """[{"node":"start","x":600,"y":0},{"node":"check","x":300,"y":0},{"node":"gate","x":0,"y":0}]""");

        Assert.Equal(["Start, When called step, 3 of 3", "Check, Prompt step, 2 of 3", "Flagged?, If step, 1 of 3"], graph.Nodes.Select(n => n.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_shown_run_adds_the_status_word_after_the_place()
    {
        var graph = await GraphOf(_f, Nodes, "[]", """[{"node":"start","x":0,"y":0},{"node":"check","x":300,"y":0},{"node":"gate","x":600,"y":0}]""", new Dictionary<string, string>(StringComparer.Ordinal) { ["check"] = "failed" });

        Assert.Equal("Check, Prompt step, 2 of 3, failed", graph.Nodes[1].Name);
    }

    [Fact]
    public async Task A_node_with_no_title_is_named_by_its_id()
    {
        var graph = await GraphOf(_f, """[{"id":"a","type":"run_agent","config":{}}]""", "[]", """[{"node":"a","x":0,"y":0}]""");

        Assert.Equal("a, Run agent step, 1 of 1", graph.Nodes[0].Name);
    }
}
