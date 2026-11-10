using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the read-only Workflows page (#2258 PR 17 part 1) at 1440 and 900 px wide, against the in-memory API serving the workflow goldens. Saved under frames/ as workflows-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFramesTests : FrameHost
{
    private const string Prefix = "workflows-";

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows(double width)
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        return (window, shell);
    }

    // A theme token's colour in one variant, so a test can compare what a chip drew with what each theme says.
    private static Color ThemeColor(Window window, string key, ThemeVariant variant)
    {
        Assert.True(window.TryFindResource(key, variant, out var value), $"{key} is missing in {variant}");
        return Assert.IsType<ISolidColorBrush>(value, exactMatch: false).Color;
    }

    private static Color ChipColor(TextBlock chip) => Assert.IsType<ISolidColorBrush>(chip.Foreground, exactMatch: false).Color;

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_list_of_workflows(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        Assert.True(Visible(window, "WorkflowRows"));
        Assert.Equal(shell.Workflows.Head, Named<TextBlock>(window, "WorkflowsHead").Text);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_empty_state_offers_New_workflow_and_names_the_agent_command(double width) => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");

        Assert.False(Visible(window, "WorkflowsHead"));
        var button = Named<Button>(window, "WorkflowsNew");
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEnabled);
        Assert.False(Visible(window, "WorkflowsCreateForm"));
        Assert.Equal("sindri wf create", Named<TextBlock>(window, "WorkflowsAgentCommand").Text);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"empty-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_New_workflow_form_asks_for_a_name_and_a_template_and_says_why_Create_is_off(double width) => OnUi(async () =>
    {
        Server.WorkflowsAnswer = RouteAnswer.Api("workflows", "list-empty");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.IsEmpty, "the Workflows page never read the empty list");

        shell.Workflows.OpenCreate();
        await Until(window, () => Visible(window, "WorkflowsCreateForm"), "the New workflow form never showed");

        Assert.False(Visible(window, "WorkflowsNew"));
        Assert.True(Visible(window, "WorkflowsNewName"));
        Assert.True(Visible(window, "WorkflowsNewTemplate"));
        Assert.False(Named<Button>(window, "WorkflowsCreate").IsEnabled);
        Assert.True(Visible(window, "WorkflowsNewReason"));
        Assert.True(Visible(window, "WorkflowsAgentCommand"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"empty-form-{width}"), Prefix);
    });

    [Fact]
    public Task A_row_title_does_not_hug_the_rows_top_border_and_New_workflow_sits_in_the_header_beside_the_list() => OnUi(async () =>
    {
        var (window, _) = await OnWorkflows(1280);
        Settle(window);

        var header = Named<Button>(window, "WorkflowsNewHeader");
        Assert.True(header.IsEffectivelyVisible && header.IsEnabled);
        var list = Named<ItemsControl>(window, "WorkflowRows");
        var row = list.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("item-row"));
        var title = row.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("row-title"));
        Assert.True(title.TranslatePoint(default, row)!.Value.Y >= 8, "the title is at least 8 px below the row's top");
        Assert.False(title.Focusable, "the row draws the focus ring, not the title");
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_workflows_runs(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        Assert.True(Visible(window, "RunRows"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"runs-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_runs_detail(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        Assert.True(Visible(window, "RunGroups"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"run-{width}"), Prefix);
    });

    // The light theme: the runs (status chips in the ok and error tones, Run now) and a running run's detail (Cancel run), each beside its dark frame above.
    [Fact]
    public Task A_workflows_runs_in_the_light_theme() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        window.RequestedThemeVariant = ThemeVariant.Light;

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");

        Assert.True(Visible(window, "RunNow"));
        await Until(window, () => window.ActualThemeVariant == ThemeVariant.Light, "the window never took the light theme");
        var chip = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Classes.Contains("wf-chip") && t.Classes.Contains("ok"));
        Assert.Equal(ThemeColor(window, "Brush.Success", ThemeVariant.Light), ChipColor(chip));   // the theme token (Success is one colour in both variants, Theme/Tokens.axaml)
        Frame(window, "runs-light-1440", Prefix);
    });

    [Fact]
    public Task A_running_runs_detail_in_the_light_theme() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "running");   // after OnWorkflows, which serves the finished run
        window.RequestedThemeVariant = ThemeVariant.Light;

        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.Run.Groups.Count > 0, "the run never showed");

        Assert.True(Visible(window, "RunCancel"));
        await Until(window, () => window.ActualThemeVariant == ThemeVariant.Light, "the window never took the light theme");
        var chip = Named<TextBlock>(window, "RunChip");   // a running run: the ok tone
        Assert.Equal(ThemeColor(window, "Brush.Success", ThemeVariant.Light), ChipColor(chip));
        Frame(window, "run-light-1440", Prefix);
    });

    // A level opens on its Back button, and Back lands on the first row of the level it returns to, never on the hidden Back button.
    [Fact]
    public Task Focus_goes_to_Back_when_a_level_opens_and_to_the_first_row_when_Back_returns() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(1440);

        var title = Named<ItemsControl>(window, "WorkflowRows").GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible);
        Click(title);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0 && Focused(window)?.Name == "WorkflowsBack", "Back never took focus when the runs opened");

        var back = Named<Button>(window, "WorkflowsBack");
        Click(back);
        await Until(window, () => shell.Workflows.IsList && Focused(window) is Button { DataContext: WorkflowRowViewModel }, "the first workflow never took focus when Back returned");

        var first = Assert.IsType<Button>(Focused(window));
        Assert.Equal(shell.Workflows.Rows[0], first.DataContext);
        Assert.False(back.IsEffectivelyVisible);
    });
}
