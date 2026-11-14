using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3683 qa note 2: the header counts the paused workflows, so "14 workflows · 9 active" no longer hides them: "14 workflows · 9 active · 1 paused". Off workflows are the rest and are not named.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsHeadCountsTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Row(string slug, int? active, bool? paused = null) =>
        new() { ["slug"] = slug, ["name"] = slug, ["activeRevision"] = active, ["latestRevision"] = 1, ["changedBy"] = "owner", ["changedAt"] = "2026-01-01T00:00:00Z", ["paused"] = paused };

    [Fact]
    public async Task The_header_counts_active_and_paused_separately_and_leaves_off_out()
    {
        _f.ServeList(new JsonObject { ["workflows"] = new JsonArray(Row("a", 1), Row("b", 1), Row("gate", 1, paused: true), Row("off", null)) });

        var page = await _f.Shown(Token);

        Assert.Equal("4 workflows · 2 active · 1 paused", page.Head);
    }

    [Fact]
    public async Task With_nothing_paused_the_header_is_as_before()
    {
        _f.ServeList(new JsonObject { ["workflows"] = new JsonArray(Row("a", 1), Row("off", null)) });

        var page = await _f.Shown(Token);

        Assert.Equal("2 workflows · 1 active", page.Head);
    }

    [Fact]
    public async Task One_workflow_reads_singular()
    {
        _f.ServeList(new JsonObject { ["workflows"] = new JsonArray(Row("gate", 1, paused: true)) });

        var page = await _f.Shown(Token);

        Assert.Equal("1 workflow · 0 active · 1 paused", page.Head);
    }
}
