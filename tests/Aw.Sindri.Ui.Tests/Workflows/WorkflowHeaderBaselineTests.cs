using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A workflow's page header (#2689): the title, the status text and the Run now / Edit buttons share one text baseline, at text scale 1.0 and 1.3, the way Health's labels and values do.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowHeaderBaselineTests : FrameHost
{
    // why: the text sits in the window; the baseline is the text layout's own
    private static double Baseline(TextBlock text, Window window) => text.TranslatePoint(default, window)!.Value.Y + text.TextLayout.Baseline;

    private static TextBlock TextOf(Control control) => control as TextBlock ?? control.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible);

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task The_title_the_status_text_and_the_buttons_share_a_baseline_at_scale(double scale) => OnUi(async () =>
    {
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowRunAnswers[RunId] = RouteAnswer.Api("workflow-runs-run", "finished");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            await shell.Workflows.OpenWorkflow(Slug, TestContext.Current.CancellationToken);
            await Until(window, () => Visible(window, "RunsHead") && Visible(window, "RunNow") && Visible(window, "WorkflowEdit"), "the runs header never showed");
            Settle(window);

            var title = Baseline(Named<TextBlock>(window, "RunsHead"), window);   // the workflow's name moved up to the page title (#2807), so the status text is the row's reference
            Assert.Equal(Baseline(Named<TextBlock>(window, "WorkflowsTitle"), window), Baseline(TextOf(Named<Button>(window, "WorkflowsBack")), window), 1.0);   // the crumb and the page title share a baseline
            Assert.Equal(title, Baseline(TextOf(Named<Button>(window, "RunNow")), window), 1.0);
            Assert.Equal(title, Baseline(TextOf(Named<Button>(window, "WorkflowEdit")), window), 1.0);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });
}
