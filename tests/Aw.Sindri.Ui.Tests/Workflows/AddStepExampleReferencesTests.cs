using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3944: Add step prefills the block's doc example, and some examples read steps of the doc page's graph ("{{triage.items}}"). A field whose example names a step the workflow
/// can't read (it is not the step the new one follows, nor upstream of it) starts empty with the example as its hint; a reference to an upstream step, or to the trigger, stays.
/// A condition's "when" names steps bare ("agent.drifted"), and those count too. A refused save is shown once, in the sheet.
/// The workflow served is the "found" golden: start (manual) -> agent (run_agent); "no agent" is the same graph with "agent" renamed "tell".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepExampleReferencesTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Add step of the block, placed after the step <after> when one is given (the selected step), on the "found" workflow or on it with "agent" renamed "tell".
    private async Task<WorkflowsPageViewModel> AddedStep(string type, string? after = null, bool noAgent = false)
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        if (noAgent)
        {
            _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflows-slug", "found").Replace("\"agent\"", "\"tell\"", StringComparison.Ordinal));
        }

        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        if (after is not null)
        {
            Assert.True(page.Runs.Graph!.SelectNode(after));
        }

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
        Assert.NotEmpty(Field(page, "code").Text);   // the rest of the example is still filled in
    }

    [Fact]
    public async Task A_new_step_keeps_an_example_reference_to_the_step_it_follows()
    {
        var page = await AddedStep("notify", after: "agent");   // example body "{{agent.summary}}": the new step goes after "agent"

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
    public async Task A_new_step_leaves_a_reference_to_a_step_that_is_not_upstream_empty()
    {
        var page = await AddedStep("notify", after: "start");   // "agent" exists but runs after "start": the validator would say GRAPH-REF-001

        var body = Field(page, "body");

        Assert.Equal(string.Empty, body.Text);
        Assert.Equal("{{agent.summary}}", body.Placeholder);
    }

    [Fact]
    public async Task A_new_step_that_follows_nothing_leaves_every_step_reference_empty()
    {
        var page = await AddedStep("notify");   // nothing selected: the new step has no upstream step

        Assert.Equal(string.Empty, Field(page, "body").Text);
    }

    [Fact]
    public async Task A_condition_opens_its_when_empty_when_it_names_a_step_the_workflow_lacks()
    {
        var page = await AddedStep("condition", after: "tell", noAgent: true);   // example when "agent.drifted", bare: start -> tell has no "agent"

        var when = Field(page, "when");

        Assert.Equal(string.Empty, when.Text);
        Assert.Equal("agent.drifted", when.Placeholder);
        Assert.False(page.CanSave);   // "when" is required
        when.Text = "tell.ok";
        Assert.True(page.CanSave);
    }

    [Fact]
    public async Task A_condition_keeps_its_when_when_it_reads_the_step_it_follows()
    {
        var page = await AddedStep("condition", after: "agent");

        Assert.Equal("agent.drifted", Field(page, "when").Text);
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

    [Fact]
    public async Task A_closed_sheet_leaves_no_old_refusal_on_the_page()
    {
        var page = await AddedStep("code");
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-GRAPH-002", "GRAPH-REF-003 node 'code_1' field 'config.code': refused");
        await page.Save(Token);

        page.CloseSettings();   // the new step is dropped, and its refusal with it

        Assert.False(page.IsSettingsOpen);
        Assert.Equal(string.Empty, page.GraphEdit.FailureText);
        Assert.False(page.GraphEdit.HasFailure);
    }
}
