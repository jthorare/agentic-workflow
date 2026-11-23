using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The canvas stores its pan already clamped (#4063): whoever writes it, a later zoom has no unclamped value to bring back (#4033).</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCanvasPanClampTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCanvasPanClampTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static Task<WorkflowGraphViewModel> Release() => ChainGraph.Of(16);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
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
}
