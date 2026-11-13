using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>An extra to the geometric bar fact in RowFocusBarTests (#2978): the real pixels of a selected row under the cursor. Real drawing needs AW_UI_FRAMES=1, so otherwise it passes without looking; it is not the red.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowBarPixelTests
{
    private static readonly bool _draws = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);

    [Fact]
    public async Task The_selection_bar_pixel_is_the_bar_colour_not_the_ring_colour() => await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowBarPixelTests).Assembly).Dispatch(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var list = new RowList { ItemsSource = RowGallery.Three(), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1440, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        row.Classes.Add("sel");
        row.Classes.Add("cursor");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var bar = Assert.NotNull(row.Layout.SelBar);
        var at = row.TranslatePoint(bar.Center, window) ?? default;
        var ink = Application.Current!.FindResource("Brush.Primary") is ISolidColorBrush b ? b.Color : default;

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
        bitmap.Render(window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var read = WriteableBitmap.Decode(stream);
        using var frame = read.Lock();
        var bytes = new byte[4];
        Marshal.Copy(frame.Address + ((int)at.Y * frame.RowBytes) + ((int)at.X * 4), bytes, 0, 4);

        Assert.True(bytes[2] == ink.R && bytes[1] == ink.G && bytes[0] == ink.B, string.Create(CultureInfo.InvariantCulture, $"bar pixel is ({bytes[2]};{bytes[1]};{bytes[0]}), the bar is ({ink.R};{ink.G};{ink.B})"));
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);
}
