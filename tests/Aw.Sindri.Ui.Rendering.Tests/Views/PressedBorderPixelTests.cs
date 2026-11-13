using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Frames.Tests.Views;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The pressed border must reach 3:1 as drawn at 1x, not only as a token (#3190, WCAG 1.4.11). The 0.98 press scale put the 1 px border on a fractional pixel, and
/// antialiasing blended it with the fill (2.81:1 on a clickable, 2.53:1 on the Home needs-you primary). Each test presses for real, renders the window at scale 1
/// and takes the best border pixel around the control's left edge: its contrast on the surface outside and on the fill inside, whichever is lower.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PressedBorderPixelTests : FrameHost
{
    private const double Required = 3.0;

    private static double Luminance(byte r, byte g, byte b)
    {
        static double Linear(byte v) => v / 255.0 is var s && s <= 0.04045 ? s / 12.92 : Math.Pow(((v / 255.0) + 0.055) / 1.055, 2.4);
        return (0.2126 * Linear(r)) + (0.7152 * Linear(g)) + (0.0722 * Linear(b));
    }

    private static double Contrast((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        var (x, y) = (Luminance(a.R, a.G, a.B), Luminance(b.R, b.G, b.B));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static Func<int, int, (byte R, byte G, byte B)> Render(Window window)
    {
        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(window);
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            var rgba = bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888;
            return (x, y) =>
            {
                var at = (y * stride) + (x * 4);
                return rgba ? (bytes[at], bytes[at + 1], bytes[at + 2]) : (bytes[at + 2], bytes[at + 1], bytes[at]);
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // The press scale moves the edge by a few pixels, so look at every pixel within 6 px of the unscaled left edge, mid-height.
    private static (double Best, string Detail) BorderContrast(Window window, Control control)
    {
        var pixel = Render(window);
        var edge = control.TranslatePoint(new Point(0, control.Bounds.Height / 2), window)!.Value;
        var (x0, y) = ((int)Math.Round(edge.X, MidpointRounding.AwayFromZero), (int)Math.Round(edge.Y, MidpointRounding.AwayFromZero));
        var outside = pixel(x0 - 9, y);
        var inside = pixel(x0 + 9, y);
        var best = 0.0;
        var detail = "";
        for (var x = x0 - 6; x <= x0 + 6; x++)
        {
            var p = pixel(x, y);
            var ratio = Math.Min(Contrast(p, outside), Contrast(p, inside));
            if (ratio > best)
            {
                (best, detail) = (ratio, string.Create(CultureInfo.InvariantCulture, $"x={x} #{p.R:X2}{p.G:X2}{p.B:X2} outside #{outside.R:X2}{outside.G:X2}{outside.B:X2} inside #{inside.R:X2}{inside.G:X2}{inside.B:X2}"));
            }
        }

        return (best, detail);
    }

    // The bottom border row at the control's laid-out bottom edge (bounds, not the drawn one: a press that moves the control must not move the row that is read), mid-width.
    private static (double Ratio, string Detail) BottomBorderContrast(Window window, Control control)
    {
        var pixel = Render(window);
        var edge = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height), window)!.Value;
        var (x, y) = ((int)Math.Round(edge.X, MidpointRounding.AwayFromZero), (int)Math.Round(edge.Y, MidpointRounding.AwayFromZero) - 1);
        var (outside, inside, p) = (pixel(x, y + 5), pixel(x, y - 5), pixel(x, y));
        var ratio = Math.Min(Contrast(p, outside), Contrast(p, inside));
        return (ratio, string.Create(CultureInfo.InvariantCulture, $"y={y} #{p.R:X2}{p.G:X2}{p.B:X2} outside #{outside.R:X2}{outside.G:X2}{outside.B:X2} inside #{inside.R:X2}{inside.G:X2}{inside.B:X2}"));
    }

    private static Point Centre(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? default;

    [Fact]
    public Task A_pressed_clickable_draws_its_border_at_3_to_1_at_scale_1() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        // 150 wide: a 0.98 scale about the centre moves the left edge 1.5 px, half a pixel, the case that blurred the border in the app.
        // A narrower sample (about 110 px with its text) moved it 1.1 px, close to a whole pixel, and passed on the scaled code (#3501 red).
        var sample = new Border { Width = 150, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(24, 12), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "Clickable" } };
        sample.Classes.Add("clickable");
        var app = Application.Current!;
        var host = new Border
        {
            Padding = new Thickness(24),
            Child = sample,
            Background = app.TryFindResource("Brush.Canvas", app.ActualThemeVariant, out var canvas) ? canvas as IBrush : null,
        };
        var window = new Window { Width = 360, Height = 140, Content = host };
        window.Show();
        Settle(window);

        window.MouseMove(Centre(sample, window), RawInputModifiers.None);
        window.MouseDown(Centre(sample, window), MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", sample.Classes, StringComparer.Ordinal);
        var (ratio, detail) = BorderContrast(window, sample);
        Assert.True(ratio >= Required, string.Create(CultureInfo.InvariantCulture, $"the pressed border draws at {ratio:0.00}:1, below {Required}:1 ({detail})"));
        window.MouseUp(Centre(sample, window), MouseButton.Left, RawInputModifiers.None);
        window.Close();
        await Task.CompletedTask;
    });

    [Fact]
    public Task The_Home_needs_you_primary_button_pressed_draws_its_border_at_3_to_1_at_scale_1() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, _) = await Connected(s => s.NeedsYou.Count > 0);
        var row = window.GetVisualDescendants().OfType<Border>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("needs-you-row") && !b.Classes.Contains("lead")
            && b.GetVisualDescendants().OfType<Button>().Any(x => x.Classes.Contains("primary") && x.IsEffectivelyVisible));
        var button = row.GetVisualDescendants().OfType<Button>().First(x => x.Classes.Contains("primary") && x.IsEffectivelyVisible);

        window.MouseMove(Centre(button, window), RawInputModifiers.None);
        window.MouseDown(Centre(button, window), MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", button.Classes, StringComparer.Ordinal);
        var (ratio, detail) = BorderContrast(window, button);
        Assert.True(ratio >= Required, string.Create(CultureInfo.InvariantCulture, $"the pressed primary button draws its border at {ratio:0.00}:1, below {Required}:1 ({detail})"));
        window.MouseUp(Centre(button, window), MouseButton.Left, RawInputModifiers.None);
    });

    [Fact]
    public Task The_Home_needs_you_primary_button_pressed_draws_its_bottom_border_inside_the_page_clip() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, _) = await Connected(s => s.NeedsYou.Count > 0);
        var row = window.GetVisualDescendants().OfType<Border>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("needs-you-row") && !b.Classes.Contains("lead")
            && b.GetVisualDescendants().OfType<Button>().Any(x => x.Classes.Contains("primary") && x.IsEffectivelyVisible));
        var button = row.GetVisualDescendants().OfType<Button>().First(x => x.Classes.Contains("primary") && x.IsEffectivelyVisible);

        window.MouseMove(Centre(button, window), RawInputModifiers.None);
        window.MouseDown(Centre(button, window), MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", button.Classes, StringComparer.Ordinal);
        var (ratio, detail) = BottomBorderContrast(window, button);
        Assert.True(ratio >= Required, string.Create(CultureInfo.InvariantCulture, $"the pressed primary button draws its bottom border at {ratio:0.00}:1, below {Required}:1 ({detail})"));
        window.MouseUp(Centre(button, window), MouseButton.Left, RawInputModifiers.None);
    });

    [Fact]
    public Task A_pressed_clickable_in_a_host_that_clips_at_its_bottom_edge_draws_its_bottom_border() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var sample = new Border { Width = 150, Height = 44, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(24, 12), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "Clickable" } };
        sample.Classes.Add("clickable");
        var clip = new Panel { Height = 44, ClipToBounds = true, Children = { sample } };
        var app = Application.Current!;
        var host = new Border
        {
            Padding = new Thickness(24),
            Child = clip,
            Background = app.TryFindResource("Brush.Canvas", app.ActualThemeVariant, out var canvas) ? canvas as IBrush : null,
        };
        var window = new Window { Width = 360, Height = 140, Content = host };
        window.Show();
        Settle(window);

        window.MouseMove(Centre(sample, window), RawInputModifiers.None);
        window.MouseDown(Centre(sample, window), MouseButton.Left, RawInputModifiers.None);
        Settle(window);

        Assert.Contains(":pressed", sample.Classes, StringComparer.Ordinal);
        var (ratio, detail) = BottomBorderContrast(window, sample);
        Assert.True(ratio >= Required, string.Create(CultureInfo.InvariantCulture, $"the pressed clickable draws its bottom border at {ratio:0.00}:1, below {Required}:1 ({detail})"));
        window.MouseUp(Centre(sample, window), MouseButton.Left, RawInputModifiers.None);
        window.Close();
        await Task.CompletedTask;
    });
}
