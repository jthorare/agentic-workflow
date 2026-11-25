using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4330: a new step's sheet says why Save is off (a line on its first empty required field), and a hint names the step the new one follows, with a field its block has.
/// The workflow served is the "found" golden with its "agent" step (a run_agent) renamed "measure", so no step is called "agent".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepSaysWhyAndNamesUpstreamTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> AddedStep(string type, string? after = null)
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var found = GoldenFiles.Api("workflows-slug", "found").Replace("\"agent\"", "\"measure\"", StringComparison.Ordinal);   // the node id, its edge, position and summary
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, found);
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
    public async Task Send_notification_says_under_its_empty_title_why_Save_is_off()
    {
        var page = await AddedStep("notify");

        Assert.False(page.CanSave);
        Assert.Equal("Fill in Title to save.", Field(page, "title").NeedText);
        Assert.Equal(string.Empty, Field(page, "body").NeedText);   // the message is optional
    }

    [Fact]
    public async Task Typing_the_title_clears_the_line_and_lights_Save()
    {
        var page = await AddedStep("notify");

        Field(page, "title").Text = "Sizes";

        Assert.True(page.CanSave);
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }

    [Fact]
    public async Task The_line_moves_to_the_next_empty_required_field()
    {
        var page = await AddedStep("memory_write");   // memory and fact are required

        Assert.Equal("Fill in Memory to save.", Field(page, "memory").NeedText);
        Field(page, "memory").Text = "drift-check";

        Assert.Equal(string.Empty, Field(page, "memory").NeedText);
        Assert.Equal("Fill in Fact to remember to save.", Field(page, "fact").NeedText);
    }

    [Fact]
    public async Task A_step_with_no_required_field_shows_no_line_and_Save_is_lit()
    {
        var page = await AddedStep("wait");

        Assert.True(page.CanSave);
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }

    [Fact]
    public async Task A_hint_names_the_step_the_new_one_follows()
    {
        var page = await AddedStep("notify", after: "measure");   // example body "{{agent.summary}}"; a run_agent has "summary"

        Assert.Equal("{{measure.summary}}", Field(page, "body").Placeholder);
        Assert.Equal(string.Empty, Field(page, "body").Text);
    }

    [Fact]
    public async Task An_If_hint_names_the_step_the_new_one_follows()
    {
        var page = await AddedStep("condition", after: "measure");   // example "agent.drifted"

        Assert.Equal("measure.drifted", Field(page, "when").Placeholder);
    }
}
