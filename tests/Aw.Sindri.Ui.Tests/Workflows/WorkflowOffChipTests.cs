using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A workflow that is off (no active revision) says "Off" once in its header as a chip, and "off" on its Schedule card; no card changes state (#3684).</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowOffChipTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_header_chip_reads_the_same_word_and_tone_as_the_list_row()
    {
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal(page.Rows[0].ChipWord, page.DetailChipWord);
        Assert.Equal(page.Rows[0].ChipGlyph, page.DetailChipGlyph);
        Assert.Equal(page.Rows[0].ChipToneWord, page.DetailChipTone);
    }

    private async Task<WorkflowGraphViewModel> GraphWith(int? activeRevision)
    {
        var json = Golden("workflows-slug", "found");
        json["spec"]!["nodes"] = JsonNode.Parse("""[{"id":"when","type":"schedule","title":"Weekday run","config":{}},{"id":"go","type":"code","title":"Go","config":{}}]""");
        json["spec"]!["edges"] = JsonNode.Parse("""[{"from":"when","to":"go"}]""");
        json["positions"] = JsonNode.Parse("""[{"node":"when","x":0,"y":0},{"node":"go","x":280,"y":0}]""");
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, json.ToJsonString());
        var served = await _f.Server.Workflow(Url, Slug, Token);
        return new WorkflowGraphViewModel(served.Value with { ActiveRevision = activeRevision }, statuses: null);
    }

    [Fact]
    public async Task An_off_workflow_puts_an_off_chip_on_its_Schedule_card_only()
    {
        var graph = await GraphWith(activeRevision: null);

        Assert.Equal("off", graph.ChipFor(graph.Nodes.Single(n => string.Equals(n.Id, "when", StringComparison.Ordinal))));
        Assert.Null(graph.ChipFor(graph.Nodes.Single(n => string.Equals(n.Id, "go", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task An_active_workflow_with_no_next_run_time_draws_no_chip()
    {
        var graph = await GraphWith(activeRevision: 1);
        graph.NextRunAt = null;

        Assert.All(graph.Nodes, n => Assert.Null(graph.ChipFor(n)));
    }

    // The fixture's clock is Thursday 2026-01-01 00:05 UTC; the chip reads local time (#3758).
    [Theory]
    [InlineData("2026-01-05T07:00:00Z", "UTC", "next Mon 7:00")]                    // more than 24 hours away: the day and the time
    [InlineData("2026-01-05T15:00:00Z", "America/Los_Angeles", "next Mon 7:00")]    // in the clock's time zone, not UTC
    [InlineData("2026-01-01T07:00:00Z", "UTC", "next 7:00")]                        // within the next 24 hours: the time only
    [InlineData("2026-01-01T23:30:00Z", "UTC", "next 23:30")]
    public async Task An_active_workflows_Schedule_card_shows_its_next_run(string next, string zone, string chip)
    {
        var graph = await GraphWith(activeRevision: 1);
        _f.Clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById(zone));
        graph.Clock = _f.Clock;
        graph.NextRunAt = DateTimeOffset.Parse(next, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(chip, graph.ChipFor(graph.Nodes.Single(n => string.Equals(n.Id, "when", StringComparison.Ordinal))), StringComparer.Ordinal);
        Assert.Null(graph.ChipFor(graph.Nodes.Single(n => string.Equals(n.Id, "go", StringComparison.Ordinal))));   // only the Schedule card
    }

    [Fact]
    public async Task An_off_workflow_keeps_the_off_chip_even_with_a_next_run_time()
    {
        var graph = await GraphWith(activeRevision: null);
        graph.Clock = _f.Clock;
        graph.NextRunAt = Now.AddHours(2);

        Assert.Equal("off", graph.ChipFor(graph.Nodes.Single(n => string.Equals(n.Id, "when", StringComparison.Ordinal))), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Opening_a_workflow_hands_its_rows_next_run_time_and_the_pages_clock_to_the_graph()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["nextRunAt"] = "2026-01-05T07:00:00Z";
        _f.ServeList(list);
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal(DateTimeOffset.Parse("2026-01-05T07:00:00Z", System.Globalization.CultureInfo.InvariantCulture), page.Runs.Graph!.NextRunAt);
        Assert.Same(_f.Clock, page.Runs.Graph.Clock);
    }
}
