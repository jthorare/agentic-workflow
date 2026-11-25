using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4272: a new step's fields start empty; the block's example is only a hint. A hint never names a step the new one can't read: that step id becomes "&lt;step id&gt;".
/// The workflow served is the "found" golden with its "agent" step renamed "measure", so no step is called "agent".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepStartsEmptyTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> AddedStep(string type, bool withAgent, string? after = null)
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        if (!withAgent)
        {
            var found = GoldenFiles.Api("workflows-slug", "found").Replace("\"agent\"", "\"measure\"", StringComparison.Ordinal);   // the node id, its edge, position and summary
            _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, found);
        }

        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        if (after is not null)
        {
            Assert.True(page.Runs.Graph!.SelectNode(after));   // the new step follows it, so it may read it (#4268)
        }

        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));
        await page.GraphEdit.AddStep(block, Token);
        return page;
    }

    private static ConfigFieldViewModel Field(WorkflowsPageViewModel page, string name) =>
        page.Editor.Fields.Single(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    [Fact]
    public async Task Send_notification_opens_with_an_empty_title_and_no_agent_in_any_hint()
    {
        var page = await AddedStep("notify", withAgent: false);   // example {"title":"Workflow update","body":"{{agent.summary}}"}

        Assert.All(page.Editor.Fields, static f => Assert.Equal(string.Empty, f.Text));
        Assert.All(page.Editor.Fields, static f => Assert.DoesNotContain("agent.", f.Placeholder, StringComparison.Ordinal));
        Assert.Equal("Workflow update", Field(page, "title").Placeholder);   // a neutral example title (#4330)
        Assert.Equal("{{<step id>.summary}}", Field(page, "body").Placeholder);
        Assert.False(page.CanSave);   // the title is required and nothing is typed
    }

    [Fact]
    public async Task If_opens_empty_with_no_agent_in_its_hint()
    {
        var page = await AddedStep("condition", withAgent: false);   // example {"when":"agent.drifted"}

        var when = Field(page, "when");

        Assert.Equal(string.Empty, when.Text);
        Assert.Equal("<step id>.drifted", when.Placeholder);
    }

    [Fact]
    public async Task A_hint_keeps_a_step_the_new_one_follows_and_the_field_still_starts_empty()
    {
        var page = await AddedStep("notify", withAgent: true, after: "agent");   // the "found" golden has a step "agent"

        var body = Field(page, "body");

        Assert.Equal(string.Empty, body.Text);
        Assert.Equal("{{agent.summary}}", body.Placeholder);
    }

    [Fact]
    public async Task A_hint_keeps_a_trigger_reference()
    {
        var page = await AddedStep("run_agent", withAgent: false);   // example prompt "Check {{trigger.ref}} ..."

        var prompt = Field(page, "prompt");

        Assert.Equal(string.Empty, prompt.Text);
        Assert.StartsWith("Check {{trigger.ref}}", prompt.Placeholder, StringComparison.Ordinal);
    }
}
