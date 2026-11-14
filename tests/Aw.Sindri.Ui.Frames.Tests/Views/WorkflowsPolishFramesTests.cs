using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2807 items 6 (breadcrumb header), 7 (a condition's missing false branch) and 8 (copy): served from the API's goldens.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsPolishFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> OnList(string? changedAt = null)
    {
        var list = Golden("workflows", "list-two");
        if (changedAt is not null)
        {
            list["workflows"]![0]!["changedAt"] = changedAt;
        }

        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        return (window, shell);
    }

    [Fact]
    public Task The_runs_header_is_a_breadcrumb_with_the_workflows_name_as_the_title_and_no_Back_row() => OnUi(async () =>
    {
        var (window, shell) = await OnList();
        var listTitle = Named<TextBlock>(window, "WorkflowsTitle");
        Assert.Equal("Workflows", listTitle.Text);
        var size = listTitle.FontSize;
        Assert.False(Visible(window, "WorkflowsBack"));

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");
        Settle(window);

        var title = Named<TextBlock>(window, "WorkflowsTitle");
        Assert.Equal("Morning check", title.Text);
        Assert.Equal(size, title.FontSize);   // the page title's size, not the old small body-strong name
        Assert.False(Visible(window, "RunsTitle"), "the workflow's name is still shown a second time, small");
        var crumb = Named<Button>(window, "WorkflowsBack");
        Assert.True(crumb.IsEffectivelyVisible);
        Assert.Equal("Workflows", crumb.Content);
        Assert.True(crumb.Focusable && crumb.IsTabStop, "the crumb is not reachable by keyboard");
        var (crumbAt, titleAt) = (crumb.TranslatePoint(default, window)!.Value, title.TranslatePoint(default, window)!.Value);   // why: both are in the window
        Assert.True(Math.Abs(crumbAt.Y - titleAt.Y) < title.Bounds.Height, string.Create(CultureInfo.InvariantCulture, $"the crumb (y={crumbAt.Y}) sits on a row of its own above the title (y={titleAt.Y})"));
    });

    [Fact]
    public Task A_condition_with_no_false_edge_draws_false_to_done() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        var spec = workflow["spec"]!;
        spec["nodes"]!.AsArray().Insert(1, JsonNode.Parse("""{"id":"check","type":"condition","title":"Flagged?","config":{"if":"true"}}"""));
        spec["edges"] = JsonNode.Parse("""[{"from":"start","to":"check"},{"from":"check","to":"agent","output":"true"}]""");
        workflow["positions"]!.AsArray().Add(JsonNode.Parse("""{"node":"check","x":140,"y":0}"""));
        var (window, shell) = await OnList();
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");

        var end = Assert.Single(shell.Workflows.Runs.Graph!.Ends);
        Assert.Equal(("check", "false → done"), (end.Node, end.Text));
        Settle(window);
        Frame(window, "graph-false-end-1280", "workflows-");
    });

    [Fact]
    public Task A_missing_branchs_done_words_clear_every_step_card_when_the_next_step_is_one_column_over() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        var spec = workflow["spec"]!;
        spec["nodes"]!.AsArray().Insert(1, JsonNode.Parse("""{"id":"check","type":"condition","title":"Flagged?","config":{"if":"true"}}"""));
        spec["edges"] = JsonNode.Parse("""[{"from":"start","to":"check"},{"from":"check","to":"agent","output":"true"}]""");
        workflow["positions"] = JsonNode.Parse("""[{"node":"start","x":0,"y":0},{"node":"check","x":280,"y":0},{"node":"agent","x":560,"y":0}]""");   // the layout's 80 px column gap
        var (window, shell) = await OnList();
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");

        var graph = shell.Workflows.Runs.Graph!;
        var end = Assert.Single(graph.Ends);
        foreach (var node in graph.Nodes)
        {
            var card = new GraphBox(node.X, node.Y, WorkflowGraphViewModel.CardWidth, WorkflowGraphViewModel.CardHeight);
            Assert.False(end.Label.Overlaps(card), string.Create(CultureInfo.InvariantCulture, $"'{end.Text}' at ({end.Label.X}, {end.Label.Y}, {end.Label.Width}x{end.Label.Height}) is drawn under the '{node.Title}' card at ({card.X}, {card.Y})"));
        }

        Settle(window);
        Frame(window, "graph-false-end-next-column-1280", "workflows-");
    });

    [Fact]
    public Task A_top_row_conditions_done_words_stay_inside_the_world_when_a_card_sits_under_it() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        var spec = workflow["spec"]!;
        spec["nodes"]!.AsArray().Insert(1, JsonNode.Parse("""{"id":"check","type":"condition","title":"Flagged?","config":{"if":"true"}}"""));
        spec["edges"] = JsonNode.Parse("""[{"from":"start","to":"check"},{"from":"check","to":"agent","output":"true"}]""");
        workflow["positions"] = JsonNode.Parse("""[{"node":"check","x":0,"y":0},{"node":"start","x":0,"y":96},{"node":"agent","x":280,"y":0}]""");   // a card straight under the top-row condition blocks "below"
        var (window, shell) = await OnList();
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");

        var end = Assert.Single(shell.Workflows.Runs.Graph!.Ends);
        Assert.True(end.Label.Y >= 0, string.Create(CultureInfo.InvariantCulture, $"'{end.Text}' is drawn at y={end.Label.Y}, above the world's top, so the canvas clips it"));
        foreach (var node in shell.Workflows.Runs.Graph.Nodes)
        {
            Assert.False(end.Label.Overlaps(new GraphBox(node.X, node.Y, WorkflowGraphViewModel.CardWidth, WorkflowGraphViewModel.CardHeight)), $"'{end.Text}' is drawn under the '{node.Title}' card");
        }
    });

    [Fact]
    public Task A_change_seconds_ago_reads_just_now() => OnUi(async () =>
    {
        var (window, shell) = await OnList(Clock.GetUtcNow().ToString("o", CultureInfo.InvariantCulture));

        Assert.Equal("Changed by owner, just now", shell.Workflows.Rows[0].ChangedText);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && string.Equals(t.Text, "Changed by owner, just now", StringComparison.Ordinal));
    });

    [Fact]
    public Task The_runs_detail_counts_steps_not_nodes() => OnUi(async () =>
    {
        var (window, shell) = await OnList();
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        var detail = Named<TextBlock>(window, "RunsDetail").Text;
        Assert.Contains("2 steps", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("node", detail, StringComparison.Ordinal);
    });

    [Fact]
    public Task The_empty_page_shows_one_New_workflow_button_and_a_list_shows_the_header_one() => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");

        Assert.True(Visible(window, "WorkflowsNew"));
        Assert.False(Visible(window, "WorkflowsNewHeader"), "the empty page shows New workflow twice");

        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-two");
        shell.GoTo(PageId.Home);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        Assert.True(Visible(window, "WorkflowsNewHeader"));
        Assert.False(Visible(window, "WorkflowsNew"));
    });
}
