using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The System headline and the stale line as drawn (#2842): cards have no border, so a toned box is a tinted fill with a 1 px
/// edge in its tone. An amber or red headline must not draw like a healthy one. Frames are saved as gh2842-headline-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SystemHeadlineToneTests : FrameHost
{
    private static readonly TokenFile _tokens = TokenFile.Load();

    [Fact]
    public Task A_healthy_headline_is_the_plain_card_surface() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var box = await OnSystem(window, shell, "Headline", "ok");

        Assert.True(box.Frame.Near(box.InsideX, box.InsideY, Tone("Surface1")), box.Frame.Describe(box.InsideX, box.InsideY));
        Assert.False(box.Frame.Near(box.InsideX, box.InsideY, Tone("AttentionSoft"), tolerance: 2), "a healthy headline is drawn amber");
        GlyphSits24In(window);
    });

    [Fact]
    public Task A_degraded_headline_is_an_amber_fill_with_an_amber_edge() => OnUi(async () =>
    {
        Server.Current = A.Status() with { Health = new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "service heartbeat failed")]) };
        var (window, shell) = await Connected(s => s.Health.ToneWarn);
        var box = await OnSystem(window, shell, "Headline", "warn");

        Assert.True(box.Frame.Near(box.InsideX, box.InsideY, Tone("AttentionSoft")), box.Frame.Describe(box.InsideX, box.InsideY));
        Assert.True(box.Frame.Near(box.EdgeX, box.EdgeY, Tone("Attention"), tolerance: 24), box.Frame.Describe(box.EdgeX, box.EdgeY));
        GlyphSits24In(window);
    });

    [Fact]
    public Task A_stalled_headline_is_a_red_fill_with_a_red_edge() => OnUi(async () =>
    {
        Server.Current = A.Status() with { Health = new HealthView("stalled", [new HealthProblemView("SND-VIEW-001", "views are stalled")]) };
        var (window, shell) = await Connected(s => s.Health.ToneError);
        var box = await OnSystem(window, shell, "Headline", "bad");

        Assert.True(box.Frame.Near(box.InsideX, box.InsideY, Tone("ErrorSoft")), box.Frame.Describe(box.InsideX, box.InsideY));
        Assert.True(box.Frame.Near(box.EdgeX, box.EdgeY, Tone("Error"), tolerance: 24), box.Frame.Describe(box.EdgeX, box.EdgeY));
        GlyphSits24In(window);
    });

    [Fact]
    public Task The_stale_line_is_amber_like_a_degraded_headline() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Server.FailPing = true;
        Clock.Advance(ShellViewModel.StaleAfter);
        await Until(window, () => shell.State == ShellState.Stale && shell.Health.ShowStaleLine, "the shell never went stale");
        var box = await OnSystem(window, shell, "StaleLineBox", "stale");

        Assert.True(box.Frame.Near(box.InsideX, box.InsideY, Tone("AttentionSoft")), box.Frame.Describe(box.InsideX, box.InsideY));
        Assert.True(box.Frame.Near(box.EdgeX, box.EdgeY, Tone("Attention"), tolerance: 24), box.Frame.Describe(box.EdgeX, box.EdgeY));
    });

    // The headline's text lines up with every card under it: Space.LG in, toned or not (the 1 px edge comes out of the padding).
    private static void GlyphSits24In(Window window)
    {
        var at = Named<Control>(window, "HeadlineGlyph").TranslatePoint(default, Named<Border>(window, "Headline"))!.Value;
        Assert.True(Math.Abs(at.X - 24) < 0.5, string.Create(CultureInfo.InvariantCulture, $"the headline glyph starts {at.X} px in, not 24"));
    }

    private static (byte R, byte G, byte B) Tone(string name)
    {
        var hex = _tokens.Color(name)[^6..];
        return (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..], 16));
    }

    // Opens System, saves the frame and finds the box: a point 6 px inside its top-left padding, and the middle of its top edge.
    private static async Task<Box> OnSystem(Window window, ShellViewModel shell, string name, string frame)
    {
        shell.GoTo(PageId.System);
        await Until(window, () => Visible(window, name), name + " never showed");
        Settle(window);
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame: this project draws with Skia");
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        bitmap.Save(Path.Combine(dir, "gh2842-headline-" + frame + ".png"), new PngBitmapEncoderOptions());
        var border = Named<Border>(window, name);
        var at = border.TranslatePoint(default, window) ?? throw new InvalidOperationException(name + " is not in the window");
        var (x, y) = ((int)Math.Round(at.X, MidpointRounding.AwayFromZero), (int)Math.Round(at.Y, MidpointRounding.AwayFromZero));
        return new Box(Pixels.Of(bitmap), x + 6, y + 6, x + ((int)border.Bounds.Width / 2), y);
    }

    private sealed record Box(Pixels Frame, int InsideX, int InsideY, int EdgeX, int EdgeY);

    private sealed class Pixels(byte[] bytes, int width, int height, bool rgba, double scale)
    {
        public static Pixels Of(Bitmap bitmap)
        {
            var size = bitmap.PixelSize;
            var stride = size.Width * 4;
            var buffer = Marshal.AllocHGlobal(stride * size.Height);
            try
            {
                bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
                var bytes = new byte[stride * size.Height];
                Marshal.Copy(buffer, bytes, 0, bytes.Length);
                return new Pixels(bytes, size.Width, size.Height, bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888, size.Width / bitmap.Size.Width);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public string Describe(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"({x}, {y}) is {At(x, y)}");

        public bool Near(int x, int y, (byte R, byte G, byte B) want, int tolerance = 8)
        {
            var (r, g, b) = At(x, y);
            return Math.Abs(r - want.R) <= tolerance && Math.Abs(g - want.G) <= tolerance && Math.Abs(b - want.B) <= tolerance;
        }

        private (byte R, byte G, byte B) At(int x, int y)
        {
            var (px, py) = ((int)(x * scale), (int)(y * scale));
            if (px < 0 || py < 0 || px >= width || py >= height)
            {
                return default;
            }

            var i = ((py * width) + px) * 4;
            return rgba ? (bytes[i], bytes[i + 1], bytes[i + 2]) : (bytes[i + 2], bytes[i + 1], bytes[i]);
        }
    }
}
