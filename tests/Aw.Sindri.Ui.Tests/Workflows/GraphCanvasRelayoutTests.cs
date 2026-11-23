using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

        // The wheel step itself (GraphCanvas.Wheel), from a pan the test sets: the canvas's own pan after zooming is not what this fact is about.
        var r = await OnUi(() => WheelSteps(graph));

        Assert.False(r.AtFit, "the canvas took the wheel while the whole graph fits");
        Assert.Equal(-50, r.Mid, 3);
        Assert.True(r.TookDown, $"the canvas left a wheel down to the page while it can scroll down (pan y after each zoom step: {r.Path})");
        Assert.Equal(-98, r.AfterDown, 3);   // one 48 px step down
        Assert.Equal(0, r.AtTop, 3);
        Assert.False(r.TookAtTop, "the canvas took a wheel up at its top edge instead of leaving it to the page");
    }

    [Fact]
    public async Task Fit_after_the_canvas_got_shorter_fits_the_new_height()
    {
        var graph = await Release();

        var (before, after, scrolls) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                var atFit = canvas.ZoomPercent;   // 100 %: the 16 steps are 456 px tall
                canvas.ZoomIn();                  // the person moved the view, so the resize below does not refit by itself
                window.Height = 420;
                Settle(window);
                canvas.FitNow();
                Settle(window);
                return (atFit, canvas.ZoomPercent, canvas.Scrolls);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(100, before);
        Assert.True(after < 100, "Fit kept the zoom it found for the taller canvas");
        Assert.False(scrolls, "after Fit the graph does not fit the shorter canvas");
    }

    // At Fit, then at 200 % from pan y -50: one wheel down, wheel up to the top edge, one more wheel up. Also the pan y after each zoom step, for the failure message.
    private static (bool AtFit, double Mid, bool TookDown, double AfterDown, double AtTop, bool TookAtTop, string Path) WheelSteps(WorkflowGraphViewModel graph)
    {
        var canvas = new GraphCanvas { Graph = graph };
        var window = new Window { Content = canvas, Width = 1440, Height = 728 };
        window.Show();
        Settle(window);
        try
        {
            var down = new Vector(0, -1);
            var up = new Vector(0, 1);
            var atFit = canvas.Wheel(down, KeyModifiers.None, new Point(200, 200));
            var path = new List<double>();
            for (var i = 0; i < 8; i++)
            {
                canvas.ZoomIn();   // to 200 %: the chain is 432 px tall, 864 at 200 %, over a 728 px canvas (under twice, so no map and no map room: #4055)
                path.Add(canvas.Pan.Y);
            }

            Settle(window);
            canvas.Pan = new Point(canvas.Pan.X, -50);   // inside [-136, 0] (and inside the old [-280, 0])
            var mid = canvas.Pan.Y;
            var tookDown = canvas.Wheel(down, KeyModifiers.None, new Point(200, 200));
            var afterDown = canvas.Pan.Y;
            for (var i = 0; i < 10 && canvas.Pan.Y < 0; i++)
            {
                _ = canvas.Wheel(up, KeyModifiers.None, new Point(200, 200));
            }

            var atTop = canvas.Pan.Y;
            var tookAtTop = canvas.Wheel(up, KeyModifiers.None, new Point(200, 200));
            return (atFit, mid, tookDown, afterDown, atTop, tookAtTop, Path: string.Join(", ", path.Select(y => y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))));
        }
        finally
        {
            window.Close();
        }
    }
}
