using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3685 point 3: Run now puts the new run in the runs list and the run view as Starting at once, from the start call's reply, while the server's views row is not there yet. The real row then replaces the placeholder with no duplicate, and a failed start leaves no placeholder.</summary>
[Lane(TestLane.Unit)]
public sealed class RunNowStartingTests
{
    private const string NewRun = "morning-check@1:4";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The views row is held back: the runs list still has only the three old runs and the run itself is not found.
    private async Task<WorkflowsPageViewModel> OnRuns()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    private void ServeTheNewRunsRow()
    {
        var list = Golden("workflows-slug-runs", "list-three");
        var row = System.Text.Json.Nodes.JsonNode.Parse(list["runs"]![0]!.ToJsonString())!;
        row["id"] = NewRun;
        row["status"] = "running";
        list["runs"]!.AsArray().Insert(0, row);
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        _f.Server.WorkflowRunAnswers[NewRun] = RouteAnswer.Api("workflow-runs-run", "running");
    }

    [Fact]
    public async Task The_new_run_shows_as_Starting_in_the_list_and_the_run_view_before_the_server_has_its_row()
    {
        var page = await OnRuns();

        await page.RunNow(Token);

        var row = page.Runs.Rows.First();
        Assert.Equal((NewRun, "Starting", 4), (row.Id, row.ChipWord, page.Runs.Rows.Count));
        Assert.Equal((true, NewRun, "Starting", false), (page.IsRun, page.Run.Id, page.Run.ChipWord, page.Run.IsFailed));
    }

    [Fact]
    public async Task A_read_that_still_lacks_the_row_keeps_one_Starting_placeholder()
    {
        var page = await OnRuns();
        await page.RunNow(Token);

        await page.Runs.Read(Url, Slug, page.Runs.Title, Token);
        await page.OpenRun(NewRun, Token);

        Assert.Equal(1, page.Runs.Rows.Count(r => r.Id == NewRun));
        Assert.Equal(("Starting", false), (page.Run.ChipWord, page.Run.IsFailed));
    }

    [Fact]
    public async Task The_real_row_replaces_the_placeholder_with_no_duplicate()
    {
        var page = await OnRuns();
        await page.RunNow(Token);
        ServeTheNewRunsRow();

        await page.Runs.Read(Url, Slug, page.Runs.Title, Token);
        await page.OpenRun(NewRun, Token);

        var rows = page.Runs.Rows.Where(r => r.Id == NewRun).ToList();
        Assert.Equal(("Running", 4, "Running"), (Assert.Single(rows).ChipWord, page.Runs.Rows.Count, page.Run.ChipWord));
    }

    [Fact]
    public async Task A_failed_start_leaves_no_placeholder_and_says_why_where_Run_now_errors_show()
    {
        var page = await OnRuns();
        _f.Server.StartWorkflowRunAnswer = RouteAnswer.Problem("workflows-slug-runs", "not-active");

        await page.RunNow(Token);

        Assert.Equal((3, true), (page.Runs.Rows.Count, page.IsRuns));
        Assert.DoesNotContain(page.Runs.Rows, r => r.Id == NewRun);
        Assert.StartsWith("Couldn't start a run.", page.Runs.NoticeText, StringComparison.Ordinal);
    }
}
