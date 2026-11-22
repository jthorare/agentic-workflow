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
    public async Task An_active_workflow_draws_no_chip_because_no_next_run_time_is_served()
    {
        var graph = await GraphWith(activeRevision: 1);

        Assert.All(graph.Nodes, n => Assert.Null(graph.ChipFor(n)));
    }
}
