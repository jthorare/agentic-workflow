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
    public async Task Zoom_strip_is_a_band_below_the_clip_and_the_map_shows_only_when_the_canvas_scrolls()
    {
        var graph = await Chain(16);

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
                stage.Canvas.ZoomIn();
                stage.Canvas.ZoomIn();
                stage.Canvas.ZoomIn();
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
    public async Task The_minimap_is_an_opaque_panel_that_shows_only_while_the_graph_does_not_fit()
    {
        var graph = await Chain(16);

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
                stage.Canvas.ZoomIn();
                stage.Canvas.ZoomIn();
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
        Assert.False(r.AtFit, "the minimap panel shows while the whole graph fits");
        Assert.True(r.Zoomed, "the minimap panel is hidden while the canvas scrolls");
        Assert.Equal(255, r.Alpha);   // opaque: nothing shows through
        Assert.Equal((1d, 8d), (r.Border, r.Padding));
    }
}
