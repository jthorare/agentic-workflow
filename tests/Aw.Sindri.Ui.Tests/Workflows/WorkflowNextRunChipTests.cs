using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Schedule card's next-run chip follows the open workflow (#4188): a time already past draws nothing, and every graph read brings the time GET
/// /sindri/workflows/{slug} serves, so a refresh or an activate changes the chip without a reopen.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowNextRunChipTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DateTimeOffset At(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

    // The fixture's clock is 2026-01-01 00:05 UTC: a next run at or before it is not next any more.
    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(-3 * 24 * 60)]
    public async Task A_next_run_at_or_before_now_draws_no_chip(int minutes)
    {
        ServeWorkflow(activeRevision: 1, nextRunAt: null);
        var served = await _f.Server.Workflow(Url, Slug, Token);
        var graph = new WorkflowGraphViewModel(served.Value, statuses: null) { Clock = _f.Clock, NextRunAt = Now.AddMinutes(minutes) };

        Assert.Null(graph.ChipFor(Schedule(graph)));
    }

    [Fact]
    public async Task A_graph_read_with_a_newer_next_run_changes_the_chip_with_the_workflow_open()
    {
        var page = await _f.Shown(Token);
        ServeWorkflow(activeRevision: 1, nextRunAt: "2026-01-01T07:00:00Z");
        await page.OpenWorkflow(Slug, Token);
        var before = page.Runs.Graph!.ChipFor(Schedule(page.Runs.Graph));

        ServeWorkflow(activeRevision: 1, nextRunAt: "2026-01-05T07:00:00Z");
        await page.RetryRuns(Token);   // a graph read with the workflow open

        Assert.Equal(("next 7:00", "next Mon 7:00"), (before, page.Runs.Graph!.ChipFor(Schedule(page.Runs.Graph))));
        Assert.Equal(At("2026-01-05T07:00:00Z"), page.Runs.Graph.NextRunAt);
    }

    [Fact]
    public async Task Activate_from_the_open_workflow_shows_the_chip_without_a_reopen()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = null;   // the open workflow is off, so Activate is offered
        _f.ServeList(list);
        var page = await _f.Shown(Token);
        ServeWorkflow(activeRevision: null, nextRunAt: null);
        await page.OpenWorkflow(Slug, Token);
        var before = page.Runs.Graph!.ChipFor(Schedule(page.Runs.Graph));

        ServeWorkflow(activeRevision: 1, nextRunAt: "2026-01-05T07:00:00Z");   // what Sindri serves once the activate landed
        await page.Activate(Token);

        Assert.Equal(("off", "next Mon 7:00"), (before, page.Runs.Graph!.ChipFor(Schedule(page.Runs.Graph))));
    }

    // #4208: a good read that sends no next run is the truth (an active workflow whose schedule has no next time); the list row's time from the open is
    // kept only when the read failed.
    [Fact]
    public async Task A_good_read_with_no_next_run_draws_no_chip_though_the_list_row_had_one()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["nextRunAt"] = "2026-01-05T07:00:00Z";
        _f.ServeList(list);
        var page = await _f.Shown(Token);
        ServeWorkflow(activeRevision: 1, nextRunAt: null);

        await page.OpenWorkflow(Slug, Token);

        Assert.Null(page.Runs.Graph!.NextRunAt);
        Assert.Null(page.Runs.Graph.ChipFor(Schedule(page.Runs.Graph)));
    }

    private static GraphNodeViewModel Schedule(WorkflowGraphViewModel graph) => graph.Nodes.Single(n => string.Equals(n.Id, "when", StringComparison.Ordinal));

    // GET /sindri/workflows/{slug}: a Schedule card and one step, on or off, with the next run Sindri serves (or none).
    private void ServeWorkflow(int? activeRevision, string? nextRunAt)
    {
        var json = Golden("workflows-slug", "found");
        json["spec"]!["nodes"] = JsonNode.Parse("""[{"id":"when","type":"schedule","title":"Weekday run","config":{}},{"id":"go","type":"code","title":"Go","config":{}}]""");
        json["spec"]!["edges"] = JsonNode.Parse("""[{"from":"when","to":"go"}]""");
        json["positions"] = JsonNode.Parse("""[{"node":"when","x":0,"y":0},{"node":"go","x":280,"y":0}]""");
        json["activeRevision"] = activeRevision;
        json["nextRunAt"] = nextRunAt;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, json.ToJsonString());
    }
}
