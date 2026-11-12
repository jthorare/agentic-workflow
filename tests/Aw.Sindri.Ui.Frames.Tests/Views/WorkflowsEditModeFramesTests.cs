using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2807 items 2, 3 and 5: Edit turns on graph edit mode (the edit bar shows, enabled), step settings open per step from the step summary line and lock the bar, and Done leaves edit mode.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsEditModeFramesTests : FrameHost
{
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    [Fact]
    public Task Edit_shows_the_edit_bar_enabled_and_step_settings_open_the_sheet_and_lock_it() => OnUi(async () =>
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");

        // opened to look at: no edit bar, no sheet
        Assert.False(Visible(window, "RunsEdit"), "the edit bar shows on a workflow opened to look at");
        Assert.False(Visible(window, "EditorSheet"));

        // Edit: the bar shows and works; the sheet stays shut
        Click(Named<Button>(window, "WorkflowEdit"));
        await Until(window, () => Visible(window, "RunsEdit"), "Edit never showed the edit bar");
        Assert.True(Named<Button>(window, "EditAdd").IsEnabled, "Add step is off in edit mode");
        Assert.False(Visible(window, "EditorSheet"), "Edit opened the step settings");
        Assert.True(Visible(window, "WorkflowEditDone"));
        Assert.False(Visible(window, "WorkflowEdit"), "Edit shows beside Done while edit mode is on (#2807)");

        // Step settings: the sheet opens for the selected step and the bar is locked with its note
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("agent"));
        await Until(window, () => Visible(window, "StepSettings"), "a selected step never offered Step settings");
        Click(Named<Button>(window, "StepSettings"));
        await Until(window, () => Visible(window, "EditorSheet") && shell.Workflows.Editor.Fields.Count > 0, "Step settings never opened the sheet");
        Assert.False(Named<Button>(window, "EditAdd").IsEnabled, "the bar stays enabled while step settings are open");
        Assert.Equal("Close step settings to change the graph.", Named<Border>(window, "EditSheetOpenName").GetValue(Avalonia.Automation.AutomationProperties.NameProperty));

        // Close: back to edit mode with the bar working; Done: out of edit mode
        Click(Named<Button>(window, "EditorClose"));
        await Until(window, () => !Visible(window, "EditorSheet") && Named<Button>(window, "EditAdd").IsEnabled, "Close never returned to edit mode");
        Click(Named<Button>(window, "WorkflowEditDone"));
        await Until(window, () => !Visible(window, "RunsEdit"), "Done never hid the edit bar");
        Assert.True(Visible(window, "WorkflowEdit"), "Edit never came back after Done (#2807)");
    });

    [Fact]
    public Task Enter_on_Edit_moves_focus_to_Done_not_the_breadcrumb() => OnUi(async () =>
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");

        var edit = Named<Button>(window, "WorkflowEdit");
        Assert.True(edit.Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        await Until(window, () => Visible(window, "WorkflowEditDone"), "Enter on Edit never turned edit mode on");
        await Until(window, () => Named<Button>(window, "WorkflowEditDone").IsFocused, "focus never reached Done after Enter on Edit (it dropped to the breadcrumb)");
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task The_list_count_keeps_12_px_from_New_workflow(int width) => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: width, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        window.UpdateLayout();
        var head = Named<TextBlock>(window, "WorkflowsHead");
        var button = Named<Button>(window, "WorkflowsNewHeader");
        var headRight = head.TranslatePoint(new Point(head.Bounds.Width, 0), window)!.Value.X;
        var buttonLeft = button.TranslatePoint(new Point(0, 0), window)!.Value.X;
        var gap = buttonLeft - headRight;
        Assert.True(gap >= 12, $"the gap is {gap.ToString(CultureInfo.InvariantCulture)} px, under 12");
    });
}
