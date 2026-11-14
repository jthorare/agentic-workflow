using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3682 item 3: a failed step's card on the run page has an Edit step button that opens the workflow with that step selected and its sheet open.</summary>
[Lane(TestLane.Unit)]
public sealed class RunEditFailedStepTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnAFailedRun()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["status"] = "failed";
        served["nodes"]![0]!["status"] = "failed";
        served["nodes"]![0]!["error"] = "boom";
        _f.ServeRun(served);
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        return page;
    }

    [Fact]
    public async Task Only_a_failed_step_offers_Edit_step()
    {
        var page = await OnAFailedRun();

        var failed = page.Run.Groups[0].Runs[0];
        var fine = page.Run.Groups[1].Runs[0];
        Assert.Equal(("agent", true, "Edit step"), (failed.Node, failed.CanEditStep, failed.EditStepText));
        Assert.Equal(("start", false), (fine.Node, fine.CanEditStep));
    }

    [Fact]
    public async Task Edit_step_opens_the_workflow_with_that_step_selected_and_its_sheet_open()
    {
        var page = await OnAFailedRun();

        await page.Run.Groups[0].Runs[0].EditStep(Token);

        Assert.True(page.IsRuns, "the page did not go back up to the workflow");
        Assert.Equal(("agent", true), (page.Runs.Graph!.SelectedId, page.IsSettingsOpen));
        Assert.Equal(["prompt"], page.Editor.Fields.Select(f => f.Name), StringComparer.Ordinal);
    }
}
