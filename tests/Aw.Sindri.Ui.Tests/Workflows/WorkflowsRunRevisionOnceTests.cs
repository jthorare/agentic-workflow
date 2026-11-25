using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// "Run revision N once" (#3691): while a saved revision is newer than the live one ("Saved as revision N. Not live yet."), the page offers one real run of it. It posts the revision
/// to the runs route, opens the run like Run now, shows a refusal where Run now's shows, and leaves the live revision alone. The found golden is live at 1 with 2 saved.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsRunRevisionOnceTests
{
    private const string RevisionRun = "morning-check@2:1";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnRuns(int? active = 1)
    {
        var shown = Golden("workflows-slug", "found");
        shown["activeRevision"] = active;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, shown.ToJsonString());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task A_saved_revision_that_is_not_live_offers_Run_revision_N_once()
    {
        var page = await OnRuns();

        Assert.Equal((true, "Run revision 2 once"), (page.ShowRunRevisionOnce, page.RunRevisionOnceText));
    }

    [Fact]
    public async Task No_button_when_the_newest_revision_is_the_live_one()
    {
        var page = await OnRuns(active: 2);

        Assert.False(page.ShowRunRevisionOnce);
    }

    [Fact]
    public async Task No_button_when_the_workflow_is_off()
    {
        var page = await OnRuns(active: null);

        Assert.False(page.ShowRunRevisionOnce);
    }

    [Fact]
    public async Task Running_the_revision_once_posts_it_and_opens_the_run()
    {
        _f.Server.StartWorkflowRunAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"run\":\"" + RevisionRun + "\",\"status\":\"started\",\"reason\":null}");
        _f.Server.WorkflowRunAnswers[RevisionRun] = RouteAnswer.Api("workflow-runs-run", "running");
        var page = await OnRuns();

        await page.RunRevisionOnce(Token);

        Assert.Equal([(Slug, 2)], _f.Server.StartedRevisionRuns);
        Assert.Empty(_f.Server.StartedWorkflows);   // never a plain run of the live revision
        Assert.Equal((true, RevisionRun), (page.IsRun, page.Run.Id));
        Assert.Empty(_f.Server.ActivatedWorkflows);   // the live revision is not changed
    }

    [Fact]
    public async Task A_refusal_shows_where_Run_nows_refusal_shows()
    {
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.Problem("workflows-slug-runs", "revision-not-found");
        var page = await OnRuns();

        await page.RunRevisionOnce(Token);

        Assert.True(page.IsRuns);
        Assert.True(page.Runs.HasNotice);
        Assert.StartsWith("Couldn't start a run. Workflow 'morning-check' has no revision 4", page.Runs.NoticeText, StringComparison.Ordinal);
        Assert.False(page.Runs.IsStarting);
    }

    [Fact]
    public async Task Run_now_still_runs_the_live_revision()
    {
        var page = await OnRuns();

        await page.RunNow(Token);

        Assert.Equal([Slug], _f.Server.StartedWorkflows);
        Assert.Empty(_f.Server.StartedRevisionRuns);
    }
}
