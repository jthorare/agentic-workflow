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

    [Fact]
    public async Task Five_zoom_steps_in_keep_the_centre_and_five_out_come_back_to_the_top()
    {
        var graph = await Release();

        // 1440 x 728, Fit 100 %, pan y 0, cards to y 432 and x 1360. At 200 % that is 2720 x 864, under twice the canvas, so no map and no map room (#4055): y pans only
        // past 728, in [728 - 432 z, 0]. Keeping the centre (y 364) gives pan y 0, 0, 0 (648 fits at 150 %), -28 (the 175 % floor), -84; the clamp's stale value once gave -280 (#4033).
        var (zoomedIn, inPath, zoomedOut) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                var path = new List<double>();
                for (var i = 0; i < 5; i++)
                {
                    canvas.ZoomIn();
                    path.Add(canvas.Pan.Y);
                }

                var after = canvas.Pan.Y;
                for (var i = 0; i < 5; i++)
                {
                    canvas.ZoomOut();
                }

                return (after, string.Join(", ", path.Select(y => y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))), canvas.Pan.Y);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(Math.Abs(zoomedIn - -84) <= 0.5, $"after five zoom steps in, pan y went {inPath}, not 0, 0, 0, -28, -84");
        Assert.Equal(0, zoomedOut, 3);
    }

    // #4033 (qa on #4061): at 125 % with the view at the bottom right limit, one step in and one step out must return to the start pan. StepTo set the
    // zoom first, so the zoom handler clamped the pan to the smaller extent and the centre formula read the clamped pan. Out from 150 % to 125 %, with the
    // limit start p0 = (cw - 1.25 R, ch - 1.25 H - 144): in gives c - (c - p0) * 1.2, still inside the 150 % extent (0.1 cw and 0.1 ch - 28.8 to spare),
    // but outside the 125 % extent, so the old order clamped it back to p0 and then moved again. Window 300 x 400, not 600 x 400: the canvas wraps the 16 cards to its width, and at 600 px the world may be a single
    // 290 px column (1.25 x 290 = 362 < 600: no x scroll, the guard failed there). Every card is at least 290 px wide (ChainGraph), so R >= 290 and
    // 1.25 R >= 362 > 300: x always scrolls; 16 cards are far taller than 205 px, so 1.25 H + 144 > 400 and y scrolls. With R = 290 and H = 432 (a
    // guess for H, from the five-steps fact's 'cards to y 432'): p0 = (-62.5, -284), in = (-87.5, -380.8), old out x = 300/2 - (150 + 62.5) * 0.8333 = -27.1
    // (35 px off), old out y = -203.3 (80 px off), new out = p0 exactly. Both axes fail under the old order when R > 0.4 cw = 120 and H > 45. The limit itself can return: the 150 % extent is wider on both axes, so the step in never clamps.
    // #4055: at 300 wide the 16 cards wrap to one column (Fit stops at the 86 % floor): about 327 x 1828 in the world, so at 125 % and 150 % the world is over
    // twice the 400 px canvas on height, the map shows and its 144 px of room is part of p0, as above. The limit comes from GraphZoom.ClampPan either way.
    [Fact]
    public async Task One_zoom_step_in_and_one_out_at_125_percent_return_to_the_start_pan_at_the_bottom_right_limit()
    {
        var graph = await Release();

        var (start, back, scrolls) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 300, Height = 400 };
            window.Show();
            Settle(window);
            try
            {
                canvas.Zoom = 1.25;
                Settle(window);
                var world = canvas.World;
                var (limitX, limitY) = GraphZoom.ClampPan((-1e6, -1e6), (world.Right, world.Bottom), 1.25, (canvas.Bounds.Width, canvas.Bounds.Height));
                canvas.Pan = new Point(limitX, limitY);
                var from = canvas.Pan;
                canvas.ZoomIn();
                canvas.ZoomOut();
                return (from, canvas.Pan, from.X < -1 && from.Y < -1);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(scrolls, "the test needs a graph that scrolls on both axes at 125 % in 300 x 400");
        Assert.True(
            Math.Abs(back.X - start.X) <= 0.5 && Math.Abs(back.Y - start.Y) <= 0.5,
            "the pan started at " + start.X.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ", " + start.Y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            + " and came back at " + back.X.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ", " + back.Y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }

    // #4063: a pan set past the limit is stored clamped, so a later zoom that widens the range does not bring the old value back (the #4033 drift).
    // 16 steps at 1440 x 728: at 175 % y may go to 728 - 1.75 x 432 = -28; at 200 % to -136. Set to -100000 at 175 %, the pan shows -28 and must stay
    // -28 at 200 %, which is inside that range; a stored -100000 would come back as -136.
    [Fact]
    public async Task A_pan_set_past_the_limit_is_stored_clamped_so_a_wider_zoom_keeps_it()
    {
        var graph = await Release();

        var (shown, afterZoom) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                canvas.Zoom = 1.75;
                canvas.Pan = new Point(0, -100000);
                var before = canvas.Pan.Y;
                canvas.Zoom = 2;
                return (before, canvas.Pan.Y);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(shown, afterZoom, 3);
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
