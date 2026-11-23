using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using MapView = Aw.Sindri.Ui.Views.Context.Whole.WholeMapView;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A file row of the map's item list as drawn (#3677, decision 3): the whole path is there, the directory dimmed and the file name in the normal ink, and no row is cut with an ellipsis.</summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapItemPathsViewTests
{
    private const string Doc = "docs/superpowers/specs/2026-11-14-context-page-design-and-the-rest-of-it.md";

    [Fact]
    public async Task A_long_path_is_drawn_whole_with_the_directory_dimmed()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WholeMapItemPathsViewTests).Assembly).Dispatch(() =>
        {
            var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
            var draft = WholeMapLayoutTests.Draft("nodes-small");
            map.Show(draft with { Areas = [draft.Areas[0]], AreaEdges = [], Edges = [], Nodes = [new WholeNode("doc:" + Doc, Doc, "decision", "area:x", [], "trusted", "human", 0.9, Confirmed: true)] });
            var window = new Window { Content = new MapView { DataContext = map }, Width = 1280, Height = 900 };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var list = window.GetVisualDescendants().OfType<ListBox>().First(l => string.Equals(l.Name, "WholeItems", StringComparison.Ordinal));
                var text = list.GetVisualDescendants().OfType<ListBoxItem>().First().GetVisualDescendants().OfType<TextBlock>().First();
                var runs = text.Inlines!.OfType<Run>().ToList();

                Assert.Equal(Doc, string.Concat(runs.Select(r => r.Text)));
                Assert.Equal((TextTrimming.None, TextWrapping.Wrap), (text.TextTrimming, text.TextWrapping));
                Assert.Equal(2, runs.Count);
                Assert.NotEqual(runs[0].Foreground, runs[1].Foreground);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
