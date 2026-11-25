using System.Net;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The failed-check line over the graph (#4380, qa on #4460): drawn as a failure (the error fill and border, its own class because QueueStyles' amber Border.callout wins over callout.fail),
/// with a Retry button that asks validate again.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsFindingsFailedFramesTests : FrameHost
{
    private async Task<Window> OnTheWorkflowWithAFailedCheck()
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.ValidateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-GRAPH-008", "no such workflow");
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => Visible(window, "FindingsFailed"), "the failed-check line never showed");
        return window;
    }

    private static Color Resource(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var brush));
        return ((ISolidColorBrush)brush!).Color;
    }

    [Fact]
    public Task The_failed_check_line_is_drawn_as_a_failure_not_amber() => OnUi(async () =>
    {
        var window = await OnTheWorkflowWithAFailedCheck();

        var line = Named<Border>(window, "FindingsFailed");

        Assert.Equal(Resource(window, "Brush.ErrorSoft"), ((ISolidColorBrush)line.Background!).Color);
        Assert.Equal(Resource(window, "Brush.Error"), ((ISolidColorBrush)line.BorderBrush!).Color);
    });

    [Fact]
    public Task The_failed_check_line_has_a_Retry_button_that_asks_validate_again() => OnUi(async () =>
    {
        var window = await OnTheWorkflowWithAFailedCheck();
        Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "passed");

        var retry = Named<Button>(window, "FindingsRetry");
        Assert.True(retry.IsEffectivelyVisible);
        retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        await Until(window, () => Server.ValidatedWorkflows.Count == 2, "Retry never asked validate again");
        await Until(window, () => !Visible(window, "FindingsFailed"), "the failed-check line stayed after a good answer");
    });
}
