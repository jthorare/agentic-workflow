using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3707: the run page of a run that never lands does what its list row does (#3702): after 10 s it says "The run didn't start." with Retry, and a run that lands late replaces it.</summary>
[Lane(TestLane.Unit)]
public sealed class RunDetailStartTimeoutTests : IDisposable
{
    private const string NewRun = "morning-check@1:4";

    private readonly InlineUiContext _ui = new();   // the timer's callback runs on the thread that advances the fake clock
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _ui.Dispose();

    private async Task<WorkflowsPageViewModel> OnTheRunPage()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.RunNow(Token);   // opens the run, which the server does not show yet
        Assert.Equal((true, NewRun, "Starting"), (page.IsRun, page.Run.Id, page.Run.ChipWord));
        return page;
    }

    private void ServeTheNewRun()
    {
        var list = Golden("workflows-slug-runs", "list-three");
        var row = JsonNode.Parse(list["runs"]![0]!.ToJsonString())!;
        row["id"] = NewRun;
        row["status"] = "running";
        list["runs"]!.AsArray().Insert(0, row);
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
        _f.Server.WorkflowRunAnswers[NewRun] = RouteAnswer.Api("workflow-runs-run", "running");
    }

    [Fact]
    public async Task The_run_page_says_the_run_did_not_start_with_Retry_at_10_seconds_and_not_at_9()
    {
        var page = await OnTheRunPage();
        _f.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.False(page.Run.DidNotStart);

        _f.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.False(page.Run.CanCancel);   // a "starting" run is not cancellable (RunStatus.CanCancel is running or waiting only): Retry is the only action

        Assert.Equal((true, "The run didn't start.", "Retry"), (page.Run.DidNotStart, page.Run.DidNotStartText, page.Run.StartRetryText));
    }

    [Fact]
    public async Task A_run_that_lands_late_replaces_the_did_not_start_page()
    {
        var page = await OnTheRunPage();
        _f.Clock.Advance(TimeSpan.FromSeconds(12));
        Assert.True(page.Run.DidNotStart);
        ServeTheNewRun();

        await page.OpenRun(NewRun, Token);

        Assert.Equal(("Running", false), (page.Run.ChipWord, page.Run.DidNotStart));
    }

    [Fact]
    public async Task Retry_on_the_run_page_starts_a_new_run_and_the_page_says_Starting_again()
    {
        var page = await OnTheRunPage();
        _f.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(page.Run.DidNotStart);

        await page.RetryStart(page.Run.Id, Token);

        Assert.Equal([Slug, Slug], _f.Server.StartedWorkflows);
        Assert.Equal(("Starting", false), (page.Run.ChipWord, page.Run.DidNotStart));
    }
}
