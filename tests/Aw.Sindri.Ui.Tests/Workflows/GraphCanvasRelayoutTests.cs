using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private static Task<WorkflowGraphViewModel> Release() => ChainGraph.Of(16);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();   // the canvas is hit only where it has drawn (its transparent back), and headless draws only on a render tick
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

    [Fact]
    public async Task The_wheel_goes_to_the_page_when_the_canvas_fits_and_to_the_canvas_when_it_scrolls()
    {
        var graph = await Release();

        var (fits, scrolls) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            var handled = new List<bool>();
            window.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) => handled.Add(e.Handled), RoutingStrategies.Bubble, handledEventsToo: true);
            window.Show();
            Settle(window);
            try
            {
                window.MouseWheel(new Point(200, 200), new Vector(0, -1));
                var atFit = handled[^1];
                for (var i = 0; i < 8; i++)
                {
                    canvas.ZoomIn();   // to 200 %, where the chain overflows the canvas both ways
                }

                Settle(window);
                window.MouseWheel(new Point(200, 200), new Vector(0, -1));
                return (atFit, handled[^1]);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(fits, "the canvas took the wheel while the whole graph fits");
        Assert.True(scrolls, "the canvas left the wheel to the page while it can scroll");
    }
}
