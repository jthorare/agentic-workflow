using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The failed run's callout (#3304, qa on #4448): drawn red (the error fill and border), not the amber of QueueStyles' Border.callout; its left edge on the page's edge, with the page's
/// normal gap above it, not the sheet callout's 16 px margin.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunFailureFramesTests : FrameHost
{
    private async Task<Window> OnTheFailedRun()
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "failed");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await shell.Workflows.OpenRun(RunId, CancellationToken.None);
        await Until(window, () => Visible(window, "RunFailureName"), "the failure callout never showed");
        return window;
    }

    private static Color Resource(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var brush));
        return ((ISolidColorBrush)brush!).Color;
    }

    [Fact]
    public Task The_failure_callout_is_red_not_amber() => OnUi(async () =>
    {
        var window = await OnTheFailedRun();

        var callout = Named<Border>(window, "RunFailureName");

        Assert.Equal(Resource(window, "Brush.ErrorSoft"), ((ISolidColorBrush)callout.Background!).Color);
        Assert.Equal(Resource(window, "Brush.Error"), ((ISolidColorBrush)callout.BorderBrush!).Color);
    });

    [Fact]
    public Task The_failure_callout_lines_up_with_the_graph_panel_and_sits_a_normal_gap_below_what_is_above_it() => OnUi(async () =>
    {
        var window = await OnTheFailedRun();
        var callout = Named<Border>(window, "RunFailureName");
        var panel = Named<Border>(window, "RunPanel");
        var calloutAt = callout.TranslatePoint(default, window)!.Value;
        var panelAt = panel.TranslatePoint(default, window)!.Value;

        Assert.Equal(panelAt.X, calloutAt.X);   // x=24 for both, not 40
        var above = callout.GetVisualParent()!.GetVisualChildren().OfType<Control>().TakeWhile(c => !ReferenceEquals(c, callout)).Last(static c => c.IsVisible);
        var gap = calloutAt.Y - (above.TranslatePoint(default, window)!.Value.Y + above.Bounds.Height);
        Assert.InRange(gap, 0, 16);
    });
}
