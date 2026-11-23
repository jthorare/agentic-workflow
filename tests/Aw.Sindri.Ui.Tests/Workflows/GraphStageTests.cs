using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The workflow canvas's stage (#3684): the zoom control is a 56 px band under the canvas, and the minimap shows only while the canvas scrolls.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphStageTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphStageTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static Task<WorkflowGraphViewModel> Chain(int count) => ChainGraph.Of(count);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Zoom_strip_is_a_band_below_the_clip_and_the_map_shows_only_when_the_graph_is_over_twice_the_canvas()
    {
        var graph = await Chain(40);   // Fit 86 %: cards to y 1132, 973 px on a 728 px canvas (under twice: no map); at 200 % 2264 px (over twice: map)

        var result = await OnUi(() =>
        {
            var stage = new GraphStage { Graph = graph };
            var window = new Window { Content = stage, Width = 1440, Height = 784 };   // 784 = 728 clip + the 56 px band
            window.Show();
            Settle(window);
            try
            {
                var relayouts = graph.RelayoutCount;
                var atFit = (Strip: stage.Strip.Bounds.Height, Canvas: stage.Canvas.Bounds.Height, MapShown: stage.Map.IsVisible);
                for (var i = 0; i < 8; i++)
                {
                    stage.Canvas.ZoomIn();
                }

                Settle(window);
                _ = graph.SelectNode("s3");
                Settle(window);
                return (atFit, MapAfterZoom: stage.Map.IsVisible, Same: relayouts == graph.RelayoutCount, Outlined: stage.Map.Nodes.Count(n => n.Selected));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(56, result.atFit.Strip);
        Assert.Equal(728, result.atFit.Canvas);
        Assert.False(result.atFit.MapShown);
        Assert.True(result.MapAfterZoom);
        Assert.True(result.Same);
        Assert.Equal(1, result.Outlined);   // the minimap outlines the selected card
    }

    [Fact]
    public async Task The_minimap_is_an_opaque_panel_that_shows_only_while_the_graph_is_over_twice_the_canvas()
    {
        var graph = await Chain(40);

        var r = await OnUi(() =>
        {
            var stage = new GraphStage { Graph = graph };
            var window = new Window { Content = stage, Width = 1440, Height = 784 };
            window.Show();
            Settle(window);
            try
            {
                var panel = stage.Map.GetVisualParent() as Border;   // the card the map sits on (#4055)
                var atFit = panel?.IsVisible;
                for (var i = 0; i < 8; i++)
                {
                    stage.Canvas.ZoomIn();   // to 200 %: over twice the canvas, so the map shows
                }

                Settle(window);
                var alpha = panel?.Background is ISolidColorBrush solid ? solid.Color.A : -1;
                return (IsPanel: panel is not null, AtFit: atFit, Zoomed: panel?.IsVisible, Alpha: alpha, Border: panel?.BorderThickness.Left ?? -1, Padding: panel?.Padding.Left ?? -1);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(r.IsPanel, "the minimap sits straight on the canvas, so the cards under it show through (#4055)");
        Assert.False(r.AtFit, "the minimap panel shows while the graph is under twice the canvas");
        Assert.True(r.Zoomed, "the minimap panel is hidden while the graph is over twice the canvas");
        Assert.Equal(255, r.Alpha);   // opaque: nothing shows through
        Assert.Equal((1d, 8d), (r.Border, r.Padding));
    }

    [Fact]
    public async Task At_1000_by_800_sixteen_steps_that_scroll_a_little_show_no_minimap()
    {
        var graph = await Chain(16);

        var r = await OnUi(() =>
        {
            var stage = new GraphStage { Graph = graph };
            var window = new Window { Content = stage, Width = 1000, Height = 800 };   // the canvas is 1000 x 744
            window.Show();
            Settle(window);
            try
            {
                stage.Canvas.ZoomIn();
                stage.Canvas.ZoomIn();   // the cards now run a little past the canvas's right edge (#4055 at rest)
                Settle(window);
                return (stage.Canvas.Scrolls, Map: stage.Map.IsVisible, Panel: stage.MapPanel.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(r.Scrolls, "the fact needs a canvas that scrolls");
        Assert.False(r.Map, "a map shows for a graph that scrolls only a little");
        Assert.False(r.Panel, "the map's panel shows for a graph that scrolls only a little");
    }

    [Fact]
    public async Task At_1000_wide_forty_steps_with_the_map_keep_every_card_clear_of_its_panel_at_rest_and_at_125_percent()
    {
        var graph = await Chain(40);   // 1000 x 520 canvas, like the app at 1000 x 800: the 40 steps are over twice its height, so the map shows (#4149)

        var (atRest, at125) = await OnUi(() =>
        {
            var stage = new GraphStage { Graph = graph };
            var window = new Window { Content = stage, Width = 1000, Height = 576 };   // 576 = 520 canvas + the 56 px band
            window.Show();
            Settle(window);
            try
            {
                var rest = Covered(stage, graph);
                for (var i = 0; i < 4; i++)
                {
                    stage.Canvas.ZoomIn();   // 86 -> 90 -> 100 -> 110 -> 125 %
                }

                Settle(window);
                return (rest, Covered(stage, graph));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(atRest.Shown, "the fact needs the map shown at Fit");
        Assert.True(atRest.Under.Length == 0, "at Fit the map panel covers " + string.Join(", ", atRest.Under));
        Assert.True(at125.Shown, "the fact needs the map shown at 125 %");
        Assert.True(at125.Under.Length == 0, "at 125 % the map panel covers " + string.Join(", ", at125.Under));
    }

    // Whether the map shows, and the cards whose rectangle meets the map panel's, both in the canvas's coordinates.
    private static (bool Shown, string[] Under) Covered(GraphStage stage, WorkflowGraphViewModel graph)
    {
        var at = stage.MapPanel.TranslatePoint(default, stage.Canvas) ?? default;
        var panel = new Rect(at, stage.MapPanel.Bounds.Size);
        return (stage.MapPanel.IsVisible, [.. graph.Nodes.Where(n => stage.Canvas.NodeRect(n.Id) is { } card && card.Intersects(panel)).Select(n => n.Id)]);
    }
}
