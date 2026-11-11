using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The workflow graph's view model (#2258 PR 17 part 2): nodes at the served positions, edges with branch labels, a node without a position, selection order and run statuses.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowGraphViewModelTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The golden workflow with its graph replaced.</summary>
    internal static async Task<WorkflowGraphViewModel> GraphOf(WorkflowsFixture f, string nodes, string edges, string positions, IReadOnlyDictionary<string, string>? statuses = null, string? slug = null)
    {
        var json = Golden("workflows-slug", "found");
        json["spec"]!["nodes"] = JsonNode.Parse(nodes);
        json["spec"]!["edges"] = JsonNode.Parse(edges);
        json["positions"] = JsonNode.Parse(positions);
        f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, json.ToJsonString());
        var served = await f.Server.Workflow(Url, Slug, Token);
        return new WorkflowGraphViewModel(slug is null ? served.Value : served.Value with { Slug = slug }, statuses);
    }

    private const string Branching = """
        [{"id":"check","type":"branch","config":{}},{"id":"yes","type":"run_agent","title":"Say yes","config":{}},{"id":"no","type":"run_agent","config":{}}]
        """;

    private const string BranchEdges = """
        [{"from":"check","to":"yes","output":"true"},{"from":"check","to":"no","output":"false"},{"from":"yes","to":"ghost"}]
        """;

    private const string BranchPositions = """
        [{"node":"check","x":0,"y":100},{"node":"yes","x":300,"y":0},{"node":"no","x":300,"y":200}]
        """;

    [Fact]
    public async Task Nodes_sit_at_the_positions_the_server_returned_and_a_node_without_a_title_shows_its_id()
    {
        var served = await _f.Server.Workflow(Url, Slug, Token);
        var graph = new WorkflowGraphViewModel(served.Value);

        Assert.Equal(
            [("start", "start", "manual", 0d, 0d), ("agent", "Check the repos", "run_agent", 280d, 0d)],
            graph.Nodes.Select(n => (n.Id, n.Title, n.Type, n.X, n.Y)));
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(("start", "agent"), (edge.From, edge.To));
        Assert.Null(edge.Label);
        Assert.Equal((new GraphPoint(200, 32), new GraphPoint(280, 32)), (edge.Start, edge.End));
    }

    [Fact]
    public async Task Branch_edges_carry_their_output_as_a_label_and_an_edge_to_a_missing_node_is_left_out()
    {
        var graph = await GraphOf(_f, Branching, BranchEdges, BranchPositions);

        Assert.Equal([("check", "yes", "true"), ("check", "no", "false")], graph.Edges.Select(e => (e.From, e.To, e.Label)));
        Assert.Equal(new GraphPoint(200, 132), graph.Edges[0].Start);
        Assert.Equal(new GraphPoint(300, 32), graph.Edges[0].End);
        Assert.Equal(new GraphPoint(300, 232), graph.Edges[1].End);
    }

    [Fact]
    public async Task A_node_without_a_position_is_not_drawn_and_is_named()
    {
        var graph = await GraphOf(
            _f,
            """[{"id":"a","type":"manual","config":{}},{"id":"b","type":"x","config":{}},{"id":"c","type":"x","config":{}}]""",
            """[{"from":"a","to":"b"},{"from":"a","to":"c"}]""",
            """[{"node":"a","x":100,"y":40},{"node":"c","x":400,"y":40}]""");

        Assert.Equal(["a", "c"], graph.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        Assert.Equal(["b"], graph.Unplaced);
        Assert.Equal([("a", "c")], graph.Edges.Select(e => (e.From, e.To)));
        Assert.Equal("1 node has no position: b", graph.UnplacedText);
        Assert.False(graph.SelectNode("b"));
    }

    [Fact]
    public async Task A_graph_whose_nodes_all_lack_positions_is_not_empty_and_a_spec_with_no_nodes_is()
    {
        var unplaced = await GraphOf(_f, """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}}]""", "[]", "[]");
        var none = await GraphOf(_f, "[]", "[]", "[]");

        Assert.False(unplaced.IsEmpty);   // only the "2 nodes have no position" line shows, not "no steps"
        Assert.True(unplaced.HasUnplaced);
        Assert.True(none.IsEmpty);
        Assert.False(none.HasUnplaced);
    }

    [Fact]
    public async Task Selection_moves_left_to_right_then_top_to_bottom_and_raises_the_node_id()
    {
        var graph = await GraphOf(
            _f,
            """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}},{"id":"c","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":300,"y":0},{"node":"b","x":0,"y":100},{"node":"c","x":0,"y":0}]""");
        var raised = new List<string>();
        graph.NodeSelected += (_, e) => raised.Add(e.Value);

        Assert.True(graph.SelectNext());   // nothing selected yet: the first
        Assert.True(graph.SelectNext());
        Assert.True(graph.SelectNext());
        Assert.False(graph.SelectNext());   // the last: nothing moves
        Assert.True(graph.SelectPrevious());
        Assert.True(graph.ActivateSelected());

        Assert.Equal(["c", "b", "a", "b", "b"], raised);
        Assert.Equal("b", graph.SelectedId);
        Assert.Equal(["b"], graph.Nodes.Where(n => n.IsSelected).Select(n => n.Id), StringComparer.Ordinal);
        Assert.False(graph.SelectNode("nope"));
    }

    [Fact]
    public async Task A_run_gives_each_node_a_glyph_word_and_tone_and_a_node_the_run_never_reached_has_none()
    {
        var graph = await GraphOf(
            _f,
            """[{"id":"a","type":"x","config":{}},{"id":"b","type":"x","config":{}},{"id":"c","type":"x","config":{}}]""",
            "[]",
            """[{"node":"a","x":0,"y":0},{"node":"b","x":300,"y":0},{"node":"c","x":600,"y":0}]""",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "succeeded", ["b"] = "failed" });

        Assert.Equal(
            [("✓", "Succeeded", GraphTone.Success), ("✕", "Failed", GraphTone.Error), ("", "", GraphTone.None)],
            graph.Nodes.Select(n => (n.Glyph, n.StatusWord, n.Tone)));
        Assert.Equal("a, x, succeeded", graph.Nodes[0].Name);
        Assert.Equal("c, x", graph.Nodes[2].Name);
    }
}
