using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// The focus ring as rendered (UI spec §11.1, §11.2): each control reached by the keyboard shows a ring of PrimaryHover
/// (#828fff) at full opacity on all four sides, just outside its bounds, and nothing the control sits in clips it away.
/// Key frames are saved under the test output's frames/ as focus-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class FocusRingTests : IDisposable
{
    // §11.1: the ring's colour, PrimaryHover.
    private static readonly (byte R, byte G, byte B) _ring = (0x82, 0x8f, 0xff);

    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FocusRingTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Running(double width = 1280, double height = 800)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = width, Height = height };
        var settings = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellState.Normal, shell.State);
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, keySymbol: null);
        Settle(window);
    }

    private static Control Focused(Window window) =>
        window.FocusManager?.GetFocusedElement() as Control ?? throw new InvalidOperationException("nothing has focus");

    private static bool FocusIsIn(Window window, Control region) =>
        window.FocusManager?.GetFocusedElement() is Control focused && (focused == region || region.IsVisualAncestorOf(focused));

    // F6 to the top bar, then Tab through its controls, collecting each one reached, until focus leaves the top bar.
    private static List<Control> TabThroughTopBar(ShellWindow window)
    {
        var topBar = window.Get<Control>("TopBar");
        FocusRegion(window, topBar);
        var reached = new List<Control>();
        for (var i = 0; i < 12 && FocusIsIn(window, topBar); i++)
        {
            reached.Add(Focused(window));
            Press(window, Key.Tab, PhysicalKey.Tab);
        }

        return reached;
    }

    // F6 until focus is in the region (§11.2: F6 cycles top bar, page and sheet).
    private static void FocusRegion(ShellWindow window, Control region)
    {
        for (var i = 0; i < 4 && !FocusIsIn(window, region); i++)
        {
            Press(window, Key.F6, PhysicalKey.F6);
        }

        Assert.True(FocusIsIn(window, region), "F6 never reached " + region.Name);
    }

    [Fact]
    public Task Every_top_bar_control_reached_by_Tab_shows_the_ring() => OnUi(async () =>
    {
        var (window, _) = await Running();
        var reached = TabThroughTopBar(window);
        string[] expected = ["Navigation", "SearchField", "HealthPill"]; // §11.2: navigation, search, health pill

        var names = reached.Select(Name).ToList();
        Assert.Equal(["TopBar", .. expected], names);

        // The top bar's own stop (F6 lands there) fills the window's width: its ring is drawn just inside it.
        FocusByTab(window, window.Get<Control>("TopBar"), "TopBar");
        AssertRing(window, Focused(window), "topbar-region", inside: true);

        foreach (var target in expected)
        {
            FocusByTab(window, window.Get<Control>("TopBar"), target);
            AssertRing(window, Focused(window), "topbar-" + target, round: target == "HealthPill");
        }
    });

    // N3: the pill's ring follows the pill's own corner radius (a full pill), not ControlCornerRadius's rounded rectangle.
    [Fact]
    public Task The_health_pills_ring_is_a_pill() => OnUi(async () =>
    {
        var (window, _) = await Running();
        FocusByTab(window, window.Get<Control>("TopBar"), "HealthPill");
        Settle(window);

        var pill = Focused(window);
        var ring = AdornerLayer.GetAdornerLayer(pill)!.Children.OfType<Border>().Single(b => b.IsEffectivelyVisible && AdornerLayer.GetAdornedElement(b) == pill);
        Assert.Equal(((Button)pill).CornerRadius, ring.CornerRadius);
        AssertRing(window, pill, "topbar-HealthPill-pill", round: true);
    });

    [Fact]
    public Task The_Home_health_strip_shows_the_ring() => OnUi(async () =>
    {
        var (window, _) = await Running();

        FocusByTab(window, window.Get<Control>("Page"), "HealthStrip");

        AssertRing(window, Focused(window), "home-health-strip");
    });

    [Fact]
    public Task The_sheet_buttons_show_the_ring() => OnUi(async () =>
    {
        var (window, shell) = await Running();
        Assert.True(DeepLink.TryParse("item/ITM-1", out var link));
        shell.Navigate(link);
        Settle(window);

        FocusByTab(window, window.Get<Control>("Sheet"), "SheetClose");

        AssertRing(window, Focused(window), "sheet-close");
    });

    [Fact]
    public Task Settings_controls_show_the_ring() => OnUi(async () =>
    {
        var (window, shell) = await Running();
        shell.GoTo(PageId.Settings);
        Settle(window);

        FocusByTab(window, window.Get<Control>("Page"), "ReduceMotionSwitch");
        AssertRing(window, Focused(window), "settings-switch");

        Press(window, Key.Tab, PhysicalKey.Tab);
        Assert.Equal("TextScaleSlider", Name(Focused(window)));
        AssertRing(window, Focused(window), "settings-slider");
    });

    // F6 until focus is in the region, then Tab until the named control has focus.
    private static void FocusByTab(ShellWindow window, Control region, string name)
    {
        FocusRegion(window, region);

        for (var i = 0; i < 40 && Name(Focused(window)) != name; i++)
        {
            Press(window, Key.Tab, PhysicalKey.Tab);
        }

        Assert.Equal(name, Name(Focused(window)));
    }

    // The control's name, or its nearest named logical ancestor's (a ListBoxItem reads as its ListBox, the top bar's own
    // stop as the top bar).
    private static string Name(Control control) =>
        control.Name ?? control.GetLogicalAncestors().OfType<Control>().FirstOrDefault(c => c.Name is not null)?.Name ?? "<unnamed>";

    // Captures a frame and checks the ring around the control (§11.1: 2 px, full opacity, offset 2 px). On each side, at a
    // quarter, a half and three quarters of its length: the pixel 3 px out from the edge (in the 2–4 px band) is the
    // ring's colour, and the pixel 1 px out (the 2 px gap) is not. A region that fills the window's width has no room
    // outside, so its ring sits 2 px inside its edge: the same check, mirrored.
    // round: a full pill's ring curves away from the corners, so its sides are sampled at mid-height only.
    private static void AssertRing(Window window, Control control, string frame, bool inside = false, bool round = false)
    {
        window.Activate();
        Settle(window);
        Assert.Same(control, Focused(window));
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame");
        Save(bitmap, frame);
        var pixels = Pixels(bitmap);
        var target = Adorned(control);
        var origin = target.TranslatePoint(new Point(0, 0), window) ?? throw new InvalidOperationException("not in the window");
        var box = new Rect(origin, target.Bounds.Size);
        var scale = window.RenderScaling;
        var outward = inside ? -1 : 1;

        var wrong = new List<string>();
        foreach (var f in new[] { 0.25, 0.5, 0.75 })
        {
            if (!round || f == 0.5)
            {
                Side("left", box.Left, box.Top + (box.Height * f), -outward, 0);
                Side("right", box.Right, box.Top + (box.Height * f), outward, 0);
            }

            Side("top", box.Left + (box.Width * f), box.Top, 0, -outward);
            Side("bottom", box.Left + (box.Width * f), box.Bottom, 0, outward);
        }

        Assert.True(wrong.Count == 0, $"ring wrong on {frame} ({box}) at: {string.Join(", ", wrong)}");

        void Side(string side, double x, double y, int dx, int dy)
        {
            bool At(int d) => pixels.IsRing((int)Math.Floor((x + (dx * d)) * scale), (int)Math.Floor((y + (dy * d)) * scale));
            if (!At(3) || At(1))
            {
                wrong.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{side}@{x:0},{y:0} (ring {At(3)}, gap {!At(1)})"));
            }
        }
    }

    // What the focus ring is drawn around: the control, or the part of its template it adorns (Fluent's ToggleSwitch
    // adorns its switch and label, not its whole box).
    private static Visual Adorned(Control control)
    {
        var layer = AdornerLayer.GetAdornerLayer(control) ?? throw new InvalidOperationException("no adorner layer");
        return layer.Children.Select(AdornerLayer.GetAdornedElement)
            .FirstOrDefault(v => v is not null && (v == control || control.IsVisualAncestorOf(v))) ?? control;
    }

    private static void Save(Bitmap bitmap, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        bitmap.Save(Path.Combine(dir, "focus-" + name + ".png"), new PngBitmapEncoderOptions());
    }

    private static Frame Pixels(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return new Frame(bytes, size.Width, size.Height, bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private sealed class Frame(byte[] bytes, int width, int height, bool rgba)
    {
        // A pixel at full ring colour (a small tolerance for the edge's anti-aliasing on a fractional position).
        public bool IsRing(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return false;
            }

            var i = ((y * width) + x) * 4;
            var (r, b) = rgba ? (bytes[i], bytes[i + 2]) : (bytes[i + 2], bytes[i]);
            var g = bytes[i + 1];
            return Math.Abs(r - _ring.R) <= 12 && Math.Abs(g - _ring.G) <= 12 && Math.Abs(b - _ring.B) <= 12;
        }
    }
}
