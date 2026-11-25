using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3683 (review of #4395): the server serves a workflow's newest 50 runs and says `more` when older ones exist. The filters and the count only see those 50, so the table says so:
/// "50+ runs", "0 of the newest 50 runs" and "No runs match these filters in the newest 50." (server-side filters are #3305).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunsTableMoreTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Run(int n) => new()
    {
        ["id"] = string.Create(CultureInfo.InvariantCulture, $"morning-check@2:{n}"),
        ["revision"] = 2,
        ["status"] = "succeeded",
        ["trigger"] = "schedule",
        ["startedAt"] = (Now - TimeSpan.FromMinutes(n)).ToString("O", CultureInfo.InvariantCulture),
        ["endedAt"] = (Now - TimeSpan.FromMinutes(n) + TimeSpan.FromSeconds(10)).ToString("O", CultureInfo.InvariantCulture),
        ["error"] = null,
        ["seconds"] = 10,
        ["result"] = null,
    };

    private async Task<WorkflowsPageViewModel> Open(int runs, bool more)
    {
        var served = new JsonObject { ["slug"] = Slug, ["runs"] = new JsonArray([.. Enumerable.Range(1, runs).Select(n => (JsonNode?)Run(n))]), ["more"] = more };
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, served.ToJsonString());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task When_older_runs_exist_the_count_says_50_plus()
    {
        var page = await Open(50, more: true);

        Assert.Equal("50+ runs", page.Runs.CountText);
    }

    [Fact]
    public async Task When_older_runs_exist_a_filter_that_finds_nothing_says_it_looked_at_the_newest_50_only()
    {
        var page = await Open(50, more: true);

        page.Runs.SetStatusFilter("failed");

        Assert.Equal((true, "0 of the newest 50 runs", "No runs match these filters in the newest 50."), (page.Runs.HasNoMatch, page.Runs.CountText, page.Runs.NoMatchText));
    }

    [Fact]
    public async Task When_every_run_was_served_the_words_stay_as_they_were()
    {
        var page = await Open(3, more: false);
        page.Runs.SetStatusFilter("failed");

        Assert.Equal(("0 of 3 runs", "No runs match these filters."), (page.Runs.CountText, page.Runs.NoMatchText));
    }
}
