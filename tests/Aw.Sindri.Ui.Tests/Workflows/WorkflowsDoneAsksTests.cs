using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Done with a changed field in the step sheet asks first (#3655): typed text survives Done until the person picks Discard.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsDoneAsksTests
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

    private async Task<WorkflowsPageViewModel> WithATypedField()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        return page;
    }

    [Fact]
    public async Task Done_with_a_changed_field_asks_and_keeps_the_typed_text_and_the_sheet()
    {
        var page = await WithATypedField();

        page.RequestDone();

        Assert.True(page.AsksSaveFirst);
        Assert.True(page.IsEditing);
        Assert.True(page.IsSettingsOpen);
        Assert.Equal("Check twice", page.Editor.Fields[0].Text);
    }

    [Fact]
    public async Task Discard_leaves_edit_mode_and_drops_the_question()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.DiscardAndLeave();

        Assert.False(page.IsEditing);
        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task Keep_editing_closes_the_question_and_keeps_the_text()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.KeepEditing();

        Assert.False(page.AsksSaveFirst);
        Assert.True(page.IsSettingsOpen);
        Assert.Equal("Check twice", page.Editor.Fields[0].Text);
    }

    [Fact]
    public async Task Done_with_no_changed_field_leaves_at_once()
    {
        var page = await WithATypedField();
        page.Editor.Fields[0].Text = "Look for drift";   // typed back to what it was

        page.RequestDone();

        Assert.False(page.AsksSaveFirst);
        Assert.False(page.IsEditing);
    }
}
