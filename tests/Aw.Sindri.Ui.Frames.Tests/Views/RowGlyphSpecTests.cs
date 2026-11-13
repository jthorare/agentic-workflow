using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The row glyphs against grammar.css .g.* (#3537, #2978): each glyph is drawn at 4x in its 16 px box, and its pixel bounding box and key colours are read back. Real drawing needs AW_UI_FRAMES=1, as in RowBarPixelTests.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowGlyphSpecTests
{
    private const int Scale = 4;
    private const int Tolerance = 8;
    private static readonly bool _draws = string.Equals(Environment.GetEnvironmentVariable("AW_UI_FRAMES"), "1", StringComparison.Ordinal);

    [Fact]
    public Task Mention_is_an_8_px_disc_in_primary_hover() => Check(RowGlyph.Mention, 8, 8, "Brush.PrimaryHover", (g, shot) => Assert.True(shot.Near(8, 8, g.Colour("Brush.PrimaryHover")), shot.Describe(8, 8)));

    [Fact]
    public Task Failed_is_a_14_px_error_disc_with_a_canvas_x() => Check(RowGlyph.Failed, 14, 14, "Brush.Error", (g, shot) =>
    {
        Assert.True(shot.AllOpaqueOnBlend(g.Colour("Brush.Error"), g.Colour("Brush.Canvas")), "Failed: a solid pixel is off the Brush.Error to Brush.Canvas blend");
        Assert.True(shot.Near(8, 3, g.Colour("Brush.Error")), shot.Describe(8, 3));
        Assert.True(shot.Near(3, 8, g.Colour("Brush.Error")), shot.Describe(3, 8));
        Assert.True(shot.Near(8, 8, g.Colour("Brush.Canvas")), shot.Describe(8, 8));
    }, oneInk: false);

    [Fact]
    public Task NeedsYou_diamond_is_14_px_tip_to_tip_in_attention() => Check(RowGlyph.NeedsYou, 14, 14, "Brush.Attention", (g, shot) => Assert.True(shot.Near(8, 8, g.Colour("Brush.Attention")), shot.Describe(8, 8)));

    [Fact]
    public Task Draft_ring_is_tertiary_ink() => Check(RowGlyph.Draft, 14, 14, "Brush.InkTertiary", more: null);

    [Fact]
    public Task PrDraft_ring_is_tertiary_ink() => Check(RowGlyph.PrDraft, 14, 14, "Brush.InkTertiary", more: null);

    [Fact]
    public Task Linked_ring_stays_subtle_ink() => Check(RowGlyph.Linked, 14, 14, "Brush.InkSubtle", more: null);

    private static Task<bool> Check(RowGlyph glyph, double width, double height, string ink, Action<Drawn, Shot>? more, bool oneInk = true) => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowGlyphSpecTests).Assembly).Dispatch(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var probe = new Probe(glyph) { Width = 16, Height = 16 };
        var window = new Window { Content = probe, Width = 64, Height = 64, SizeToContent = SizeToContent.Manual };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var drawn = new Drawn(probe);
        var shot = Shot.Take(probe);
        var (boxW, boxH) = shot.Box();
        Assert.True(Math.Abs(boxW - width) <= 0.5 && Math.Abs(boxH - height) <= 0.5, string.Create(CultureInfo.InvariantCulture, $"{glyph} box is {boxW} x {boxH} px, grammar.css says {width} x {height}"));
        var expected = drawn.Colour(ink);
        Assert.True(!oneInk || shot.AllOpaqueNear(expected), $"{glyph}: some solid pixel is not {ink} ({expected})");
        more?.Invoke(drawn, shot);
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);

    private sealed class Probe(RowGlyph glyph) : Control
    {
        public override void Render(DrawingContext context) => RowGlyphPainter.Paint(context, glyph, RowGlyphPainter.PaletteFor(this));
    }

    private sealed class Drawn(Control owner)
    {
        public Color Colour(string key) => owner.TryFindResource(key, owner.ActualThemeVariant, out var v) && v is ISolidColorBrush b ? b.Color : throw new InvalidOperationException($"no solid brush {key}");
    }

    /// <summary>A 4x render of the 16 px probe as BGRA bytes.</summary>
    private sealed class Shot(byte[] bytes, int size)
    {
        public static Shot Take(Control probe)
        {
            var size = 16 * Scale;
            using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96 * Scale, 96 * Scale));
            bitmap.Render(probe);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            stream.Position = 0;
            using var read = WriteableBitmap.Decode(stream);
            using var frame = read.Lock();
            var bytes = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
            {
                Marshal.Copy(frame.Address + (y * frame.RowBytes), bytes, y * size * 4, size * 4);
            }

            return new Shot(bytes, size);
        }

        public (double Width, double Height) Box()
        {
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (bytes[(((y * size) + x) * 4) + 3] > 128)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            return maxX < 0 ? (0, 0) : ((maxX - minX + 1) / (double)Scale, (maxY - minY + 1) / (double)Scale);
        }

        public bool Near(double x, double y, Color want)
        {
            var i = ((((int)(y * Scale)) * size) + (int)(x * Scale)) * 4;
            return Within(bytes[i + 2], want.R) && Within(bytes[i + 1], want.G) && Within(bytes[i], want.B);
        }

        public string Describe(double x, double y)
        {
            var i = ((((int)(y * Scale)) * size) + (int)(x * Scale)) * 4;
            return string.Create(CultureInfo.InvariantCulture, $"pixel at ({x};{y}) is ({bytes[i + 2]};{bytes[i + 1]};{bytes[i]}) alpha {bytes[i + 3]}");
        }

        public bool AllOpaqueNear(Color want)
        {
            var any = false;
            for (var i = 0; i < bytes.Length; i += 4)
            {
                if (bytes[i + 3] >= 250)
                {
                    any = true;
                    if (!(Within(bytes[i + 2], want.R) && Within(bytes[i + 1], want.G) && Within(bytes[i], want.B)))
                    {
                        return false;
                    }
                }
            }

            return any;
        }

        public bool AllOpaqueOnBlend(Color from, Color to)
        {
            int[] f = [from.R, from.G, from.B];
            int[] e = [to.R, to.G, to.B];
            var lead = 0;
            for (var c = 1; c < 3; c++)
            {
                if (Math.Abs(e[c] - f[c]) > Math.Abs(e[lead] - f[lead]))
                {
                    lead = c;
                }
            }

            var any = false;
            for (var i = 0; i < bytes.Length; i += 4)
            {
                if (bytes[i + 3] < 250)
                {
                    continue;
                }

                any = true;
                int[] v = [bytes[i + 2], bytes[i + 1], bytes[i]];
                var span = e[lead] - f[lead];
                var t = span == 0 ? 0 : Math.Clamp((v[lead] - f[lead]) / (double)span, 0, 1);
                for (var c = 0; c < 3; c++)
                {
                    if (Math.Abs(v[c] - (f[c] + (t * (e[c] - f[c])))) > Tolerance)
                    {
                        return false;
                    }
                }
            }

            return any;
        }

        private static bool Within(byte a, byte b) => Math.Abs(a - b) <= Tolerance;
    }
}
