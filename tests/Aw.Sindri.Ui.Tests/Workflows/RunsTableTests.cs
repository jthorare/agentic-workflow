using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3683 runs table (frame wf-runs): a run reads as time (relative in the cell, absolute in a two-line tip), trigger in words, revision, duration and a one-line result, never as its id. Status and date filters narrow the rows
/// ("23 of 500 runs"), Clear filters undoes them, and ticking exactly two runs offers Compare 2. The clock is 2026-01-01 00:05 UTC.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunsTableTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string At(TimeSpan before) => (Now - before).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static JsonObject Run(int n, string status, TimeSpan ago, string trigger = "schedule", int revision = 2, double? seconds = 10, string? result = null) => new()
    {
        ["id"] = $"morning-check@{revision}:{n}",
        ["revision"] = revision,
        ["status"] = status,
        ["trigger"] = trigger,
        ["startedAt"] = At(ago),
        ["endedAt"] = status is "running" ? null : At(ago - TimeSpan.FromSeconds(seconds ?? 0)),
        ["error"] = status == "failed" ? "boom" : null,
        ["seconds"] = status is "running" ? null : seconds,
        ["result"] = result,
    };

    private async Task<Aw.Sindri.Ui.ViewModels.Workflows.WorkflowsPageViewModel> Open(params JsonObject[] runs)
    {
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject { ["slug"] = Slug, ["runs"] = new JsonArray([.. runs.Select(r => (JsonNode?)r)]) }.ToJsonString());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task A_run_reads_as_time_trigger_revision_duration_and_one_line_result_with_a_two_line_time_tip()
    {
        var page = await Open(Run(7, "failed", TimeSpan.FromHours(7) + TimeSpan.FromMinutes(42), trigger: "cli", seconds: 9, result: "tell failed: the notification was not delivered"));

        var row = page.Runs.Rows.Single();
        Assert.Equal(("7h ago", "From the CLI", "r2", "9 s", "tell failed: the notification was not delivered"), (row.TimeText, row.TriggerWords, row.RevisionShort, row.DurationText, row.ResultText));
        Assert.Equal("Wed 31 Dec 2025 · 16:23:00\n7h 42m ago", row.TimeTip);   // the absolute date and time, then the exact age: two lines
        Assert.DoesNotContain(row.TimeText, row.Id, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("manual", "By hand")]
    [InlineData("schedule", "On schedule")]
    [InlineData("cli", "From the CLI")]
    [InlineData("agent", "By an agent")]
    public async Task The_trigger_reads_in_words(string kind, string words)
    {
        var page = await Open(Run(1, "succeeded", TimeSpan.FromMinutes(3), trigger: kind));

        Assert.Equal(words, page.Runs.Rows.Single().TriggerWords);
    }

    [Fact]
    public async Task A_run_still_running_has_no_duration_yet_and_a_failed_one_with_no_result_falls_back_to_its_error()
    {
        var page = await Open(Run(2, "running", TimeSpan.FromMinutes(1)), Run(1, "failed", TimeSpan.FromMinutes(9)));

        Assert.Equal((string.Empty, "boom"), (page.Runs.Rows[0].DurationText, page.Runs.Rows[1].ResultText));
    }

    [Fact]
    public async Task The_status_filter_narrows_the_rows_and_the_count_says_how_many_of_all()
    {
        var page = await Open(Run(3, "failed", TimeSpan.FromMinutes(3)), Run(2, "succeeded", TimeSpan.FromMinutes(20)), Run(1, "failed", TimeSpan.FromHours(2)));

        page.Runs.SetStatusFilter("failed");

        Assert.Equal(["morning-check@2:3", "morning-check@2:1"], page.Runs.Rows.Select(r => r.Id), StringComparer.Ordinal);
        Assert.Equal(("2 of 3 runs", true), (page.Runs.CountText, page.Runs.HasFilter));
    }

    [Fact]
    public async Task The_date_filter_keeps_the_runs_in_its_range_and_Clear_filters_brings_them_all_back()
    {
        var page = await Open(Run(3, "failed", TimeSpan.FromHours(3)), Run(2, "failed", TimeSpan.FromDays(2)), Run(1, "failed", TimeSpan.FromDays(9)));

        page.Runs.SetDateFilter("7d");
        Assert.Equal(2, page.Runs.Rows.Count);

        page.Runs.SetDateFilter("24h");
        Assert.Equal(1, page.Runs.Rows.Count);

        page.Runs.ClearFilters();
        Assert.Equal((3, false, "3 runs"), (page.Runs.Rows.Count, page.Runs.HasFilter, page.Runs.CountText));
    }

    [Fact]
    public async Task A_filter_with_no_match_says_so_and_offers_Clear_filters()
    {
        var page = await Open(Run(1, "succeeded", TimeSpan.FromMinutes(3)));

        page.Runs.SetStatusFilter("failed");

        Assert.Equal((0, true, "No runs match these filters."), (page.Runs.Rows.Count, page.Runs.HasNoMatch, page.Runs.NoMatchText));
    }

    [Fact]
    public async Task Exactly_two_ticked_runs_offer_Compare_2_and_one_or_three_do_not()
    {
        var page = await Open(Run(3, "failed", TimeSpan.FromMinutes(3)), Run(2, "succeeded", TimeSpan.FromMinutes(20)), Run(1, "failed", TimeSpan.FromHours(2)));
        var rows = page.Runs.Rows;

        rows[0].IsSelected = true;
        Assert.Equal((false, 1), (page.Runs.CanCompare, page.Runs.SelectedCount));
        rows[1].IsSelected = true;
        Assert.Equal((true, "Compare 2"), (page.Runs.CanCompare, page.Runs.CompareText));
        rows[2].IsSelected = true;
        Assert.Equal((false, 3), (page.Runs.CanCompare, page.Runs.SelectedCount));
    }
}
