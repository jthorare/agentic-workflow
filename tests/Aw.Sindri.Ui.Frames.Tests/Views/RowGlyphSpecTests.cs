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
    public Task Linked_stays_subtle_ink() => Check(RowGlyph.Linked, 14, 14, "Brush.InkSubtle", more: null);

    [Fact]
    public Task Message_bubble_is_13_wide_with_a_tail_down_to_14_5() => Check(RowGlyph.Message, 13, 12.5, "Brush.InkSubtle", (g, shot) =>
    {
        var (bodyW, bodyH) = shot.Box(maxY: 11);
        Assert.True(Math.Abs(bodyW - 13) <= 0.5 && Math.Abs(bodyH - 9) <= 0.5, string.Create(CultureInfo.InvariantCulture, $"Message bubble body (y < 11) is {bodyW} x {bodyH} px, grammar.css says 13 x 9"));
        Assert.True(shot.Near(8.5, 2.75, g.Colour("Brush.InkSubtle")), shot.Describe(8.5, 2.75));
    });

    /// <summary>
    /// Linked is two chained links on the diagonal (#3581, grammar.css .g.linked), not a ring. Coordinates are glyph-box px (14 x 14);
    /// the probe centres that box in 16 px, so add 1 to each. Link A's stroke centreline is a 7 x 4.5 rounded rect about (5.25, 8.75),
    /// turned -45 deg, so its long axis runs (0.707, -0.707) and its far end is the centre minus 3.5 along it: (2.78, 11.22).
    /// Link B is about (8.75, 5.25), far end (11.22, 2.78). The ring's outline touches the left, right, top and bottom middles; the
    /// nearest link is 4.42 away along its short axis, past its half height of 3, so those points are clear of antialiasing.
    /// Box: each link is a pill (8.5 x 6, radius 3), whose end caps are radius-3 half circles about (+-1.25, 0). Turned 45 deg, its half-extent
    /// in x and y is 1.25 * 0.707 + 3 = 3.88, so the chain spans 5.25 - 3.88 = 1.37 to 8.75 + 3.88 = 12.63: 11.26 px each way (the old ring was 14).
    /// </summary>
    [Fact]
    public Task Linked_is_two_links_on_the_diagonal_not_a_ring() => Check(RowGlyph.Linked, 11.25, 11.25, "Brush.InkSubtle", (g, shot) =>
    {
        (double X, double Y)[] empty = [(0.75, 7), (13.25, 7), (7, 0.75), (7, 13.25)];
        foreach (var (x, y) in empty)
        {
            Assert.True(shot.Alpha(x + 1, y + 1) <= 64, $"Linked: a ring outline point is inked, so it is still a ring. {shot.Describe(x + 1, y + 1)}");
        }

        (double X, double Y)[] ink = [(2.78, 11.22), (11.22, 2.78)];
        foreach (var (x, y) in ink)
        {
            Assert.True(shot.Alpha(x + 1, y + 1) >= 250 && shot.Near(x + 1, y + 1, g.Colour("Brush.InkSubtle")), $"Linked: the far end of a link is not Brush.InkSubtle. {shot.Describe(x + 1, y + 1)}");
        }
    });

    [Fact]
    public Task Linked_and_PrDraft_outlines_differ_at_1x() => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowGlyphSpecTests).Assembly).Dispatch(() =>
    {
        if (!_draws)
        {
            return true;
        }

        var linked = Mask1x(RowGlyph.Linked);
        var draft = Mask1x(RowGlyph.PrDraft);
        var differing = linked.Zip(draft).Count(pair => pair.First != pair.Second);
        Assert.True(differing >= 20, string.Create(CultureInfo.InvariantCulture, $"Linked and PrDraft differ in {differing} px at 1x, need at least 20: the outline must differ, not only size or ink"));
        return true;
    }, TestContext.Current.CancellationToken);

    private static bool[] Mask1x(RowGlyph glyph)
    {
        var probe = new Probe(glyph) { Width = 16, Height = 16 };
        var window = new Window { Content = probe, Width = 64, Height = 64, SizeToContent = SizeToContent.Manual };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        using var bitmap = new RenderTargetBitmap(new PixelSize(16, 16), new Vector(96, 96));
        bitmap.Render(probe);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var read = WriteableBitmap.Decode(stream);
        using var frame = read.Lock();
        var mask = new bool[16 * 16];
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                mask[(y * 16) + x] = Marshal.ReadByte(frame.Address, (y * frame.RowBytes) + (x * 4) + 3) > 128;
            }
        }

        window.Close();
        return mask;
    }

    [Fact]
    public Task PrDraft_centre_dot_stays_4_px() => Check(RowGlyph.PrDraft, 14, 14, "Brush.InkTertiary", (g, shot) =>
    {
        var (w, h) = shot.Box(centreRadius: 4, ink: g.Colour("Brush.InkTertiary"));
        Assert.True(Math.Abs(w - 4) <= 0.5 && Math.Abs(h - 4) <= 0.5, string.Create(CultureInfo.InvariantCulture, $"PrDraft centre dot is {w} x {h} px, grammar.css says 4 x 4"));
    });

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

        /// <summary>The box of solid pixels, optionally only those above maxY, within centreRadius px of (8, 8), and near the given ink.</summary>
        public (double Width, double Height) Box(double maxY = double.MaxValue, double centreRadius = double.MaxValue, Color? ink = null)
        {
            int minX = size, minY = size, maxPx = -1, maxPy = -1;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (!Included(x, y, maxY, centreRadius, ink))
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    maxPx = Math.Max(maxPx, x);
                    minY = Math.Min(minY, y);
                    maxPy = Math.Max(maxPy, y);
                }
            }

            return maxPx < 0 ? (0, 0) : ((maxPx - minX + 1) / (double)Scale, (maxPy - minY + 1) / (double)Scale);
        }

        private bool Included(int x, int y, double maxY, double centreRadius, Color? ink)
        {
            var i = ((y * size) + x) * 4;
            var px = (x + 0.5) / Scale;
            var py = (y + 0.5) / Scale;
            if (bytes[i + 3] <= 128 || py >= maxY || Math.Sqrt(Math.Pow(px - 8, 2) + Math.Pow(py - 8, 2)) > centreRadius)
            {
                return false;
            }

            return ink is not { } want || (Within(bytes[i + 2], want.R) && Within(bytes[i + 1], want.G) && Within(bytes[i], want.B));
        }

        public int Alpha(double x, double y) => bytes[(((((int)(y * Scale)) * size) + (int)(x * Scale)) * 4) + 3];

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
