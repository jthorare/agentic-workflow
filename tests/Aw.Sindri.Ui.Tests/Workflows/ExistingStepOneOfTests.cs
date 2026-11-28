using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4369: a saved Wait step opened with neither or both of Wait for and Wait until says why Save is off as soon as the sheet opens, not after the first keystroke.
/// The workflow served is the "found" golden with its "agent" step turned into a wait with the config under test.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ExistingStepOneOfTests
{
    private const string Empty = "Fill in Wait for or Wait until to save.";
    private const string Both = "Fill in only one of Wait for and Wait until.";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // blocks: the workflows-blocks golden, or with every "oneOf" key taken out, as an older Sindri sends it
    private async Task<WorkflowsPageViewModel> OpenedWait(string config, bool serverSendsOneOf = true)
    {
        var blocks = JsonNode.Parse(GoldenFiles.Api("workflows-blocks", "listed"))!;
        if (!serverSendsOneOf)
        {
            foreach (var block in blocks["groups"]!.AsArray().SelectMany(static g => g!["blocks"]!.AsArray()))
            {
                _ = block!.AsObject().Remove("oneOf");
            }
        }

        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, blocks.ToJsonString());
        var workflow = JsonNode.Parse(GoldenFiles.Api("workflows-slug", "found"))!;
        var node = workflow["spec"]!["nodes"]![1]!.AsObject();
        node["type"] = "wait";
        _ = node.Remove("title");
        node["config"] = JsonNode.Parse(config);
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, workflow.ToJsonString());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        Assert.True(page.Runs.Graph!.SelectNode("agent"));
        await page.OpenSettings(Token);
        return page;
    }

    private static ConfigFieldViewModel Field(WorkflowsPageViewModel page, string name) =>
        page.Editor.Fields.Single(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    [Fact]
    public async Task A_saved_Wait_with_neither_field_opens_with_the_line_and_Save_off()
    {
        var page = await OpenedWait("{}");

        Assert.False(page.CanSave);
        Assert.Equal(Empty, Field(page, "duration").NeedText);
        Assert.Equal(string.Empty, Field(page, "until").NeedText);
    }

    [Fact]
    public async Task A_saved_Wait_with_both_fields_opens_with_the_line_and_clearing_one_clears_it()
    {
        var page = await OpenedWait("""{"duration":"10m","until":"07:00"}""");

        Assert.False(page.CanSave);
        Assert.Equal(Both, Field(page, "duration").NeedText);

        Field(page, "duration").Text = string.Empty;

        Assert.True(page.CanSave);
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }

    [Fact]
    public async Task A_saved_Wait_with_one_field_opens_with_Save_on_and_no_line()
    {
        var page = await OpenedWait("""{"until":"07:00"}""");

        Assert.True(page.CanSave);
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }

    [Fact]
    public async Task Text_of_only_spaces_counts_as_empty()
    {
        var page = await OpenedWait("""{"duration":"10m"}""");

        Field(page, "until").Text = "   ";

        Assert.True(page.CanSave);   // the spaces are not a second value
        Assert.False(Field(page, "duration").HasNeed);

        Field(page, "duration").Text = "  ";

        Assert.False(page.CanSave);
        Assert.Equal(Empty, Field(page, "duration").NeedText);
    }

    [Fact]
    public async Task A_server_that_sends_no_oneOf_leaves_the_rule_off()
    {
        var page = await OpenedWait("{}", serverSendsOneOf: false);

        Assert.True(page.CanSave);   // an older Sindri: Save follows the required fields only, and Wait has none
        Assert.All(page.Editor.Fields, static f => Assert.False(f.HasNeed));
    }
}
