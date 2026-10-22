using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// Real-font canaries for the text-fit assertions that Aw.Sindri.Ui.Tests makes under headless drawing. That project has no
/// Skia, so Avalonia's stub shaper measures every glyph at the font size and knows neither Inter nor the embedded mono face:
/// its wrap, trim, clip and baseline numbers are not the app's. Here the same kinds of assertion run with the real fonts at the
/// narrow window (900 px) and the large text scale (1.3): a wrap, a trimmed value, a clipped line, a box leaving its parent,
/// two baselines, and the chord hint's width.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RealFontTextFitTests : IDisposable
{
    private const string LongReason = "/sindri/status answered with something this build can't read, and this sentence runs on "
        + "long enough that no window the app allows can show it on one line, so the banner has to wrap it and keep the fix in view";

    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RealFontTextFitTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(double width, Func<ShellViewModel, bool> ready)
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !ready(shell) && !following.IsFaulted; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.False(following.IsFaulted, following.Exception?.ToString());
        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Rect Box(Window window, Control control) => new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size); // why: the control is in the window

    private static void Scaled(double scale, Action body)
    {
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            body();
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    }

    // Wraps (more than one line), nothing trimmed, and every part inside the window: the Unreadable banner's long reason.
    [Fact]
    public Task Unreadable_banner_wraps_the_long_reason_and_keeps_every_part_in_the_window_at_900_px() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        _server.UnreadableReason = LongReason;
        var (window, _) = await Show(900, s => s.State == ShellState.Unreadable);

        var reason = Named<TextBlock>(window, "UnreadableText");
        Assert.True(reason.TextLayout.TextLines.Count > 1, string.Create(CultureInfo.InvariantCulture, $"the reason is on {reason.TextLayout.TextLines.Count} line"));
        var fix = Named<TextBlock>(window, "DoctorFixText");
        Assert.False(fix.TextLayout.TextLines.Any(l => l.HasCollapsed), "sindri doctor is trimmed");
        foreach (var part in (Control[])[reason, Named<TextBlock>(window, "UnreadableHeading"), Named<TextBlock>(window, "UnreadableCheckIt"), fix])
        {
            var box = Box(window, part);
            Assert.True(part.IsEffectivelyVisible && box.Left >= 0 && box.Right <= window.Bounds.Width + 0.5 && box.Bottom <= window.Bounds.Height + 0.5,
                $"{part.Name} runs out of the window: {box} in {window.Bounds.Size}");
        }
    });

    // A line that must stay whole is not clipped: the record's path (mono) and the stale record's detail, at scale 1.3.
    [Fact]
    public Task Not_running_path_and_detail_are_not_clipped_at_text_scale_1_3() => OnUi(async () =>
    {
        _records.Location = "/var/folders/07/k3x9q2m1n8b7v6c5x4z3/T/aw-sindri-home-1234567890/sindri/ui/control.json";
        var (window, _) = await Show(900, s => s.State == ShellState.NotRunning);
        Scaled(1.3, () =>
        {
            Settle(window);
            var path = Named<TextBlock>(window, "ReasonDetailText");
            Assert.True(path.IsEffectivelyVisible, "the path is hidden");
            Assert.EndsWith("/sindri/ui/control.json", path.Text, StringComparison.Ordinal);
            Assert.False(path.TextLayout.TextLines.Any(l => l.HasCollapsed), "the path is trimmed by layout: " + path.Text);
            Assert.True(path.TextLayout.WidthIncludingTrailingWhitespace <= path.Bounds.Width + 0.5, "the path is clipped");
            Assert.True(path.Bounds.Height < path.LineHeight * 1.5, string.Create(CultureInfo.InvariantCulture, $"the path takes {path.Bounds.Height} px"));
        });
    });

    [Fact]
    public Task Stale_record_detail_is_not_clipped_at_text_scale_1_3() => OnUi(async () =>
    {
        _records.Write(A.Record(pid: 69190, epoch: 4));
        _server.Pong = new(69190, 0);
        var (window, _) = await Show(900, s => s.State == ShellState.NotRunning);
        Scaled(1.3, () =>
        {
            Settle(window);
            var detail = Named<TextBlock>(window, "ReasonDetailText");
            Assert.True(detail.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(detail.Text), "the detail is empty or hidden");
            Assert.False(detail.TextLayout.TextLines.Any(l => l.HasCollapsed), "the detail is trimmed by layout: " + detail.Text);
            Assert.True(detail.TextLayout.WidthIncludingTrailingWhitespace <= detail.Bounds.Width + 0.5, "the detail is clipped");
        });
    });

    // No strip value is trimmed or leaves the strip at 900 px and scale 1.3.
    [Fact]
    public Task Health_strip_values_are_whole_and_inside_the_strip_at_900_px_and_text_scale_1_3() => OnUi(async () =>
    {
        _records.Write(A.Record(pid: 1234567));
        _server.Current = A.Status(pid: 1234567) with { ApiVersion = 1 };
        _server.Pong = new(1234567, 7);
        var (window, _) = await Show(900, s => s.State == ShellState.Normal);
        Scaled(1.3, () =>
        {
            Settle(window);
            var strip = Box(window, Named<Control>(window, "HealthStrip"));
            var values = Named<ItemsControl>(window, "HealthCells").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
            Assert.NotEmpty(values);
            foreach (var value in values)
            {
                Assert.False(value.TextLayout.TextLines.Any(l => l.HasCollapsed), value.Text + " is trimmed");
                var box = Box(window, value);
                Assert.True(box.Right <= strip.Right && box.Bottom <= strip.Bottom, value.Text + " runs out of the strip");
            }
        });
    });

    // Two texts of a row share a baseline (the real fonts' ascents differ between Inter and mono).
    [Fact]
    public Task Health_labels_and_values_share_a_baseline_with_the_real_fonts() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Push(A.Event(3));
        var (window, shell) = await Show(900, s => s.State == ShellState.Normal && s.Health.LastEvent.EndsWith("#3", StringComparison.Ordinal));
        shell.GoTo(PageId.System);
        Settle(window);

        static double Baseline(TextBlock t, Window w) => t.TranslatePoint(default, w)!.Value.Y + t.TextLayout.Baseline; // why: in the window
        var rows = Named<Control>(window, "HealthPage").GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("row") && g.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var texts = row.Children.OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Take(2).ToList();
            Assert.Equal(Baseline(texts[0], window), Baseline(texts[1], window), 1.0);
        }
    });

    // The chord hint's text is not clipped, and the hint stays in the window and the bar, at 900 px and scale 1.3.
    [Fact]
    public Task Chord_hint_text_is_not_clipped_and_stays_in_the_bar_at_900_px_and_text_scale_1_3() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(900, s => s.State == ShellState.Normal);
        Scaled(1.3, () =>
        {
            Settle(window);
            window.KeyPress(Key.G, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.True(shell.IsChordPending);
            var hint = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ChordHint");
            Assert.True(hint.IsEffectivelyVisible);
            var label = hint.GetVisualDescendants().OfType<TextBlock>().First();
            Assert.True(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 0.5, "the hint's text is clipped");
            Assert.True(Box(window, Named<Control>(window, "TopBar")).Contains(Box(window, hint)), "the hint is outside the top bar");
            Assert.True(new Rect(window.ClientSize).Contains(Box(window, hint)), "the hint leaves the window");
        });
    });
}
