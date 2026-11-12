using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The keyboard map (spec 4.6) wired to the page: N and Ctrl+K open Add step, Ctrl+Enter saves the open sheet, Esc closes the topmost layer only, and a bare letter does nothing inside a field.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowEditorKeysPageTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[{"group":"AI","blocks":[{"type":"prompt","title":"Prompt","isTrigger":false,"config":[{"name":"prompt","type":"text","required":true}],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Open()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task N_with_the_canvas_focused_opens_the_Add_step_picker_and_sends_nothing()
    {
        var page = await Open();

        Assert.True(await page.PressKey(new EditorKeyPress("N", CanvasFocus: true), Token));

        Assert.True(page.GraphEdit.IsAdding);
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }

    [Fact]
    public async Task Ctrl_K_opens_Add_step_unless_the_shell_owns_focus()
    {
        var page = await Open();

        Assert.False(await page.PressKey(new EditorKeyPress("K", Ctrl: true, ShellFocus: true), Token));
        Assert.False(page.GraphEdit.IsAdding);
        Assert.True(await page.PressKey(new EditorKeyPress("K", Ctrl: true), Token));
        Assert.True(page.GraphEdit.IsAdding);
    }

    [Fact]
    public async Task A_bare_letter_inside_a_field_does_nothing()
    {
        var page = await Open();

        Assert.False(await page.PressKey(new EditorKeyPress("N", CanvasFocus: true, InField: true), Token));

        Assert.False(page.GraphEdit.IsAdding);
    }

    [Fact]
    public async Task Ctrl_Enter_with_the_sheet_open_saves_the_step_in_one_call()
    {
        var page = await Open();
        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.PickBlock(page.GraphEdit.Groups[0].Blocks[0], Token);
        Assert.Single(page.Editor.Fields).Text = "Say hi";

        Assert.True(await page.PressKey(new EditorKeyPress("Enter", Ctrl: true, InField: true), Token));

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.False(page.IsEditing);
    }

    [Fact]
    public async Task Ctrl_Enter_with_no_sheet_open_is_not_the_editors()
    {
        var page = await Open();

        Assert.False(await page.PressKey(new EditorKeyPress("Enter", Ctrl: true), Token));
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }

    [Fact]
    public async Task Esc_closes_one_layer_at_a_time_a_picker_before_a_sheet_and_nothing_is_saved()
    {
        var page = await Open();
        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.PickBlock(page.GraphEdit.Groups[0].Blocks[0], Token);
        await page.GraphEdit.BeginAdd(Token);   // refused while the sheet is open: the sheet is the only layer

        Assert.True(await page.PressKey(new EditorKeyPress("Escape", InField: true), Token));
        Assert.False(page.IsEditing);
        Assert.True(await page.PressKey(new EditorKeyPress("N", CanvasFocus: true), Token));
        Assert.True(page.GraphEdit.IsAdding);
        Assert.True(await page.PressKey(new EditorKeyPress("Escape"), Token));
        Assert.False(page.GraphEdit.IsAdding);
        Assert.False(await page.PressKey(new EditorKeyPress("Escape"), Token));   // nothing open: the shell's own Esc runs
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }
}
