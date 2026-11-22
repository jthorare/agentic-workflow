using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3944: Add step prefills the block's doc example, and some examples read steps of the doc page's graph ("{{triage.items}}"). A field whose example names a step the workflow
/// lacks starts empty with the example as its hint; a reference to a step the workflow has, or to the trigger, stays. A refused save is shown once, in the sheet.
/// The workflow served is the "found" golden: steps "start" (manual) and "agent" (run_agent).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepExampleReferencesTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> AddedStep(string type)
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));
        await page.GraphEdit.AddStep(block, Token);
        return page;
    }

    private static ConfigFieldViewModel Field(WorkflowsPageViewModel page, string name) =>
        page.Editor.Fields.Single(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    [Fact]
    public async Task A_new_step_leaves_a_field_empty_when_its_example_names_a_step_the_workflow_lacks()
    {
        var page = await AddedStep("code");   // example input "{{triage.items}}": the workflow has no "triage"

        var input = Field(page, "input");

        Assert.Equal(string.Empty, input.Text);
        Assert.Equal("{{triage.items}}", input.Placeholder);
        Assert.NotEqual(string.Empty, Field(page, "code").Text);   // the rest of the example is still filled in
    }

    [Fact]
    public async Task A_new_step_keeps_an_example_reference_to_a_step_the_workflow_has()
    {
        var page = await AddedStep("notify");   // example body "{{agent.summary}}": the workflow has "agent"

        var body = Field(page, "body");

        Assert.Equal("{{agent.summary}}", body.Text);
        Assert.Equal(string.Empty, body.Placeholder);
    }

    [Fact]
    public async Task A_new_step_keeps_an_example_reference_to_the_trigger()
    {
        var page = await AddedStep("run_agent");   // example prompt "Check {{trigger.ref}} ..."

        Assert.StartsWith("Check {{trigger.ref}}", Field(page, "prompt").Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_required_field_left_empty_this_way_keeps_Save_off_until_typed()
    {
        var page = await AddedStep("collect");   // example value "{{summarize.text}}", required; the workflow has no "summarize"

        Assert.False(page.CanSave);
        Field(page, "value").Text = "{{agent.text}}";
        Assert.True(page.CanSave);
    }

    [Fact]
    public async Task A_refused_save_shows_once_while_the_sheet_is_open()
    {
        var page = await AddedStep("code");
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-GRAPH-002", "GRAPH-REF-003 node 'code_1' field 'config.code': refused");

        await page.Save(Token);

        Assert.True(page.IsSettingsOpen);   // a refusal keeps the sheet open
        Assert.Contains("GRAPH-REF-003", page.Editor.FailureText, StringComparison.Ordinal);   // the sheet's line
        Assert.Equal(string.Empty, page.GraphEdit.FailureText);   // the page's line under "Close step settings to change the graph."
        Assert.False(page.GraphEdit.HasFailure);
    }
}
