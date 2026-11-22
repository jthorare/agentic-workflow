using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3702: a Starting placeholder that gets no run row for 10 s says "The run didn't start." with Retry. A row that arrives late still replaces it. Retry does what Run now does.</summary>
[Lane(TestLane.Unit)]
public sealed class RunNowStartTimeoutTests : IDisposable
{
    private const string NewRun = "morning-check@1:4";

    private readonly InlineUiContext _ui = new();   // the timer's callback runs on the thread that advances the fake clock
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _ui.Dispose();

    private async Task<WorkflowsPageViewModel> StartedNoRow()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.RunNow(Token);
        return page;
    }

    private void ServeTheNewRunsRow()
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
    public async Task After_10_seconds_with_no_run_the_row_says_the_run_did_not_start_and_offers_Retry()
    {
        var page = await StartedNoRow();
        _f.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.False(page.Runs.Rows[0].DidNotStart);   // still waiting

        _f.Clock.Advance(TimeSpan.FromSeconds(1));

        var row = page.Runs.Rows[0];
        Assert.Equal((NewRun, true, "The run didn't start.", "Retry"), (row.Id, row.DidNotStart, row.DidNotStartText, row.RetryText));
    }

    [Fact]
    public async Task A_run_row_arriving_at_12_seconds_replaces_the_failed_placeholder()
    {
        var page = await StartedNoRow();
        _f.Clock.Advance(TimeSpan.FromSeconds(12));
        Assert.True(page.Runs.Rows[0].DidNotStart);
        ServeTheNewRunsRow();

        await page.Runs.Read(Url, Slug, page.Runs.Title, Token);

        var row = Assert.Single(page.Runs.Rows, r => r.Id == NewRun);
        Assert.Equal(("Running", false, 4), (row.ChipWord, row.DidNotStart, page.Runs.Rows.Count));
    }

    [Fact]
    public async Task Retry_starts_a_new_run_and_shows_Starting_again()
    {
        var page = await StartedNoRow();
        _f.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(page.Runs.Rows[0].DidNotStart);

        await page.RetryStart(NewRun, Token);

        Assert.Equal([Slug, Slug], _f.Server.StartedWorkflows);
        var row = Assert.Single(page.Runs.Rows, r => r.Id == NewRun);
        Assert.Equal(("Starting", false, 4), (row.ChipWord, row.DidNotStart, page.Runs.Rows.Count));
    }
}
