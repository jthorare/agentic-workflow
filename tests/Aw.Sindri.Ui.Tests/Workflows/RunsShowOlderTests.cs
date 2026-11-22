using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// Show older runs (#3305): Sindri serves a workflow's newest 50 runs and says when older ones exist; the runs list then offers the next page, appends it under
/// the rows shown (the shown rows stay the same objects, so the list keeps its scroll), says so while it loads and when it fails, and keeps the older rows on a re-read.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunsShowOlderTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A page of runs, newest first: each occurrence ended (or, for "running", started) that many minutes before the fixture's clock.
    private static RouteAnswer Runs(bool more, params (int Occurrence, string Status)[] runs)
    {
        var rows = new JsonArray();
        foreach (var (occurrence, status) in runs)
        {
            rows.Add(new JsonObject
            {
                ["id"] = string.Create(CultureInfo.InvariantCulture, $"{Slug}@1:{occurrence}"),
                ["revision"] = 1,
                ["status"] = status,
                ["trigger"] = "schedule",
                ["startedAt"] = Now.AddMinutes(-occurrence).ToString("O", CultureInfo.InvariantCulture),
                ["endedAt"] = null,
                ["error"] = null,
            });
        }

        return new RouteAnswer(HttpStatusCode.OK, new JsonObject { ["slug"] = Slug, ["runs"] = rows, ["more"] = more }.ToJsonString());
    }

    private static string[] Ids(RunsViewModel runs) => [.. runs.Rows.Select(static r => r.Id[(r.Id.IndexOf(':', StringComparison.Ordinal) + 1)..])];

    private async Task<RunsViewModel> Opened()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page.Runs;
    }

    private void ServeNewest(bool more, params (int Occurrence, string Status)[] runs) => _f.Server.WorkflowRunsAnswers[Slug] = Runs(more, runs);

    [Fact]
    public async Task Older_runs_on_the_server_offer_Show_older_runs()
    {
        ServeNewest(more: true, (5, "running"), (4, "succeeded"), (3, "failed"));

        var runs = await Opened();

        Assert.Equal((true, true, "Show older runs"), (runs.HasMore, runs.CanShowMore, runs.ShowMoreText));
    }

    [Fact]
    public async Task No_older_runs_offer_nothing()
    {
        var runs = await Opened();   // the list-three golden: more is false

        Assert.Equal((false, false), (runs.HasMore, runs.CanShowMore));
    }

    [Fact]
    public async Task Show_older_runs_reads_past_the_shown_runs_and_appends_them_under_the_same_rows()
    {
        ServeNewest(more: true, (5, "running"), (4, "succeeded"), (3, "failed"));
        _f.Server.WorkflowRunsPageAnswer = (_, offset) => offset == 3 ? Runs(more: false, (2, "succeeded"), (1, "succeeded")) : null;
        var runs = await Opened();
        var first = runs.Rows[0];

        await runs.ShowMore(Url, Token);

        Assert.Equal([(Slug, 3)], _f.Server.WorkflowRunsPages);
        Assert.Equal(["5", "4", "3", "2", "1"], Ids(runs), StringComparer.Ordinal);
        Assert.Same(first, runs.Rows[0]);   // appended, not rebuilt: the list keeps its scroll
        Assert.Equal((false, false), (runs.HasMore, runs.CanShowMore));
        Assert.Equal("1 running · 5 runs", runs.Head);
    }

    [Fact]
    public async Task Each_press_reads_the_page_after_every_run_shown_so_far()
    {
        ServeNewest(more: true, (6, "succeeded"), (5, "succeeded"));
        _f.Server.WorkflowRunsPageAnswer = (_, offset) => offset switch
        {
            2 => Runs(more: true, (4, "succeeded"), (3, "succeeded")),
            4 => Runs(more: false, (2, "succeeded")),
            _ => null,
        };
        var runs = await Opened();

        await runs.ShowMore(Url, Token);
        await runs.ShowMore(Url, Token);

        Assert.Equal([(Slug, 2), (Slug, 4)], _f.Server.WorkflowRunsPages);
        Assert.Equal(["6", "5", "4", "3", "2"], Ids(runs), StringComparer.Ordinal);
        Assert.False(runs.HasMore);
    }

    [Fact]
    public async Task A_run_already_shown_is_not_listed_twice_when_new_runs_pushed_the_page_down()
    {
        ServeNewest(more: true, (5, "succeeded"), (4, "succeeded"));
        _f.Server.WorkflowRunsPageAnswer = (_, _) => Runs(more: false, (4, "succeeded"), (3, "succeeded"));   // a new run since: the page starts one run later
        var runs = await Opened();

        await runs.ShowMore(Url, Token);

        Assert.Equal(["5", "4", "3"], Ids(runs), StringComparer.Ordinal);
    }

    [Fact]
    public async Task While_older_runs_load_the_button_says_so_and_a_second_press_does_nothing()
    {
        ServeNewest(more: true, (5, "succeeded"), (4, "succeeded"));
        _f.Server.WorkflowRunsPageAnswer = (_, _) => Runs(more: false, (3, "succeeded"));
        var runs = await Opened();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.WorkflowRunsPageGate = gate;

        var loading = runs.ShowMore(Url, Token);
        Assert.Equal((true, false, "Loading older runs…"), (runs.IsLoadingMore, runs.CanShowMore, runs.ShowMoreText));
        await runs.ShowMore(Url, Token);   // ignored: one read at a time
        gate.SetResult();
        await loading;

        Assert.Single(_f.Server.WorkflowRunsPages);
        Assert.Equal(["5", "4", "3"], Ids(runs), StringComparer.Ordinal);
        Assert.False(runs.IsLoadingMore);
    }

    [Fact]
    public async Task A_failed_read_says_so_keeps_the_rows_and_Try_again_reads_the_same_page()
    {
        ServeNewest(more: true, (5, "succeeded"), (4, "succeeded"));
        var calls = 0;
        _f.Server.WorkflowRunsPageAnswer = (_, _) => ++calls == 1 ? RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-UI-002", "Sindri is not answering") : Runs(more: false, (3, "succeeded"));
        var runs = await Opened();

        await runs.ShowMore(Url, Token);

        Assert.True(runs.IsMoreFailed);
        Assert.StartsWith("Couldn't load older runs just now. ", runs.MoreFailedText, StringComparison.Ordinal);
        Assert.Equal((true, true, "Try again"), (runs.HasMore, runs.CanShowMore, runs.ShowMoreText));
        Assert.Equal(["5", "4"], Ids(runs), StringComparer.Ordinal);

        await runs.ShowMore(Url, Token);

        Assert.Equal([(Slug, 2), (Slug, 2)], _f.Server.WorkflowRunsPages);
        Assert.Equal((false, string.Empty), (runs.IsMoreFailed, runs.MoreFailedText));
        Assert.Equal(["5", "4", "3"], Ids(runs), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_re_read_keeps_the_older_runs_and_every_run_the_new_page_pushed_out()
    {
        ServeNewest(more: true, (5, "succeeded"), (4, "succeeded"), (3, "succeeded"));
        _f.Server.WorkflowRunsPageAnswer = (_, _) => Runs(more: false, (2, "succeeded"), (1, "succeeded"));
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.Runs.ShowMore(Url, Token);
        ServeNewest(more: true, (6, "running"), (5, "succeeded"), (4, "succeeded"));   // a new run: 3 slid off the newest page

        await page.RetryRuns(Token);

        Assert.Equal(["6", "5", "4", "3", "2", "1"], Ids(page.Runs), StringComparer.Ordinal);
        Assert.False(page.Runs.HasMore);   // the last older page said none are left
        Assert.Equal("1 running · 6 runs", page.Runs.Head);
    }

    [Fact]
    public async Task The_page_button_reads_the_open_workflows_next_page()
    {
        ServeNewest(more: true, (5, "succeeded"));
        _f.Server.WorkflowRunsPageAnswer = (_, _) => Runs(more: false, (4, "succeeded"));
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);

        await page.ShowOlderRuns(Token);

        Assert.Equal(["5", "4"], Ids(page.Runs), StringComparer.Ordinal);
    }
}
