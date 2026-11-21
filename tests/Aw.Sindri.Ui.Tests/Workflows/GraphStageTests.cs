using Avalonia.Controls;
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

    private static async Task<WorkflowGraphViewModel> Chain(int count)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var ids = Enumerable.Range(0, count).Select(i => string.Create(inv, $"s{i}")).ToList();
        var nodes = "[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"type\":\"code\",\"title\":\"" + id + "\",\"config\":{}}")) + "]";
        var edges = "[" + string.Join(",", ids.Skip(1).Select((id, i) => "{\"from\":\"" + ids[i] + "\",\"to\":\"" + id + "\"}")) + "]";
        var positions = "[" + string.Join(",", ids.Select((id, i) => "{\"node\":\"" + id + "\",\"x\":" + (i * 280).ToString(inv) + ",\"y\":0}")) + "]";
        return await WorkflowGraphViewModelTests.GraphOf(new WorkflowsFixture(), nodes, edges, positions);
    }

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
                return (atFit, MapAfterZoom: stage.Map.IsVisible, Same: relayouts == graph.RelayoutCount, Outlined: stage.Map.Nodes.Where(n => n.Selected).Count());
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
}
