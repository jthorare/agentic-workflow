using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4504: a run that ends while its page is open finishes on that page only (the events re-read the open level), so Back to the workflow must read the runs again, or the table's row
/// stays "running, Started 0s ago" with no duration until another event arrives.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunsBackRereadsRunsTests
{
    private const string NewRun = "morning-check@1:4";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The runs list as Sindri serves it once the new run ended: succeeded, 0.88 s.
    private void ServeTheRunEnded()
    {
        var list = Golden("workflows-slug-runs", "list-three");
        var row = JsonNode.Parse(list["runs"]![0]!.ToJsonString())!;
        (row["id"], row["status"], row["seconds"], row["endedAt"]) = (NewRun, "succeeded", 0.88, "2026-01-01T00:04:00.0000000Z");
        list["runs"]!.AsArray().Insert(0, row);
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public async Task Back_from_the_run_page_shows_the_runs_final_state_and_duration_in_the_table()
    {
        _f.Server.WorkflowRunAnswers[NewRun] = RouteAnswer.Api("workflow-runs-run", "running");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.RunNow(Token);   // the run page opens on the new run
        Assert.True(page.IsRun);
        ServeTheRunEnded();   // it ended while its page was open

        page.Back();

        var row = Assert.Single(page.Runs.Rows, r => r.Id == NewRun);
        Assert.Equal(("Succeeded", "<1 s"), (row.ChipWord, row.DurationText));
    }
}
