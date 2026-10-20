using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The context map's own controls as drawn (F3 spec 1 §2): the edges in Wire, WireHi and dashed, the ports, the dimmed edge,
/// the dot grid that moves with the view, and the coverage strip's bars. Frames are saved under the test output's frames/ as
/// context-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ContextControlsRenderingTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();
    private static readonly int[] _portXs = [20, 220];

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextControlsRenderingTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (byte R, byte G, byte B) Token(string name)
    {
        var hex = _tokens.Color(name)[^6..];
        return (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..], 16));
    }

    private static Window Stage(Control content, double width = 400, double height = 300)
    {
        var canvas = new Border { Background = (IBrush)Application.Current!.FindResource("Brush.Canvas")!, Child = content };
        var window = new Window { Content = canvas, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ContextEdgesControl Edges(params MapEdge[] edges) =>
        new() { Edges = edges, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch };

    private static MapEdge Line(string key, double y, bool dashed = false) => new(key, EdgeRoute.Straight(new Point(20, y), new Point(220, y)), dashed);

    private static Frame Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame");
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        bitmap.Save(Path.Combine(dir, "context-" + name + ".png"), new PngBitmapEncoderOptions());
        return Frame.Of(bitmap);
    }

    [Fact]
    public async Task A_confirmed_edge_is_a_solid_2_px_line_in_Wire() => await OnUi(() =>
    {
        var window = Stage(Edges(Line("a", 100)));
        var frame = Capture(window, "edge-solid");

        for (var x = 40; x < 200; x += 7)
        {
            Assert.True(frame.Near(x, 100, Token("Wire")), frame.Describe(x, 100));
            Assert.True(frame.Near(x, 99, Token("Wire")) || frame.Near(x, 101, Token("Wire")), "not 2 px thick at " + frame.Describe(x, 100));
            Assert.True(frame.Near(x, 94, Token("Canvas")), frame.Describe(x, 94));
        }

        window.Close();
        return true;
    });

    [Fact]
    public async Task A_suggested_edge_is_dashed_8_on_4_off() => await OnUi(() =>
    {
        var window = Stage(Edges(Line("a", 100, dashed: true)));
        var frame = Capture(window, "edge-dashed");

        var on = Enumerable.Range(40, 120).Select(x => frame.Near(x, 100, Token("Wire"))).ToList();
        var runs = Runs(on);

        Assert.Contains(false, on);
        Assert.Contains(true, on);
        Assert.All(runs.Where(r => r.Lit), r => Assert.InRange(r.Length, 7, 9));
        Assert.All(runs.Where(r => !r.Lit), r => Assert.InRange(r.Length, 3, 5));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_highlighted_edge_is_WireHi() => await OnUi(() =>
    {
        var edges = Edges(Line("a", 100), Line("b", 150));
        edges.Highlighted = new HashSet<string>(StringComparer.Ordinal) { "a" };
        var window = Stage(edges);
        var frame = Capture(window, "edge-highlight");

        Assert.True(frame.Near(120, 100, Token("WireHi")), frame.Describe(120, 100));
        Assert.True(frame.Near(120, 150, Token("Wire")), frame.Describe(120, 150));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_dimmed_edge_is_Wire_at_42_percent_over_the_canvas() => await OnUi(() =>
    {
        var edges = Edges(Line("a", 100));
        edges.Dimmed = new HashSet<string>(StringComparer.Ordinal) { "a" };
        var window = Stage(edges);
        var frame = Capture(window, "edge-dimmed");

        var (wire, canvas) = (Token("Wire"), Token("Canvas"));
        var expected = ((byte)(canvas.R + ((wire.R - canvas.R) * 0.42)), (byte)(canvas.G + ((wire.G - canvas.G) * 0.42)), (byte)(canvas.B + ((wire.B - canvas.B) * 0.42)));
        Assert.True(frame.Near(120, 100, expected, 6), frame.Describe(120, 100));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Each_end_has_a_10_px_port_filled_with_the_card_colour_and_outlined_in_Wire() => await OnUi(() =>
    {
        var window = Stage(Edges(Line("a", 100)));
        var frame = Capture(window, "edge-ports");

        foreach (var x in _portXs)
        {
            Assert.True(frame.Near(x, 97, Token("Surface1")), "port: " + frame.Describe(x, 97));
            Assert.True(frame.Near(x, 100, Token("Surface1")), "the line stops under the port");
            Assert.True(frame.Near(x, 95, Token("Wire"), 10), "the port's top outline: " + frame.Describe(x, 95));
            Assert.True(frame.Near(x, 94, Token("Canvas")), "nothing outside the outline: " + frame.Describe(x, 94));
            Assert.True(frame.Near(x, 104, Token("Wire"), 10), "the port's bottom outline: " + frame.Describe(x, 104));
        }

        window.Close();
        return true;
    });

    // C3: a port belongs to every edge that ends on it.
    [Fact]
    public async Task A_port_shared_with_a_lit_edge_is_lit_and_not_dimmed_by_the_dimmed_one() => await OnUi(() =>
    {
        var edges = Edges(
            new MapEdge("dim", EdgeRoute.Straight(new Point(20, 100), new Point(220, 100)), Dashed: false),
            new MapEdge("lit", EdgeRoute.Straight(new Point(20, 100), new Point(20, 200)), Dashed: false));
        edges.Dimmed = new HashSet<string>(StringComparer.Ordinal) { "dim" };
        edges.Highlighted = new HashSet<string>(StringComparer.Ordinal) { "lit" };
        var window = Stage(edges);
        var frame = Capture(window, "ports-shared");

        Assert.True(frame.Near(20, 95, Token("WireHi"), 10), "the shared port's outline: " + frame.Describe(20, 95));
        Assert.True(frame.Near(20, 97, Token("Surface1")), "the lit edge does not paint over the port: " + frame.Describe(20, 97));
        window.Close();
        return true;
    });

    // C3: a port whose edges are all dimmed is dimmed too.
    [Fact]
    public async Task A_port_of_a_dimmed_edge_alone_is_dimmed() => await OnUi(() =>
    {
        var edges = Edges(Line("dim", 100));
        edges.Dimmed = new HashSet<string>(StringComparer.Ordinal) { "dim" };
        var window = Stage(edges);
        var frame = Capture(window, "ports-dimmed");

        Assert.False(frame.Near(20, 95, Token("Wire"), 6), "dimmed outline: " + frame.Describe(20, 95));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_dot_grid_is_22_px_in_HairlineStrong_and_moves_with_the_view() => await OnUi(() =>
    {
        var zoom = new ZoomBorder { Child = new Border { Width = 1360, Height = 872 } };
        zoom.Resources["Motion.Base"] = TimeSpan.Zero;
        var window = Stage(zoom, 300, 200);
        var home = Capture(window, "dots-home");

        Assert.True(home.Near(11, 11, Token("HairlineStrong"), 14), home.Describe(11, 11));
        Assert.True(home.Near(33, 11, Token("HairlineStrong"), 14));
        Assert.True(home.Near(5, 5, Token("Canvas")));

        window.MouseWheel(new Point(150, 100), new Vector(-1, 0));   // a negative x delta moves the map 40 px right
        var moved = Capture(window, "dots-panned");

        Assert.Equal(new ZoomView(1, 40, 0), zoom.View);
        Assert.True(moved.Near(51, 11, Token("HairlineStrong"), 14), moved.Describe(51, 11));
        Assert.True(moved.Near(11, 11, Token("Canvas"), 4), "the dot left its old place");
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_coverage_strip_draws_full_hatched_and_empty_bars() => await OnUi(() =>
    {
        var strip = new CoverageStrip
        {
            Areas = [new("A", CoverageLevel.Searched, "searched"), new("B", CoverageLevel.Limited, "limited"), new("C", CoverageLevel.NotAvailable, "not available")],
            Lead = "2 of 3 areas searched.",
            Rest = "B is limited.",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = Stage(strip, 400, 60);
        var frame = Capture(window, "coverage-strip");

        var rects = Descend(strip).Where(b => b.Width == 34).Select(b => b.TranslatePoint(new Point(0, 0), window)!.Value).ToList();
        Assert.Equal(3, rects.Count);

        var full = rects[0];
        Assert.True(frame.Near((int)full.X + 17, (int)full.Y + 3, Token("Ink"), 10), frame.Describe((int)full.X + 17, (int)full.Y + 3));

        var hatched = rects[1];
        var samples = Enumerable.Range(0, 30).Select(i => frame.At((int)hatched.X + 2 + i, (int)hatched.Y + 3)).ToList();
        Assert.Contains(samples, p => Math.Abs(p.R - Token("InkSubtle").R) < 14);
        Assert.Contains(samples, p => Math.Abs(p.R - Token("Surface3").R) < 14);

        var empty = rects[2];
        Assert.True(frame.Near((int)empty.X + 17, (int)empty.Y + 3, Token("Surface3"), 10), frame.Describe((int)empty.X + 17, (int)empty.Y + 3));
        window.Close();
        return true;
    });

    private static IEnumerable<Border> Descend(Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<Border>();

    private static List<(bool Lit, int Length)> Runs(IReadOnlyList<bool> samples)
    {
        var runs = new List<(bool Lit, int Length)>();
        foreach (var lit in samples)
        {
            if (runs.Count > 0 && runs[^1].Lit == lit)
            {
                runs[^1] = (lit, runs[^1].Length + 1);
            }
            else
            {
                runs.Add((lit, 1));
            }
        }

        // The first and last run are cut by the sampling window.
        return runs.Count > 2 ? runs[1..^1] : runs;
    }

    private sealed class Frame(byte[] bytes, int width, int height, bool rgba, double scale)
    {
        public static Frame Of(Bitmap bitmap)
        {
            var size = bitmap.PixelSize;
            var stride = size.Width * 4;
            var buffer = Marshal.AllocHGlobal(stride * size.Height);
            try
            {
                bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
                var bytes = new byte[stride * size.Height];
                Marshal.Copy(buffer, bytes, 0, bytes.Length);
                return new Frame(bytes, size.Width, size.Height, bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888, size.Width / bitmap.Size.Width);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public string Describe(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"({x}, {y}) is {At(x, y)}");

        public (byte R, byte G, byte B) At(int x, int y)
        {
            var (px, py) = ((int)(x * scale), (int)(y * scale));
            if (px < 0 || py < 0 || px >= width || py >= height)
            {
                return default;
            }

            var i = ((py * width) + px) * 4;
            return rgba ? (bytes[i], bytes[i + 1], bytes[i + 2]) : (bytes[i + 2], bytes[i + 1], bytes[i]);
        }

        public bool Near(int x, int y, (byte R, byte G, byte B) want, int tolerance = 8)
        {
            var (r, g, b) = At(x, y);
            return Math.Abs(r - want.R) <= tolerance && Math.Abs(g - want.G) <= tolerance && Math.Abs(b - want.B) <= tolerance;
        }
    }
}
