using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A run row in a workflow's runs card (#2700) has the workflow list row's vertical padding: its title starts as far below the row's top as a list row's title does, for a failed and a succeeded run, at text scale 1.0 and 1.3.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RunRowPaddingTests : FrameHost
{
    // why: the title text sits in its row; its top is the distance from the row's top
    private static double TitleTop(Border row) =>
        row.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Classes.Contains("body-strong")).TranslatePoint(default, row)!.Value.Y;

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task A_run_rows_title_sits_as_far_below_its_top_as_a_list_rows_does_at_scale(double scale) => OnUi(async () =>
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
            Settle(window);
            var listRow = Named<ItemsControl>(window, "WorkflowRows").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("item-row") && b.IsEffectivelyVisible);
            var expected = TitleTop(listRow);

            await shell.Workflows.OpenWorkflow(Slug, TestContext.Current.CancellationToken);
            await Until(window, () => shell.Workflows.Runs.Rows.Count >= 2, "the runs never showed");
            Settle(window);

            var runRows = Named<ItemsControl>(window, "RunRows").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("item-row") && b.IsEffectivelyVisible).ToList();
            Assert.True(runRows.Count >= 2, "a failed and a succeeded run show");
            Assert.All(runRows, row => Assert.Equal(expected, TitleTop(row), 1.0));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });
}
