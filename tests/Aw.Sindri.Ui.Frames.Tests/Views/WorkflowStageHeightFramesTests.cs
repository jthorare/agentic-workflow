using Avalonia;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The workflow canvas fills the window under the header (#3684): the page scrolls, and the stage is sized from the scroll viewer's viewport, never under 320 px.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowStageHeightFramesTests : FrameHost
{
    // The stage's height, its bottom in the window, and the bottom of the page scroller's viewport in the window.
    private async Task<(double Height, double Bottom, double ViewportBottom)> Measure(int height)
    {
        Server.WorkflowAnswers[WorkflowsSlug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[WorkflowsSlug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1440, height: height);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(WorkflowsSlug, TestContext.Current.CancellationToken);
        await Until(window, () => Visible(window, "RunsStagePanel"), "the graph never showed");
        Settle(window);
        var panel = Named<Border>(window, "RunsStagePanel");
        var pages = Named<ScrollViewer>(window, "Pages");
        var viewportBottom = pages.TranslatePoint(default, window)!.Value.Y + pages.Viewport.Height;
        return (panel.Bounds.Height, panel.TranslatePoint(new Point(0, panel.Bounds.Height), window)!.Value.Y, viewportBottom);
    }

    private const string WorkflowsSlug = "waiting-on-review";

    [Fact]
    public Task At_1440_by_900_the_stage_is_taller_than_320_and_ends_inside_the_window() => OnUi(async () =>
    {
        var (h, bottom, viewportBottom) = await Measure(900);

        Assert.True(h > 320, "the stage did not grow past 320");
        Assert.Equal(viewportBottom - 16, bottom, 0.5);   // it fills the page's viewport down to the 16 px gap, so it ends inside the window
    });

    // The window can't be shorter than Size.WindowMinHeight (600), so the 320 floor never binds at 100 % text on this page; GraphZoomTests checks the floor itself.
    [Fact]
    public Task At_the_600_px_window_minimum_the_stage_still_fills_the_viewport_and_is_at_least_320() => OnUi(async () =>
    {
        var (h, bottom, viewportBottom) = await Measure(600);

        Assert.True(h >= 320, "the stage went under 320");
        Assert.Equal(viewportBottom - 16, bottom, 0.5);
    });
}
