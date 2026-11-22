using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3682 item 2: a refused save belongs to the sheet it came from. It is cleared when the sheet closes, when the selection moves and when the page shows another workflow or the list.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsEditErrorScopeTests
{
    private const string Other = "evening-check";

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

    private async Task<WorkflowsPageViewModel> WithARefusedSave()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        _f.Server.WorkflowAnswers[Other] = RouteAnswer.Api("workflows-slug", "found");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.Edit(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-GRAPH-001", "Node schedule_1 has no schedule.");
        page.Editor.Fields[0].Text = "Check twice";
        await page.Save(Token);
        Assert.StartsWith("Couldn't save the edit", page.Editor.FailureText, StringComparison.Ordinal);   // the refusal is on screen on its own sheet
        return page;
    }

    [Fact]
    public async Task A_refused_save_on_one_workflow_shows_no_error_on_the_next_workflow_opened()
    {
        var page = await WithARefusedSave();

        await page.OpenWorkflow(Other, Token);

        Assert.Equal((string.Empty, false), (page.Editor.FailureText, page.GraphEdit.HasFailure));
    }

    [Fact]
    public async Task A_refused_save_is_gone_after_Back_to_the_list()
    {
        var page = await WithARefusedSave();

        page.Back();

        Assert.Equal((string.Empty, false), (page.Editor.FailureText, page.GraphEdit.HasFailure));
    }

    [Fact]
    public async Task Closing_the_sheet_clears_its_refusal()
    {
        var page = await WithARefusedSave();

        page.CloseEditor();

        Assert.Equal(string.Empty, page.Editor.FailureText);
    }

    [Fact]
    public async Task Moving_to_another_step_clears_the_refusal_and_keeps_the_sheet_open()
    {
        var page = await WithARefusedSave();

        page.Editor.Select("start");

        Assert.Equal((string.Empty, true), (page.Editor.FailureText, page.IsEditing));
    }
}
