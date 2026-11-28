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
public sealed class WorkflowsRunRevisionOnceTests : IDisposable
{
    private const string RevisionRun = "morning-check@2:1";

    private readonly InlineUiContext _ui = new();   // the start deadline's timer callback runs on the thread that advances the fake clock
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _ui.Dispose();

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

    [Fact]
    public async Task Retry_on_a_revision_run_that_got_no_row_runs_that_revision_again()
    {
        _f.Server.StartWorkflowRunAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"run\":\"" + RevisionRun + "\",\"status\":\"started\",\"reason\":null}");
        _f.Server.WorkflowRunAnswers[RevisionRun] = RouteAnswer.Api("workflow-runs-run", "running");
        var page = await OnRuns();
        await page.RunRevisionOnce(Token);
        _f.Clock.Advance(TimeSpan.FromSeconds(10));   // no run row came: the placeholder says the run did not start

        await page.RetryStart(RevisionRun, Token);

        Assert.Equal([(Slug, 2), (Slug, 2)], _f.Server.StartedRevisionRuns);   // revision 2 again, never the live revision 1
        Assert.Empty(_f.Server.StartedWorkflows);
    }

    [Fact]
    public async Task Retry_on_a_plain_Run_now_still_sends_no_revision()
    {
        var page = await OnRuns();
        await page.RunNow(Token);
        var placeholder = Assert.Single(page.Runs.Rows, static r => r.Status == "starting");
        _f.Clock.Advance(TimeSpan.FromSeconds(10));

        await page.RetryStart(placeholder.Id, Token);

        Assert.Equal([Slug, Slug], _f.Server.StartedWorkflows);
        Assert.Empty(_f.Server.StartedRevisionRuns);
    }
}
