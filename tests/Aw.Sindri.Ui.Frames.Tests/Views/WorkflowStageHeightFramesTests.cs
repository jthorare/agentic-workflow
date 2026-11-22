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
    private async Task<(double Height, double Bottom, double Window)> Measure(int height)
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
        return (panel.Bounds.Height, panel.TranslatePoint(new Point(0, panel.Bounds.Height), window)!.Value.Y, window.Bounds.Height);
    }

    private const string WorkflowsSlug = "waiting-on-review";

    [Fact]
    public Task At_1440_by_900_the_stage_is_taller_than_320_and_ends_inside_the_window() => OnUi(async () =>
    {
        var (h, bottom, window) = await Measure(900);

        Assert.True(h > 320, "the stage did not grow past 320");
        Assert.True(bottom <= window, "the stage runs past the window");
    });

    [Fact]
    public Task At_1440_by_500_the_stage_is_320() => OnUi(async () =>
    {
        var (h, _, _) = await Measure(500);

        Assert.Equal(320, h, 0.5);
    });
}
