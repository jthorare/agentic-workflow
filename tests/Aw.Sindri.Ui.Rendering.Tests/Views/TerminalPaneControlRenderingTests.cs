using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>The terminal pane control paints the grid it is given (#3383): a cell's background lands in its own cell box, and the control reports how many columns and rows fit.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class TerminalPaneControlRenderingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<bool> OnUi(Action body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalPaneControlRenderingTests).Assembly).Dispatch(() =>
        {
            body();
            return true;
        }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (byte R, byte G, byte B) Pixel(Bitmap bitmap, int x, int y)
    {
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), buffer, 4, 4);
            var bytes = new byte[4];
            Marshal.Copy(buffer, bytes, 0, 4);
            return bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888 ? (bytes[0], bytes[1], bytes[2]) : (bytes[2], bytes[1], bytes[0]);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public Task A_red_background_cell_paints_red_inside_its_box_and_not_in_the_blank_row_below() => OnUi(() =>
    {
        const string Esc = "\u001b";
        var grid = TerminalFrameParser.Parse($"{Esc}[41m  {Esc}[0m\n\n", "0 0 8 2 0 0 0 \n");
        var pane = new TerminalPaneControl { Width = 200, Height = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var fits = new List<(int, int)>();
        pane.FitChanged += (_, e) => fits.Add((e.Columns, e.Rows));
        var window = new Window { Width = 200, Height = 100, Content = pane };
        window.Show();
        Settle(window);

        pane.Draw(grid);
        Settle(window);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame: this project draws with Skia");

        var cell = pane.CellSize;
        Assert.True(cell.Width > 0 && cell.Height > 0);
        var (ir, ig, ib) = Pixel(frame, (int)(cell.Width * 0.5), (int)(cell.Height * 0.5));
        var (br, bg, bb) = Pixel(frame, (int)(cell.Width * 0.5), (int)(cell.Height * 1.5));
        Assert.Equal((0xCD, 0x31, 0x31), (ir, ig, ib));
        Assert.NotEqual((0xCD, 0x31, 0x31), (br, bg, bb));
        Assert.Contains(((int)(200 / cell.Width), (int)(100 / cell.Height)), fits);
    });

    [Fact]
    public Task Painting_the_same_frame_again_makes_no_new_brush_and_a_dim_cell_is_fainter_than_a_plain_one() => OnUi(() =>
    {
        const string Esc = "\u001b";
        var grid = TerminalFrameParser.Parse($"{Esc}[38;2;255;255;255m\u2588{Esc}[2m\u2588{Esc}[0m\n\n", "0 0 8 2 0 0 0 \n");
        var pane = new TerminalPaneControl { Width = 200, Height = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = new Window { Width = 200, Height = 100, Content = pane };
        window.Show();
        Settle(window);

        pane.Draw(grid);
        Settle(window);
        window.CaptureRenderedFrame()?.Dispose();
        var made = pane.BrushesCreated;
        pane.Draw(grid);
        Settle(window);
        using var second = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame: this project draws with Skia");

        var cell = pane.CellSize;
        var (pr, _, _) = Pixel(second, (int)(cell.Width * 0.5), (int)(cell.Height * 0.5));
        var (dr, _, _) = Pixel(second, (int)(cell.Width * 1.5), (int)(cell.Height * 0.5));
        Assert.True(made > 0);
        Assert.Equal(made, pane.BrushesCreated);
        Assert.True(dr < pr, "the dim block is not fainter than the plain one");
    });
}
