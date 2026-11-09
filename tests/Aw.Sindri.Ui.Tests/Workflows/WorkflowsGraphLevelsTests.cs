using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The graph on the runs and run levels (#2258 PR 17 part 2a): the runs level draws the revision it read; a run's detail draws it with each node's status, but only when the
/// run walked that revision (positions belong to a revision); and a read again keeps the selected node.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsGraphLevelsTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_runs_level_draws_the_served_revisions_graph_without_statuses()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        var graph = Assert.IsType<WorkflowGraphViewModel>(page.Runs.Graph);
        Assert.Equal(["start", "agent"], graph.Nodes.Select(n => n.Id));
        Assert.All(graph.Nodes, n => Assert.Equal(string.Empty, n.StatusWord));
    }

    [Fact]
    public async Task A_run_of_the_shown_revision_draws_the_graph_with_each_nodes_status()
    {
        _f.ServeRun("running");

        var run = await _f.OpenedRun(Token);

        var graph = Assert.IsType<WorkflowGraphViewModel>(run.Graph);
        Assert.Equal(
            [("start", Labels.WfChip("succeeded").Word), ("agent", Labels.WfChip("running").Word)],
            graph.Nodes.Select(n => (n.Id, n.StatusWord)));
    }

    [Fact]
    public async Task A_run_of_an_older_revision_draws_no_graph()
    {
        var workflow = Golden("workflows-slug", "found");
        workflow["revision"] = 2;   // the page reads revision 2; the served run walked revision 1
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, workflow.ToJsonString());

        var run = await _f.OpenedRun(Token);

        Assert.Null(run.Graph);
        Assert.NotEmpty(run.Groups);   // the node runs still show, as a list
    }

    [Fact]
    public async Task A_read_again_keeps_the_selected_node()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        var before = page.Runs.Graph!;
        Assert.True(before.SelectNode("agent"));

        await page.Runs.Read(Url, Slug, page.Runs.Title, Token);

        Assert.NotSame(before, page.Runs.Graph);
        Assert.Equal("agent", page.Runs.Graph!.SelectedId);
    }
}
