using Avalonia;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The header of one workflow with a long name (#2807): at text scale 1.3 and 1000 px wide the title ends in an ellipsis inside the window, keeps its full text as a tooltip, and the "Workflows" crumb keeps its width.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowHeaderLayoutTests : FrameHost
{
    private const string LongSlug = "waiting-on-review-then-post-the-summary-to-the-channel-and-wait-again";

    [Fact]
    public Task A_long_title_ends_in_an_ellipsis_inside_the_window_and_the_crumb_keeps_its_width() => OnUi(async () =>
    {
        Server.WorkflowAnswers[LongSlug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[LongSlug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1000, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        var resources = Application.Current!.Resources;
        var crumb = Named<Button>(window, "WorkflowsBack");
        TextScale.Apply(resources, 1.3);
        try
        {
            await shell.Workflows.OpenWorkflow(LongSlug, TestContext.Current.CancellationToken);
            await Until(window, () => Visible(window, "WorkflowsBack") && Visible(window, "RunNow"), "the workflow's page never showed");
            Settle(window);

            var back = Named<Button>(window, "WorkflowsBack");
            var title = Named<TextBlock>(window, "WorkflowsTitle");
            Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
            Assert.Equal(LongSlug, ToolTip.GetTip(title));
            Assert.True(title.TextLayout.TextLines.Any(l => l.HasCollapsed), "the long title was not shortened");
            Assert.True(title.TranslatePoint(new Point(title.Bounds.Width, 0), window)!.Value.X <= window.Bounds.Width, "the title runs past the window");
            Assert.True(back.Bounds.Width >= back.DesiredSize.Width - 0.5 && back.Bounds.Width >= back.Content!.ToString()!.Length * 4, "the crumb shrank");
            Assert.False(Visible(window, "WorkflowsHead"), "the list's counts show on a workflow's page");
            Assert.True(Visible(window, "WorkflowsState"), "the workflow's own state does not show");
            Assert.Equal(shell.Workflows.DetailStateText, Named<TextBlock>(window, "WorkflowsState").Text);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });
}
