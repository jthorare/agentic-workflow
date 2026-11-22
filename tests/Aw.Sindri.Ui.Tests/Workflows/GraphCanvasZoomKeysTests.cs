using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The canvas's zoom keys (#3684): <c>+</c> and <c>-</c> step the zoom ladder, <c>0</c> is 100 %, and none of them lays the cards out again.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCanvasZoomKeysTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCanvasZoomKeysTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, keySymbol: null);
        Settle(window);
    }

    [Fact]
    public async Task Plus_minus_and_zero_step_the_zoom_and_never_lay_the_cards_out_again()
    {
        var graph = await ChainGraph.Of(16);   // at 1440 x 728 the 16 steps fit at 100 %

        var (atFit, plus, plusAgain, minus, zero, shiftEquals, sameLayout) = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                _ = canvas.Focus();
                var relayouts = graph.RelayoutCount;
                var fit = canvas.ZoomPercent;
                Press(window, Key.OemPlus, PhysicalKey.Equal);
                var p1 = canvas.ZoomPercent;
                Press(window, Key.Add, PhysicalKey.NumPadAdd);
                var p2 = canvas.ZoomPercent;
                Press(window, Key.OemMinus, PhysicalKey.Minus);
                var m = canvas.ZoomPercent;
                Press(window, Key.D0, PhysicalKey.Digit0);
                var z = canvas.ZoomPercent;
                Press(window, Key.OemPlus, PhysicalKey.Equal, RawInputModifiers.Shift);   // "+" on a US keyboard
                return (fit, p1, p2, m, z, canvas.ZoomPercent, relayouts == graph.RelayoutCount);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal((100, 110, 125, 110, 100, 110), (atFit, plus, plusAgain, minus, zero, shiftEquals));
        Assert.True(sameLayout, "a zoom key laid the cards out again");
    }

    [Fact]
    public async Task Ctrl_with_a_zoom_key_is_not_the_canvass()
    {
        var graph = await ChainGraph.Of(16);

        var handled = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Settle(window);
            try
            {
                return (canvas.ZoomKey(Key.D0, KeyModifiers.Control), canvas.ZoomKey(Key.OemPlus, KeyModifiers.Control), canvas.ZoomKey(Key.OemMinus, KeyModifiers.Shift));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal((false, false, false), handled);   // Ctrl+0 / Ctrl++ belong to the app; Shift+- is "_", not minus
    }
}
