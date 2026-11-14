using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Pixels, not layouts (#3189): at a render scaling of 2, a clip pushed and popped in one Render left every later draw of that Render empty, so a flat row showed its glyph, id and title
/// and none of its fact text. This test renders a host at scaling 2 and counts ink in a fact cell. It needs real drawing, which the frames app has only when AW_UI_FRAMES=1, so otherwise it passes without looking.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowDrawsAtScaleTests
{
    private static readonly bool _draws = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);

    [Fact]
    public async Task A_row_draws_the_ink_of_its_fact_text_at_render_scaling_2() => await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowDrawsAtScaleTests).Assembly).Dispatch(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var list = new RowList { ItemsSource = new[] { RowGallery.Group("Needs you", 4) }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1440, Height = 600 };
        Assert.Null(RowFlatRedTests.SetHostScaling(window, 2));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        var cell = row.Layout.Cells.Single(c => string.Equals(c.Key, "ref", StringComparison.Ordinal));
        var at = row.TranslatePoint(new Point(cell.Bounds.X, 0), window) ?? default;

        var ink = InkIn(window, new Rect(at.X * 2, at.Y * 2, cell.Bounds.Width * 2, row.Bounds.Height * 2));

        Assert.True(ink >= 5, string.Create(CultureInfo.InvariantCulture, $"{ink} ink pixels in the ref cell at render scaling 2"));
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);

    // How many pixels of the device-pixel rectangle differ strongly from its top-left pixel (the background).
    private static int InkIn(Window window, Rect device)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(window.ClientSize.Width * 2), (int)(window.ClientSize.Height * 2)), new Vector(192, 192));
        bitmap.Render(window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var read = WriteableBitmap.Decode(stream);
        using var frame = read.Lock();
        var background = Pixel(frame.Address, frame.RowBytes, (int)device.X, (int)device.Y);
        var ink = 0;
        for (var y = (int)device.Y; y < (int)device.Bottom; y++)
        {
            for (var x = (int)device.X; x < (int)device.Right; x++)
            {
                var pixel = Pixel(frame.Address, frame.RowBytes, x, y);
                var difference = Math.Abs(pixel.Item1 - background.Item1) + Math.Abs(pixel.Item2 - background.Item2) + Math.Abs(pixel.Item3 - background.Item3);
                ink += difference > 120 ? 1 : 0;
            }
        }

        return ink;
    }

    private static (int, int, int) Pixel(IntPtr address, int rowBytes, int x, int y)
    {
        var bytes = new byte[4];
        Marshal.Copy(address + (y * rowBytes) + (x * 4), bytes, 0, 4);
        return (bytes[0], bytes[1], bytes[2]);
    }
}
