using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The workflow page's crumb (#3655): it is named for where it goes, and click and Enter both take the page back to the list, with focus on the list.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsCrumbFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> OnAWorkflow()
    {
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => Visible(window, "WorkflowsBack"), "the workflow's page never showed its crumb");
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task The_crumb_is_named_Back_to_Workflows() => OnUi(async () =>
    {
        var (window, _) = await OnAWorkflow();

        Assert.Equal("Back to Workflows", AutomationProperties.GetName(Named<Button>(window, "WorkflowsBack")));
    });

    [Fact]
    public Task Clicking_the_crumb_returns_to_the_list_and_focus_lands_in_it() => OnUi(async () =>
    {
        var (window, shell) = await OnAWorkflow();

        Named<Button>(window, "WorkflowsBack").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Workflows.IsList, "the click never went back to the list");
        Settle(window);

        Assert.False(Visible(window, "WorkflowsBack"));
        Assert.True(Visible(window, "WorkflowRows"));
        var focused = window.FocusManager?.GetFocusedElement() as Control;
        Assert.NotNull(focused);
        Assert.True(Named<ItemsControl>(window, "WorkflowRows").IsVisualAncestorOf(focused), "focus did not land in the workflow list");
    });

    [Fact]
    public Task Enter_on_the_crumb_returns_to_the_list_and_focus_lands_in_it() => OnUi(async () =>
    {
        var (window, shell) = await OnAWorkflow();
        Assert.True(Named<Button>(window, "WorkflowsBack").Focus(NavigationMethod.Tab));

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Workflows.IsList, "Enter never went back to the list");
        Settle(window);

        var focused = window.FocusManager?.GetFocusedElement() as Control;
        Assert.NotNull(focused);
        Assert.True(Named<ItemsControl>(window, "WorkflowRows").IsVisualAncestorOf(focused), "focus did not land in the workflow list");
    });
}
