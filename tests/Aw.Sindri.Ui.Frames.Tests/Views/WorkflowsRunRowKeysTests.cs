using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Keys on the Workflows page's deeper levels (#2956): Enter on a focused run row opens the run (Space is kept for peek, #2978), and Esc goes back one level, with the focus Back puts.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunRowKeysTests : FrameHost
{
    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private async Task<(Window Window, ShellViewModel Shell)> OnRuns()
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.IsRuns && shell.Workflows.Runs.Rows.Count > 0, "the runs never opened");
        return (window, shell);
    }

    private static Border FirstRunRow(Window window) =>
        Named<ItemsControl>(window, "RunRows").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("item-row") && b.IsEffectivelyVisible);

    [Fact]
    public Task Enter_on_a_run_row_opens_the_run() => OnUi(async () =>
    {
        var (window, shell) = await OnRuns();
        var row = FirstRunRow(window);
        Assert.True(row.Focus(NavigationMethod.Tab));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        await Until(window, () => shell.Workflows.IsRun, "the run never opened from the row");
    });

    [Fact]
    public Task Escape_on_the_runs_level_goes_back_to_the_list_with_the_focus_Back_gives() => OnUi(async () =>
    {
        var (window, shell) = await OnRuns();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        await Until(window, () => shell.Workflows.IsList && Focused(window) is Border { DataContext: WorkflowRowViewModel }, "Esc never went back to the list with the first row focused");
    });

    [Fact]
    public Task Escape_on_a_run_goes_back_to_the_runs() => OnUi(async () =>
    {
        var (window, shell) = await OnRuns();
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => shell.Workflows.IsRun, "the run never opened");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        await Until(window, () => shell.Workflows.IsRuns && Focused(window) is Button, "Esc never went back to the runs with a run row's button focused");
    });
}
