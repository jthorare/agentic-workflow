using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's Needs you and Queued rows lead with the title; the DEC-/ITM- id stays, after the title (#2782), and the decision's status line is in plain words.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeRowTitleFirstTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The visible text of a row in reading order, the state chip's word left out.
    private static Task<(string Text, double X)[]> VisibleText(UserControl row, object model) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeRowTitleFirstTests).Assembly).Dispatch(() =>
        {
            row.DataContext = model;
            var window = new Window { Width = 1000, Content = row };
            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                return window.GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text) && !t.GetVisualAncestors().OfType<StateChipView>().Any())
                    .Select(t => (t.Text!, t.TranslatePoint(default, window)?.X ?? 0)).ToArray();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }, Token);

    private static NeedsYouRowViewModel DecisionRow()
    {
        var list = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", "one-request")), SindriApiClient.Json).Value;
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
        var entry = NeedsYouMapper.Map(list, catalog, decisions: null, new HomeCounts(Running: 0, WithYou: 1, Queued: 0)).Rows[0];
        return new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));
    }

    [Fact]
    public async Task A_Needs_you_row_shows_its_title_first_and_the_id_after()
    {
        var model = DecisionRow();

        var text = await VisibleText(new NeedsYouRow(), model);

        var title = text.Single(t => t.Text == model.Title);
        var id = text.Single(t => t.Text == model.ShownId);
        Assert.Equal(title, text[0]);
        Assert.True(title.X < id.X, $"the title (x={title.X}) should sit left of the id (x={id.X})");
    }

    [Fact]
    public async Task A_Queued_row_shows_its_title_first_and_the_id_after()
    {
        var model = new ItemRowViewModel(new ItemSummaryView("ITM-01JN2", "Try the queue", Repo: null, "running", Step: null, A.Noon.AddMinutes(-12), PrimaryAction: null, Actions: []), "running", primaryAction: null, A.Noon);

        var text = await VisibleText(new CompactItemRow(), model);

        var title = text.Single(t => t.Text == "Try the queue");
        var id = text.Single(t => t.Text == "ITM-01JN2");
        Assert.Equal(title, text[0]);
        Assert.True(title.X < id.X, $"the title (x={title.X}) should sit left of the id (x={id.X})");
    }

    [Fact]
    public void The_decision_status_line_does_not_name_the_workflow_engine()
    {
        var model = DecisionRow();

        Assert.True(model.HasNotice);
        Assert.DoesNotContain("workflow engine", model.Notice, StringComparison.OrdinalIgnoreCase);
    }
}
