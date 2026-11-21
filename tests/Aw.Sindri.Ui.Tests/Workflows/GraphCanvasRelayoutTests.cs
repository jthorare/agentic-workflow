using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The canvas wraps its cards only on open, on a size change and on a text-size change (#3684): the zoom buttons never re-wrap, and a size change keeps the selected card in view.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCanvasRelayoutTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCanvasRelayoutTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static Task<WorkflowGraphViewModel> Release()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var ids = Enumerable.Range(0, 16).Select(i => string.Create(inv, $"s{i}")).ToList();
        var nodes = "[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"type\":\"code\",\"title\":\"" + id + "\",\"config\":{}}")) + "]";
        var edges = "[" + string.Join(",", ids.Skip(1).Select((id, i) => "{\"from\":\"" + ids[i] + "\",\"to\":\"" + id + "\"}")) + "]";
        var positions = "[" + string.Join(",", ids.Select((id, i) => "{\"node\":\"" + id + "\",\"x\":" + (i * 280).ToString(inv) + ",\"y\":0}")) + "]";
        return WorkflowGraphViewModelTests.GraphOf(new WorkflowsFixture(), nodes, edges, positions);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Zoom_changes_never_lay_the_cards_out_again()
    {
        var graph = await Release();

        var (before, after) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                var first = graph.RelayoutCount;
                canvas.Zoom *= 1.1;
                canvas.Zoom *= 1.1;
                Settle(window);
                return (first, graph.RelayoutCount);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(before > 0);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task A_size_change_keeps_the_selected_card_in_view()
    {
        var graph = await Release();

        var (rect, size) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                _ = graph.SelectNode("s9");
                window.Width = 1000;
                Settle(window);
                return (canvas.NodeRect("s9")!.Value, canvas.Bounds.Size);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(rect.Left >= 0 && rect.Top >= 0 && rect.Right <= size.Width && rect.Bottom <= size.Height, "the selected card left the canvas after the resize");
    }
}
