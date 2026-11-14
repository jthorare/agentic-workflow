using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Icons;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The shell window under the headless platform, bound to a shell following the in-memory API: one key frame per pre-API
/// state (UI spec §4.1, §18.2), saved under the test output's frames/ as shell-*.png. Plain xUnit v3 facts that dispatch
/// onto the headless session's UI thread: Avalonia.Headless.XUnit 12.1.3 is built against xUnit v3 3.2 and fails
/// discovery under the repo's 4.0 (docs/csharp/sindri-ui.md "Tests").
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ShellWindowTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    // Each test stops its shell and closes its windows, then settles, before it returns: a layout pass or a ticker left
    // queued would run after the headless session has moved on (another test's theme, or the session's reset).
    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ShellWindowTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(Func<ShellViewModel, bool> ready, bool run = true, ISindriStarter? starter = null)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard(), window: starter is null ? null : WindowPorts.None with { Starter = starter });
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = run ? shell.Run(_stop.Token) : Task.CompletedTask;
        await Until(() => ready(shell) || following.IsFaulted);
        Assert.False(following.IsFaulted, following.Exception?.ToString());
        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static string Text(Window window, string name) => window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == name)?.Text ?? "<missing>";

    // A control by name anywhere in the window, across the views' own name scopes (TopBar, HomePage).
    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // A state icon's glyph (§11.3), by the icon's name.
    private static StateGlyph Glyph(Window window, string name) => Named<StateIcon>(window, name).Glyph;

    private static bool Visible(Window window, string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static void Frame(Window window, string name)
    {
        Settle(window);
        FrameFile.Save(window, "shell-" + name);
    }

    // Every visible control in the window carries an accessible name (§11.2). A live region's text is its name (it carries
    // no separate one: docs/csharp/sindri-ui.md "LiveSetting on macOS").
    private static void EveryControlIsNamed(Window window)
    {
        var unnamed = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible
                && (c is Button or ListBoxItem or TextBlock { Text.Length: > 1 })
                && string.IsNullOrEmpty(AutomationProperties.GetName(c))
                && !c.Classes.Contains("live-region")
                && !IsNamedDisclosureToggle(c)
                && AutomationProperties.GetAccessibilityView(c) != AccessibilityView.Raw   // hidden from the tree on purpose; its parent carries the name
                && (c is not TextBlock || (c.FindAncestorOfType<Button>() is null && c.FindAncestorOfType<ListBoxItem>() is null)))
            .Select(c => c.GetType().Name + ":" + (c as TextBlock)?.Text)
            .ToList();
        Assert.True(unnamed.Count == 0, "unnamed: " + string.Join(", ", unnamed));
    }

    // An Expander's header toggle: the named Expander carries the name.
    private static bool IsNamedDisclosureToggle(Control c) =>
        c is ToggleButton && c.FindAncestorOfType<Expander>() is { } disclosure && !string.IsNullOrEmpty(AutomationProperties.GetName(disclosure));

    // §14.2: every text is a step of the type ramp, so the Text size preference reaches all of it. At scale 1.3 each
    // visible TextBlock's size is a ramp size × 1.3 (a literal size, or one Fluent's own default sets, stays put). The
    // scale is applied, checked and reset without yielding the UI thread, so no other test sees it.
    private static readonly double[] _ramp = [40, 24, 20, 15, 13, 12, 11];   // Display, PageTitle, DetailTitle, SectionTitle, Body, Caption/Mono, Eyebrow/Kbd (#2546, #2779)

    private static void EveryTextIsOnTheRampAt130(Window window)
    {
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, 1.3);
        try
        {
            Settle(window);
            var off = window.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && !_ramp.Any(r => Math.Abs(t.FontSize - (r * 1.3)) < 0.01))
                .Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.Name ?? t.Text}={t.FontSize}"))
                .ToList();
            Assert.True(off.Count == 0, "off the ramp at 130%: " + string.Join(", ", off));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    }

    [Fact]
    public Task Every_text_on_Home_Health_and_the_sheet_follows_the_text_scale() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        EveryTextIsOnTheRampAt130(window);

        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        shell.GoTo(PageId.System);
        Settle(window);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_while_not_running_follows_the_text_scale() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_while_connecting_follows_the_text_scale() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_on_a_version_mismatch_follows_the_text_scale() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, _) = await Show(s => s.State == ShellState.VersionMismatch);
        EveryTextIsOnTheRampAt130(window);
    });

    // I6: the sheet's heading is DetailTitle (20, SemiBold since #2546) in the mono family; .mono's later size doesn't win.
    [Fact]
    public Task The_sheet_heading_is_a_mono_detail_title() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);

        var title = window.Get<TextBlock>("SheetSubjectText");
        Assert.Equal(20, title.FontSize);
        Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        Assert.Contains("JetBrains Mono", title.FontFamily.ToString(), StringComparison.Ordinal);
    });

    private static Color Ink(TextBlock t) => ((ISolidColorBrush)t.Foreground!).Color;

    private static Color Colour(string key) =>
        Application.Current!.TryGetResource(key, theme: null, out var v) && v is Color c ? c : default;

    // I1 (home.html .seg): one group; unselected labels muted, the selected one in PrimaryHover with the Primary dot;
    // The whole group disabled while not running.

    [Fact]
    public Task The_navigation_is_one_segmented_group_with_the_selected_dot() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);
        var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label") && t.Text is { Length: > 1 }).ToList();

        Assert.Equal(Colour("Color.PrimaryHover"), Ink(labels.Single(t => t.Text == Labels.Home)));
        Assert.All(labels.Where(t => t.Text != Labels.Home), t => Assert.Equal(Colour("Color.InkSubtle"), Ink(t)));
        _ = Assert.Single(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>(), e => e.Classes.Contains("nav-dot") && e.IsEffectivelyVisible);
        var group = Named<StackPanel>(window, "NavigationGroup");
        Assert.True(group.IsVisualAncestorOf(Named<ListBox>(window, "Navigation")));
        Frame(window, "nav");
    });

    [Fact]
    public Task Not_running_dims_and_disables_the_navigation() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        Assert.False(Named<StackPanel>(window, "NavigationGroup").IsEffectivelyEnabled);
        Assert.All(window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label")),
            t => Assert.Equal(Colour("Color.InkTertiary"), Ink(t)));
    });

    // I3 (home.html .phead): each Home panel has a header row (the eyebrow over a hairline) and a body; "not served yet"
    // and a group's empty sentence are quiet InkSubtle captions. N2/N6: panel insets and the page gutter are on the spacing scale (16, 24).
    [Fact]
    public Task Home_panels_have_a_header_row_and_a_quiet_body() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        // U2: Running and Queued are GET /queue's groups; an empty one shows its own sentence (E-18) the same quiet way.
        foreach (var name in (string[])["NeedsYouPanel", "RunningPanel", "QueuedPanel"])
        {
            var panel = Named<Border>(window, name);
            var head = panel.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("phead"));
            Assert.Equal(new Thickness(0, 0, 0, 1), head.BorderThickness);
            Assert.Contains(head.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("section-title"));
        }

        foreach (var (name, text) in (ReadOnlySpan<(string, string)>)[("RunningPanel", "Nothing running.")])
        {
            var panel = Named<Border>(window, name);
            var head = panel.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("phead"));
            var quiet = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == text && t.IsEffectivelyVisible);
            Assert.Contains("caption", quiet.Classes, StringComparer.Ordinal);
            Assert.Equal(Colour("Color.InkSubtle"), Ink(quiet));

            // I3: the caption sits centred in the panel's body, across and down (states.html .empty).
            var box = Box(window, name);
            var at = quiet.TranslatePoint(new Point(0, 0), window) ?? default;
            var ink = quiet.TextLayout.HitTestTextPosition(0);
            var inkCentreX = at.X + quiet.Padding.Left + ink.X + (quiet.TextLayout.Width / 2);
            var headBottom = (head.TranslatePoint(new Point(0, 0), window) ?? default).Y + head.Bounds.Height;
            Assert.InRange(inkCentreX - box.Center.X, -1, 1);
            Assert.InRange(at.Y + (quiet.Bounds.Height / 2) - ((headBottom + box.Bottom) / 2), -1, 1);
        }

        Assert.Equal(new Thickness(24), Named<Panel>(window, "PageContent").Margin);
        Frame(window, "home-panels");
    });

    // I7 (states.html Not running): a compact card centred in the page, with the struck ◆ mark.
    [Fact]
    public Task Not_running_is_a_compact_centred_card_with_the_struck_mark() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        var card = Box(window, "NotRunningPanel");
        var page = Box(window, "Page");
        Assert.True(card.Width <= 520, string.Create(CultureInfo.InvariantCulture, $"card is {card.Width} wide"));
        Assert.Equal(page.Center.X, card.Center.X, precision: 0);
        Assert.Equal(page.Center.Y, card.Center.Y, precision: 0);
        Assert.True(Visible(window, "NotRunningMark"));
        Assert.Contains("detail-title", window.Get<TextBlock>("NotRunningText").Classes, StringComparer.Ordinal);
    });

    // I7 (§4.1, §12.4): the card leads with the reason's words, and the record's path, shortened (~ for home, a middle
    // ellipsis), sits on its own line in mono, whole: never wrapped mid-path, at text scale 1.0 and 1.3.
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task Not_running_leads_with_the_reason_and_shows_the_path_shortened_on_its_own_line(double scale) => OnUi(async () =>
    {
        _records.Location = "/var/folders/07/k3x9q2m1n8b7v6c5x4z3/T/aw-sindri-home-1234567890/sindri/ui/control.json";
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            var reason = Named<TextBlock>(window, "ReasonText");
            var path = Named<TextBlock>(window, "ReasonDetailText");
            Assert.StartsWith("Sindri", reason.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("/", reason.Text, StringComparison.Ordinal);
            Assert.Equal("/var/…/sindri/ui/control.json", path.Text);
            Assert.Contains("mono", path.Classes, StringComparer.Ordinal);
            Assert.Equal(TextWrapping.NoWrap, path.TextWrapping);
            Assert.True(path.Bounds.Height < path.LineHeight * 1.5, string.Create(CultureInfo.InvariantCulture, $"the path takes {path.Bounds.Height} px"));
            Assert.True(Box(window, "ReasonDetailText").Top >= Box(window, "ReasonText").Bottom, "the path isn't on its own line");
            Assert.True(path.TextLayout.WidthIncludingTrailingWhitespace <= path.Bounds.Width + 0.5, "the path is clipped");
            Frame(window, string.Create(CultureInfo.InvariantCulture, $"not-running-path-{scale * 100:0}"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // R9 (§11.1, §11.3): one colour rule for glyphs: ✓ is Success wherever it means ok, ✕ is Error, every other glyph is
    // InkMuted; the word beside each keeps its own colour.
    [Fact]
    public Task Glyph_colours_follow_one_rule() => OnUi(async () =>
    {
        var (down, _) = await Show(s => s.State == ShellState.NotRunning);
        Assert.Equal(Brush("Error"), Named<StateIcon>(down, "PillGlyph").Foreground);

        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.Equal(Brush("Success"), Named<StateIcon>(window, "PillGlyph").Foreground);
        var profile = Named<ItemsControl>(window, "HealthCells").GetVisualDescendants().OfType<StateIcon>().Single(i => i.IsEffectivelyVisible && i.DataContext is HealthCell cell && string.Equals(cell.Label, Labels.CellProfile, StringComparison.Ordinal));
        Assert.Equal(Brush("Success"), profile.Foreground);
        shell.GoTo(PageId.System);
        Settle(window);
        Assert.Equal(Brush("Success"), Named<StateIcon>(window, "ProfileHeadGlyph").Foreground);

        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);
        Assert.Equal(Brush("InkMuted"), Named<StateIcon>(window, "PillGlyph").Foreground);
        Assert.Equal(Brush("InkMuted"), Named<StateIcon>(window, "RefreshIcon").Foreground);
    });

    private static object? Brush(string token) =>
        Application.Current!.TryGetResource("Brush." + token, Avalonia.Styling.ThemeVariant.Dark, out var brush) ? brush : null; // why: the headless session starts the app

    // R11: the not-responding banner's content starts on the page's 24 px gutter, in line with the page title.
    [Fact]
    public Task The_banner_lines_up_with_the_page_gutter() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        _server.FailStatus = true;
        for (var i = 0; i < 3; i++)
        {
            await shell.Health.Refresh(Token);
        }

        Settle(window);
        var glyph = Named<Control>(window, "NotRespondingBanner").GetVisualDescendants().OfType<StateIcon>().First();
        var title = Named<Control>(window, "HomePage").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("page-title"));
        Assert.Equal(title.TranslatePoint(default, window)!.Value.X, glyph.TranslatePoint(default, window)!.Value.X, 0.5); // why: both are in the window
    });

    // R12: on Health, each row's label and value share a baseline.
    [Fact]
    public Task Health_labels_and_values_share_a_baseline() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Push(A.Event(3));
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Health.LastEvent.EndsWith("#3", StringComparison.Ordinal));
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

    // R6 and R7 at the 1000 px minimum and text scale 1.3: the strip's "." keycap is a visible cap (its glyph bold at Body
    // size, the cap at least as wide as it is tall), and no strip value is trimmed (the pid shows whole).
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task The_strip_shows_its_keycaps_and_whole_values_at_1000_px(double scale) => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 600) });
        _records.Write(A.Record(pid: 1234567));
        _server.Current = A.Status(pid: 1234567) with { ApiVersion = 1 };
        _server.Pong = new(1234567, 7);
        var (window, _) = await Show(s => s.State == ShellState.Normal);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            var strip = Box(window, "HealthStrip");
            foreach (var value in Named<ItemsControl>(window, "HealthCells").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Assert.False(value.TextLayout.TextLines.Any(l => l.HasCollapsed), value.Text + " is trimmed");
                var box = new Rect(value.TranslatePoint(default, window)!.Value, value.Bounds.Size); // why: in the window
                Assert.True(box.Right <= strip.Right && box.Bottom <= strip.Bottom, value.Text + " runs out of the strip");
            }

            Assert.Contains(Named<ItemsControl>(window, "HealthCells").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Running");
            Frame(window, string.Create(CultureInfo.InvariantCulture, $"strip-1000-{scale * 100:0}"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // I8 (states.html Connecting): Home's frame (title, the Needs-you panel's header, two half panels) with left-aligned
    // skeleton bars; static, so nothing moves under reduced motion.
    [Fact]
    public Task Connecting_echoes_Homes_frame_with_left_aligned_skeleton_rows() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);
        var skeleton = Named<Control>(window, "ConnectingSkeleton");

        Assert.Contains(skeleton.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("page-title") && t.Text == Labels.Home);
        Assert.Contains(skeleton.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("section-title") && t.Text == Labels.NeedsYouPanel);
        Assert.Equal(3, skeleton.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("panel")));
        var bars = skeleton.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("skeleton")).ToList();
        Assert.True(bars.Count >= 6);
        Assert.All(bars, b => Assert.Equal(HorizontalAlignment.Left, b.HorizontalAlignment));
        Assert.All(bars, b => Assert.Empty(b.Transitions ?? []));
    });

    // I6: Close reads "✕ Close" so it doesn't look like Back's equal; lead (3): the heading's ring has room before Close.
    [Fact]
    public Task The_sheet_heading_has_room_for_its_ring_before_Close() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);

        Assert.Contains(window.Get<Button>("SheetClose").GetVisualDescendants().OfType<StateIcon>(), i => i.Icon == "Close");
        var title = Box(window, "SheetTitle");
        var close = Box(window, "SheetClose");
        Assert.True(close.Left - title.Right >= 8, string.Create(CultureInfo.InvariantCulture, $"{close.Left - title.Right} px between the heading and Close"));
    });

    // Lead (7): the edge tab keeps the spacing token's gap from the panels at a narrow window.
    [Fact]
    public Task The_edge_tab_keeps_a_gap_from_the_panels() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 700) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Settle(window);

        var tab = Box(window, "EdgeTab");
        var panel = Box(window, "NeedsYouPanel");
        Assert.True(tab.Left - panel.Right >= 8, string.Create(CultureInfo.InvariantCulture, $"{tab.Left - panel.Right} px between the panels and the edge tab"));
        Frame(window, "edge-tab-narrow");
    });

    // Lead (6), §11.2: F6 lands on the page region itself, and Tab then moves inside it.
    [Fact]
    public Task F6_lands_on_the_page_region_on_Home() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
        window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
        Settle(window);
        Assert.Same(window.Get<Border>("Page"), window.FocusManager?.GetFocusedElement());

        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
        Settle(window);
        Assert.True(window.Get<Border>("Page").IsVisualAncestorOf((Visual)window.FocusManager!.GetFocusedElement()!));
    });

    // Lead (5): ⌘, moves focus into Settings (its first control), as opening the sheet moves it to the heading.
    [Fact]
    public Task Command_comma_moves_focus_into_Settings() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);
        var settings = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        Settle(window);

        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        Assert.NotNull(focused);
        Assert.True(window.SettingsSlot.IsVisualAncestorOf(focused), focused.GetType().Name);
    });

    [Fact]
    public Task Connecting_shows_the_chrome_and_skeleton_rows() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);

        Assert.True(Visible(window, "TopBar"));
        Assert.True(Visible(window, "ConnectingSkeleton"));
        Assert.False(Visible(window, "Pages"));
        Assert.Equal(Labels.Connecting, Text(window, "HealthPillText"));

        // S1: no glyph while connecting, so its slot goes and the word sits centred in the pill.
        Assert.False(Visible(window, "PillGlyph"));
        var pill = Box(window, "HealthPill");
        var word = Box(window, "HealthPillText");
        Assert.True(Math.Abs(word.Left - pill.Left - (pill.Right - word.Right)) <= 1,
            string.Create(CultureInfo.InvariantCulture, $"insets {word.Left - pill.Left} and {pill.Right - word.Right}"));
        Frame(window, "connecting");
    });

    // First-run spec 4.1: while a start runs, the page's own no-record reason, its path line and "Looks again" would answer a second question.
    [Fact]
    public Task While_a_start_runs_the_no_record_reason_and_the_looks_again_line_are_hidden() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning, starter: new StartedStarter());

        Assert.Equal("Starting Sindri…", Text(window, "StartMessageText"));
        Assert.Equal((false, false, false), (Visible(window, "ReasonText"), Visible(window, "ReasonDetailText"), Visible(window, "LooksAgainText")));
        Assert.Equal(("Starting", false, true), (Text(window, "NotRunningText"), Visible(window, "NotRunningMark"), Visible(window, "StartingSpinner")));
        Assert.Equal(("Starting", false, true), (Text(window, "HealthPillText"), Visible(window, "PillGlyph"), Visible(window, "PillSpinner")));
        Assert.Equal("Waiting for Sindri · 0 s", Text(window, "StartDetailsText"));
    });

    private sealed class StartedStarter : ISindriStarter
    {
        public event EventHandler<SindriExitedEventArgs>? Exited { add { } remove { } }

        public int Launch => 1;

        public string LogPath => "/logs/serve.log";

        public void Release()
        {
        }

        public string? LastError() => null;

        public Result<SindriStartOutcome> Start() => SindriStartOutcome.Started;
    }

    [Fact]
    public Task Not_running_shows_the_reason_the_sindri_fix_and_how_often_it_looks_again() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        Assert.True(Visible(window, "NotRunningPanel"));
        Assert.False(Visible(window, "Pages"));
        Assert.Equal(Labels.NoRecord, Text(window, "ReasonText"));
        Assert.Equal("sindri", Text(window, "FixText"));
        Assert.Equal("Looks again every 2 s", Text(window, "LooksAgainText"));
        Assert.Equal(Labels.NotRunning, Text(window, "HealthPillText"));
        Assert.Equal(StateGlyph.Down, Glyph(window, "PillGlyph"));
        EveryControlIsNamed(window);
        Frame(window, "not-running");
    });

    // I7, kill -9 (§4.1, §12.4): SND-UI-002's sentence leads, the address shows once on its own mono line, and the
    // platform's exception text isn't shown.
    [Fact]
    public Task Not_running_after_kill_9_leads_with_a_sentence_and_shows_the_address_once() => OnUi(async () =>
    {
        _records.Write(A.Record(port: 61879));
        _server.GoDown();
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        Assert.Equal(Labels.NothingAnswered, Text(window, "ReasonText"));
        Assert.Equal("127.0.0.1:61879", Text(window, "ReasonDetailText"));
        Assert.Contains("mono", Named<TextBlock>(window, "ReasonDetailText").Classes, StringComparer.Ordinal);
        Assert.True(Box(window, "ReasonDetailText").Top >= Box(window, "ReasonText").Bottom, "the address isn't on its own line");
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty).ToList();
        _ = Assert.Single(texts, t => t.Contains("61879", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("refused", StringComparison.OrdinalIgnoreCase));
        EveryControlIsNamed(window);
        Frame(window, "not-running-kill9");
    });

    // I7, a stale record (§4.1, §12.4): SND-UI-001's sentence leads; the record and the answer are one detail line.
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task Not_running_on_a_stale_record_leads_with_a_sentence_and_one_detail_line(double scale) => OnUi(async () =>
    {
        _records.Write(A.Record(pid: 69190, epoch: 4));
        _server.Pong = new(69190, 0);
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            Assert.Equal(Labels.StaleRecord, Text(window, "ReasonText"));
            Assert.Equal("recorded pid 69190 · epoch 4, answering epoch 0", Text(window, "ReasonDetailText"));
            var detail = Named<TextBlock>(window, "ReasonDetailText");
            Assert.True(detail.TextLayout.WidthIncludingTrailingWhitespace <= detail.Bounds.Width + 0.5, "the detail is clipped");
            Frame(window, string.Create(CultureInfo.InvariantCulture, $"not-running-stale-{scale * 100:0}"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // ⌘, while not running (§12.3): Settings is local preferences, so it shows over the Not running card; Esc returns to
    // the card.
    [Fact]
    public Task Command_comma_while_not_running_shows_Settings_and_Esc_returns_to_the_card() => OnUi(async () =>
    {
        var (window, shell) = await Show(s => s.State == ShellState.NotRunning);
        var settings = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        Settle(window);
        Assert.True(Visible(window, "SettingsSlot"));
        Assert.False(Visible(window, "NotRunningPanel"));
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        Assert.True(focused is not null && window.SettingsSlot.IsVisualAncestorOf(focused), "focus isn't in Settings");
        Frame(window, "settings-not-running");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
        Assert.True(Visible(window, "NotRunningPanel"));
        Assert.False(Visible(window, "SettingsSlot"));
        Assert.Equal(PageId.Home, shell.CurrentPage);
        var after = window.FocusManager?.GetFocusedElement() as Control;
        Assert.True(after is { IsEffectivelyVisible: true }, "focus went nowhere");
    });

    // Review run 2 (julik-0): a sheet open when Sindri stops is hidden, not closed. One Esc on Settings over the card
    // returns to the card (it never spends the keypress on the hidden sheet), and Esc on the bare card keeps the hidden
    // sheet, so it is back when Sindri is.
    [Fact]
    public Task Esc_while_not_running_leaves_Settings_in_one_press_and_keeps_a_hidden_sheet() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        _records.Remove();
        _server.GoDown();
        _clock.Advance(HealthViewModel.PingEvery);
        await Until(() => shell.State == ShellState.NotRunning);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { }) };
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        Settle(window);
        Assert.True(Visible(window, "SettingsSlot"));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
        Assert.True(shell.IsNotRunningPanel);
        Assert.True(Visible(window, "NotRunningPanel"));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
        Assert.True(shell.IsSheetOpen);
        Assert.False(Visible(window, "Sheet"));
    });

    [Fact]
    public Task A_version_mismatch_takes_the_whole_window() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, _) = await Show(s => s.State == ShellState.VersionMismatch);

        Assert.True(Visible(window, "VersionMismatchPanel"));
        Assert.False(Visible(window, "TopBar"));
        // N4 (states.html): the two versions as labelled rows, and the fix as a sentence around sindri in the mono
        // command chip; the line is named by the whole sentence.
        var rows = Named<ItemsControl>(window, "VersionRows").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["This app", "API v1", "Sindri", "API v2"], rows);
        Assert.Equal(Labels.VersionMismatchFixLead, Text(window, "VersionFixText"));
        var command = window.Get<TextBlock>("VersionFixCommand");
        Assert.Equal(Labels.StartFix, command.Text);
        Assert.Contains("mono", command.Classes, StringComparer.Ordinal);
        Assert.Contains("command", command.FindAncestorOfType<Border>()!.Classes, StringComparer.Ordinal);
        Assert.Equal(Labels.VersionMismatchFix, AutomationProperties.GetName(window.Get<Control>("VersionFix")));
        Frame(window, "version-mismatch");
    });

    [Fact]
    public Task Normal_shows_Home_with_its_four_panel_frames() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        Assert.True(Visible(window, "HomePage"));
        foreach (var panel in (string[])["NeedsYouPanel", "RunningPanel", "QueuedPanel", "HealthStrip"])
        {
            Assert.True(Visible(window, panel), panel);
        }

        Assert.Equal((StateGlyph.Ok, Labels.Running, "updated 0s ago"), (Glyph(window, "PillGlyph"), Text(window, "HealthPillText"), Text(window, "PillAgeText")));
        Assert.Equal("Running · updated 0s ago", AutomationProperties.GetName(Named<Button>(window, "HealthPill")));
        Assert.Equal(1280, window.Width);
        Assert.Equal((1000d, 600d), (window.MinWidth, window.MinHeight));
        EveryControlIsNamed(window);
        Frame(window, "normal");
    });

    // Screens spec R8-13 withdraws the attention band (rev 7 §3.2): the health pill carries the state, and needs-you
    // shows as row tint and the panel's count chip. Whatever the page, the window has no such element.
    [Fact]
    public Task No_attention_band_on_any_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        foreach (var page in Enum.GetValues<PageId>())
        {
            shell.GoTo(page);
            Settle(window);

            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "AttentionBand");
        }
    });

    [Fact]
    public Task Unreadable_shows_the_banner_and_keeps_the_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        var (window, _) = await Show(s => s.State == ShellState.Unreadable);

        Assert.True(Visible(window, "UnreadableBanner"));
        Assert.True(Visible(window, "Pages"));
        Assert.Contains("SND-UI-003", Text(window, "UnreadableText"), StringComparison.Ordinal);
        Assert.Equal("sindri doctor", Text(window, "DoctorFixText"));
        Frame(window, "unreadable");
    });

    // Review run 2 (accessibility-tester-0, WCAG 4.1.3): the Unreadable banner, the Not responding banner and the Not
    // running card each announce themselves through a polite TextBlock, as "Sending…" does ("LiveSetting on macOS"): no
    // separate name, never "", live only while its state holds, the announcement set while Polite and its resting text
    // while Off, and in the tree before its state comes so the text change is what is spoken.
    [Fact]
    public Task Unreadable_is_announced_through_a_polite_live_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var changes = LiveChanges(window, "UnreadableLive");

        _server.UnreadableStatus = true;
        _server.Push(A.Event(1));
        await Until(() => shell.State == ShellState.Unreadable);
        Settle(window);
        Assert.Equal(ShellState.Unreadable, shell.State);
        _server.UnreadableStatus = false;
        _server.Push(A.Event(2));
        _clock.Advance(HealthViewModel.RetryEvery);
        await Until(() => shell.State == ShellState.Normal);
        Settle(window);

        AnnouncedOnly(window, "UnreadableLive", changes, Labels.Unreadable + ": SND-UI-003");
    });

    [Fact]
    public Task Not_responding_is_announced_through_a_polite_live_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var changes = LiveChanges(window, "NotRespondingLive");

        await GoStale(window, shell, BarState.NotResponding);
        Settle(window);
        Assert.True(shell.IsNotRespondingBanner);
        (_server.FailStatus, _server.FailPing) = (false, false);
        await shell.Health.Refresh(Token);
        Settle(window);

        AnnouncedOnly(window, "NotRespondingLive", changes, Labels.NotResponding);
    });

    [Fact]
    public Task Not_running_is_announced_through_a_polite_live_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var changes = LiveChanges(window, "NotRunningLive");

        _records.Remove();
        _server.GoDown();
        _clock.Advance(HealthViewModel.PingEvery);
        await Until(() => shell.State == ShellState.NotRunning);
        Settle(window);
        _server.ComeUp();
        _records.Write(A.Record());
        _clock.Advance(HealthViewModel.RetryEvery);
        await Until(() => shell.State == ShellState.Normal);
        Settle(window);

        AnnouncedOnly(window, "NotRunningLive", changes, Labels.NotRunning + ": ");
    });

    // Review run 5 (accessibility-tester-0): the version-mismatch card replaces the whole shell, so it takes focus (its
    // heading) and announces itself; the sentence is spoken through a polite live region that is in the tree before the
    // mismatch comes, including a mismatch that appears mid-session.
    [Fact]
    public Task A_version_mismatch_at_start_puts_focus_on_the_card_heading() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, _) = await Show(s => s.State == ShellState.VersionMismatch);

        Assert.True(Named<TextBlock>(window, "VersionMismatchText").Focusable);
        Assert.Same(Named<TextBlock>(window, "VersionMismatchText"), Focused(window));
    });

    [Fact]
    public Task A_version_mismatch_mid_session_moves_focus_from_the_hidden_shell_to_the_card_heading() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(Named<Border>(window, "Page").Focus(NavigationMethod.Tab));

        shell.Health.Show(A.Record().Url, A.Status() with { ApiVersion = 2 });
        await Until(() => ReferenceEquals(Focused(window), window.Get<TextBlock>("VersionMismatchText")));
        Settle(window);

        Assert.Equal(ShellState.VersionMismatch, shell.State);
        Assert.Same(Named<TextBlock>(window, "VersionMismatchText"), Focused(window));
    });

    [Fact]
    public Task A_version_mismatch_is_announced_through_a_polite_live_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var changes = LiveChanges(window, "MismatchLive");

        shell.Health.Show(A.Record().Url, A.Status() with { ApiVersion = 2 });
        Settle(window);

        Assert.Equal([(Labels.VersionMismatch, AutomationLiveSetting.Polite)], changes);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(Named<TextBlock>(window, "MismatchLive"))));
    });

    // Review run 5 (accessibility-tester-2, WCAG 4.1.3): when Sindri recovers, the banner and card clear silently and the
    // pill isn't live, so the change back is spoken through its own polite region. Spec §12 has no sentence for it, so it
    // says the pill's own label, "Running". Nothing is said at start-up, and the region rests when trouble comes again.
    [Fact]
    public Task Recovery_is_announced_as_Running_through_a_polite_live_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var changes = LiveChanges(window, "RunningLive");
        Assert.Empty(changes);

        _server.UnreadableStatus = true;
        _server.Push(A.Event(1));
        await Until(() => shell.State == ShellState.Unreadable);
        Settle(window);
        Assert.Empty(changes);
        _server.UnreadableStatus = false;
        _server.Push(A.Event(2));
        _clock.Advance(HealthViewModel.RetryEvery);
        await Until(() => shell.State == ShellState.Normal);
        Settle(window);

        Assert.Equal([(Labels.Running, AutomationLiveSetting.Polite)], changes);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(Named<TextBlock>(window, "RunningLive"))));

        _server.UnreadableStatus = true;
        _clock.Advance(HealthViewModel.RefreshEvery);   // the refresh that followed the recovery is still throttled (§9.2)
        _server.Push(A.Event(3));
        await Until(() => shell.State == ShellState.Unreadable);
        Settle(window);

        Assert.Equal([(Labels.Running, AutomationLiveSetting.Polite), (Labels.Sindri, AutomationLiveSetting.Off)], changes);
    });

    // Review run 3 (julik-frontend-races-reviewer-0): going straight from Not running to Unreadable and back, each state's
    // new reason is spoken only through its own live region. The old state's region goes Off and rests; it never speaks
    // the new reason while it is still Polite. The sequences are exact, so a stray announcement fails.
    [Fact]
    public Task Not_running_to_Unreadable_and_back_speaks_each_reason_only_through_its_own_region() => OnUi(async () =>
    {
        var (window, shell) = await Show(_ => true, run: false);
        var notRunning = LiveChanges(window, "NotRunningLive");
        var unreadable = LiveChanges(window, "UnreadableLive");
        _ = shell.Run(_stop.Token);
        await Until(() => shell.State == ShellState.NotRunning);
        Settle(window);
        Assert.Equal(ShellState.NotRunning, shell.State);
        var notRunningSaid = Labels.NotRunning + ": " + shell.Reason;

        _records.Write(A.Record());
        _server.Refusal = A.NoTokenRefusal;
        _clock.Advance(HealthViewModel.RetryEvery);
        await Until(() => shell.State == ShellState.Unreadable);
        Settle(window);
        Assert.Equal(ShellState.Unreadable, shell.State);
        var unreadableSaid = Labels.Unreadable + ": " + shell.Reason;

        _records.Remove();
        _clock.Advance(HealthViewModel.RetryEvery);
        await Until(() => shell.State == ShellState.NotRunning);
        Settle(window);
        Assert.Equal(ShellState.NotRunning, shell.State);

        Assert.Equal(
            [(notRunningSaid, AutomationLiveSetting.Polite), (Labels.Sindri, AutomationLiveSetting.Off), (notRunningSaid, AutomationLiveSetting.Polite)],
            notRunning);
        Assert.Equal([(unreadableSaid, AutomationLiveSetting.Polite), (Labels.Sindri, AutomationLiveSetting.Off)], unreadable);
    });

    // Every change of a live region's text, with the live setting it had when it changed.
    private static List<(string? Text, AutomationLiveSetting Live)> LiveChanges(Window window, string name)
    {
        var live = Named<TextBlock>(window, name);
        var changes = new List<(string? Text, AutomationLiveSetting Live)>();
        live.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                changes.Add((live.Text, AutomationProperties.GetLiveSetting(live)));
            }
        };
        Assert.True(live.IsEffectivelyVisible, $"{name} isn't in the tree before its state comes");
        return changes;
    }

    private static void AnnouncedOnly(Window window, string name, List<(string? Text, AutomationLiveSetting Live)> changes, string announced)
    {
        var live = Named<TextBlock>(window, name);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(live)), $"{name} has a separate name");
        Assert.Equal((0d, 0d), (live.Bounds.Width, live.Opacity));
        Assert.Contains(changes, c => c.Live == AutomationLiveSetting.Polite && c.Text!.StartsWith(announced, StringComparison.Ordinal));
        Assert.All(changes, c => Assert.False(string.IsNullOrEmpty(c.Text)));
        Assert.All(changes, c => Assert.Equal(c.Text == Labels.Sindri ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite, c.Live));
        Assert.Equal((Labels.Sindri, AutomationLiveSetting.Off), (live.Text, AutomationProperties.GetLiveSetting(live)));
    }

    [Fact]
    public Task Stale_says_so_in_the_pill() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;

        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Equal((StateGlyph.Stale, Labels.Stale), (Glyph(window, "PillGlyph"), Text(window, "HealthPillText")));
        Assert.Equal(Labels.UpdatedAgo(ShellViewModel.StaleAfter), Text(window, "PillAgeText"));
        Assert.True(Visible(window, "RefreshButton"));

        // R4: one mark per pill state, the glyph (§11.3); no dot beside it.
        Assert.False(Visible(window, "PillDot"));
        var pill = Named<Button>(window, "HealthPill").GetVisualDescendants().ToList();
        _ = Assert.Single(pill.OfType<Avalonia.Controls.Shapes.Shape>().Concat<Control>(pill.OfType<StateIcon>()), c => c.IsEffectivelyVisible);

        // R3: the strip's values are marked stale (⌛ and the word), and its name says so.
        Assert.True(Visible(window, "StripStale"));
        Assert.Equal(Labels.Stale, Text(window, "StripStaleText"));
        Assert.Equal(StateGlyph.Stale, Glyph(window, "StripStaleGlyph"));
        Assert.Equal(shell.Health.StripName, AutomationProperties.GetName(Named<Button>(window, "HealthStrip")));
        EveryControlIsNamed(window);
        Frame(window, "stale");

        var before = _server.StatusCalls;
        Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => shell.State == ShellState.Normal);
        Assert.Equal(before + 1, _server.StatusCalls);
        Assert.False(Visible(window, "RefreshButton"));
        Assert.Equal((StateGlyph.Ok, Labels.Running), (Glyph(window, "PillGlyph"), Text(window, "HealthPillText")));
        Assert.False(Visible(window, "StripStale"));
    });

    // R1 (§11.3): every glyph the shell draws is a vector icon from Theme/Icons.axaml at the icon size (16 px, the Body
    // line, scaled with the text), never a font glyph that falls back to another face at half height; each sits beside
    // its word.
    private static readonly string[] _glyphCharacters = ["✓", "✕", "◆", "◇", "⌛", "⟳", "⌘"];

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task State_glyphs_are_vector_icons_sized_to_the_text(double scale) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            var size = 16 * scale;
            foreach (var name in (string[])["PillGlyph", "RefreshIcon", "BrandMark"])
            {
                var icon = Named<StateIcon>(window, name);
                Assert.True(icon.IsEffectivelyVisible, name + " is hidden");
                Assert.NotNull(icon.Geometry);
                // Within layout rounding (the window rounds to whole pixels).
                Assert.Equal(size, icon.Bounds.Width, 0.5);
                Assert.Equal(size, icon.Bounds.Height, 0.5);
            }

            Assert.Equal(StateGlyph.Stale, Glyph(window, "PillGlyph"));
            Assert.Equal("Refresh", Named<StateIcon>(window, "RefreshIcon").Icon);
            Assert.All(window.GetVisualDescendants().OfType<StateIcon>().Where(i => i.IsEffectivelyVisible), i => Assert.NotNull(i.Geometry));
            var fontGlyphs = window.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && t.Text is { } text && _glyphCharacters.Any(g => text.Contains(g, StringComparison.Ordinal)))
                .Select(t => t.Name + ":" + t.Text)
                .ToList();
            Assert.True(fontGlyphs.Count == 0, "font glyphs: " + string.Join(", ", fontGlyphs));
            Frame(window, string.Create(CultureInfo.InvariantCulture, $"icons-{scale * 100:0}"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // §4.1 "sending" (§12.4), R2: while a refresh is in flight Refresh keeps its place and its label, disabled, and a
    // spinner replaces its ⟳ (static under reduced motion); after 300 ms "Sending…" is announced through a polite live
    // region that takes no room in the bar, so nothing in the top bar moves; a second click does nothing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task While_a_refresh_is_in_flight_Refresh_keeps_its_label_shows_a_spinner_and_announces_Sending(bool reduceMotion) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        var resources = Application.Current!.Resources;
        Motion.Apply(resources, reduceMotion);
        try
        {
            Settle(window);
            var stale = _topBarControls.Where(n => Visible(window, n)).Select(n => (n, Box(window, n))).ToList();
            _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var before = _server.StatusCalls;
            var refresh = Named<Button>(window, "RefreshButton");

            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            _clock.Advance(TimeSpan.FromMilliseconds(300));
            await Until(() => shell.Health.IsSending);
            Settle(window);

            SendingKeepsTheBar(window, stale, reduceMotion);
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(before + 1, _server.StatusCalls);
            EveryControlIsNamed(window);
            Frame(window, reduceMotion ? "refresh-sending-reduced" : "refresh-sending");

            _server.StatusGate.SetResult();
            await Until(() => shell.State == ShellState.Normal);
            Settle(window);
            var live = Named<TextBlock>(window, "RefreshSending");
            Assert.False(live.IsEffectivelyVisible);
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(live));
            Assert.False(Named<StateIcon>(window, "RefreshSpinner").IsSpinning);
        }
        finally
        {
            Motion.Apply(resources, reduce: false);
        }
    });

    // While sending: Refresh disabled with its label, the spinner in place of ⟳ (turning unless motion is reduced),
    // "Sending…" only in the zero-width live region, and every top-bar control where the stale bar had it.
    private static void SendingKeepsTheBar(Window window, List<(string, Rect)> stale, bool reduceMotion)
    {
        var refresh = Named<Button>(window, "RefreshButton");
        Assert.False(refresh.IsEffectivelyEnabled);
        Assert.Contains(refresh.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == Labels.Refresh);
        var spinner = Named<StateIcon>(window, "RefreshSpinner");
        Assert.True(spinner.IsEffectivelyVisible, "no spinner while sending");
        Assert.False(Visible(window, "RefreshIcon"));
        Assert.Equal(!reduceMotion, spinner.IsSpinning);
        var live = Named<TextBlock>(window, "RefreshSending");
        Assert.Equal(Labels.Sending, live.Text);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(live));
        Assert.Equal(0, live.Bounds.Width);
        Assert.Equal(0, live.Opacity);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.Text == Labels.Sending);
        Assert.Equal(stale, stale.Select(c => (c.Item1, Box(window, c.Item1))));
    }

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private async Task<(ShellWindow Window, ShellViewModel Shell)> StaleWithRefreshFocused()
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);
        Assert.True(Named<Button>(window, "RefreshButton").Focus(NavigationMethod.Tab));
        return (window, shell);
    }

    // §11.2 (c): when Sindri answers a focused Refresh, the button hides with the stale state; focus moves to its
    // neighbour, the health pill, never to nothing.
    [Fact]
    public Task When_Refresh_hides_on_recovery_focus_moves_to_the_pill() => OnUi(async () =>
    {
        var (window, shell) = await StaleWithRefreshFocused();

        Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => shell.State == ShellState.Normal);
        Settle(window);

        Assert.False(Visible(window, "RefreshButton"));
        Assert.Same(Named<Button>(window, "HealthPill"), Focused(window));
    });

    // §4.1 "refused / failed": focus stays on the control, through the in-flight phase that disables it.
    [Fact]
    public Task A_failed_refresh_keeps_focus_on_Refresh() => OnUi(async () =>
    {
        var (window, shell) = await StaleWithRefreshFocused();
        _server.FailStatus = true;
        _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = Named<Button>(window, "RefreshButton");

        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.False(refresh.IsEffectivelyEnabled);
        _server.StatusGate.SetResult();
        await Until(() => shell.Health.CanRefresh);
        Settle(window);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Same(refresh, Focused(window));
    });

    // §11.2 (c): Esc on a sheet a deep link opened returns focus to the page region (no row opened it), never to nothing;
    // beside the page and covering it below 1100 px alike.
    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task Esc_on_a_deep_linked_sheet_returns_focus_to_the_page_region(int width) => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 700) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);
        Assert.True(window.Get<Button>("SheetClose").Focus(NavigationMethod.Tab));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);

        Assert.False(Visible(window, "Sheet"));
        Assert.Same(window.Get<Border>("Page"), Focused(window));
    });

    // §11.2 (c): a sheet reopened from its edge tab returns focus to the edge tab when it closes.
    [Fact]
    public Task Closing_a_sheet_reopened_from_its_edge_tab_returns_focus_to_the_edge_tab() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Settle(window);
        var tab = window.Get<Button>("EdgeTab");
        Assert.True(tab.Focus(NavigationMethod.Tab));

        tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.True(window.Get<Button>("SheetClose").Focus(NavigationMethod.Tab));
        window.Get<Button>("SheetClose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Same(tab, Focused(window));
    });

    // Review run 4 (accessibility-tester-0; §11.2): at 1100 px and wider the sheet sits beside the page, and the edge tab
    // that reopens it hides as it opens. Focus moves into the sheet (its heading) rather than staying on the hidden tab.
    [Fact]
    public Task Reopening_the_sheet_from_its_edge_tab_at_1280_px_moves_focus_into_the_sheet() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1280, 800) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        shell.CloseSheet();
        Settle(window);
        var tab = window.Get<Button>("EdgeTab");
        Assert.True(tab.Focus(NavigationMethod.Tab));

        tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        var sheet = window.Get<Border>("Sheet");
        Assert.DoesNotContain("covers", sheet.Classes, StringComparer.Ordinal);
        Assert.False(tab.IsEffectivelyVisible);
        Assert.True(Focused(window) is Visual focused && sheet.IsVisualAncestorOf(focused), $"focus is on {(Focused(window) as Control)?.Name ?? Focused(window)?.GetType().Name ?? "nothing"}, not in the sheet");
        Assert.Same(window.Get<Border>("SheetTitle"), Focused(window));
    });

    // Review run 4 (accessibility-tester-1; WCAG 2.4.3): narrowing the window below 1100 px while the sheet is open makes
    // it cover the page, which it disables. Focus on a page control moves out (into the sheet or the top bar), never
    // staying on a disabled control under the sheet.
    [Fact]
    public Task Narrowing_below_1100_px_with_focus_in_the_page_moves_focus_out_of_the_covered_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1280, 800) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);
        Assert.True(Named<Button>(window, "HealthStrip").Focus(NavigationMethod.Tab));

        window.Width = 1000;
        Settle(window);

        var sheet = window.Get<Border>("Sheet");
        Assert.Contains("covers", sheet.Classes, StringComparer.Ordinal);
        Assert.False(window.Get<Border>("Page").IsEffectivelyEnabled);
        var focused = Focused(window) as Control;
        Assert.True(focused is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true }, $"focus is on {focused?.Name ?? focused?.GetType().Name ?? "nothing"}, disabled or hidden");
        Assert.True(focused is not null && (sheet.IsVisualAncestorOf(focused) || window.Get<Control>("TopBar").IsVisualAncestorOf(focused)), $"focus is on {focused?.Name ?? focused?.GetType().Name}, outside the sheet and the top bar");
    });

    // §11.2 (c): when a state change hides the focused control (Home's health strip when Sindri stops), focus moves to
    // the page region rather than nowhere.
    [Fact]
    public Task When_a_state_change_hides_the_focused_control_focus_moves_to_its_region() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(Named<Button>(window, "HealthStrip").Focus(NavigationMethod.Tab));

        _records.Remove();
        _server.GoDown();
        _clock.Advance(HealthViewModel.PingEvery);
        await Until(() => shell.State == ShellState.NotRunning);
        Settle(window);

        Assert.False(Visible(window, "HealthStrip"));
        Assert.Same(window.Get<Border>("Page"), Focused(window));
    });

    // B2 on the Health page: stale reads ⌛ stale at the process section's head.
    [Fact]
    public Task Stale_says_so_on_the_Health_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        shell.GoTo(PageId.System);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);

        Assert.Equal((StateGlyph.Stale, Labels.Stale), (Glyph(window, "ProcessGlyph"), Text(window, "ProcessWord")));
        Frame(window, "health-stale");
    });

    [Fact]
    public Task The_health_pill_opens_Health_with_S1bs_status_card() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Live());
        _server.Push(A.Event(3));
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Health.LastEvent.EndsWith("#3", StringComparison.Ordinal));

        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HealthPill").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(PageId.System, shell.CurrentPage);
        Assert.True(Visible(window, "HealthPage"));
        Settle(window);

        // I5 (services-health.html HEALTH): label/value rows at Body size under section heads that carry the state's
        // glyph and word; no badge repeating the pill. #2779: the health word is the page's one display number, and the
        // card titles are section titles; every other text is Body size or smaller.
        List<string?> Rows(string name) => [.. Named<ItemsControl>(window, name).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)];
        Assert.Equal(["pid · epoch", "4242 · 7", "Host lease", "held · expires " + A.Noon.AddSeconds(60).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture), "API", "127.0.0.1:7410"], Rows("ProcessRows"));
        Assert.Equal((StateGlyph.Ok, Labels.Running), (Glyph(window, "ProcessGlyph"), Text(window, "ProcessWord")));
        Assert.Equal(("live", StateGlyph.Ok), (Text(window, "ProfileHeadText"), Glyph(window, "ProfileHeadGlyph")));
        Assert.Equal(["live", "ab12cd", "file", "/home/profile.yaml"], Rows("ProfileRows"));
        HealthHeadsAndPathsAreAligned(window);
        LastEventRowKeepsTheRhythm(window);
        Assert.False(Visible(window, "HeadlineText"));
        var page = Named<Control>(window, "HealthPage");
        Assert.Equal("HeadlineWord", Assert.Single(page.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Classes.Contains("display")).Name);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.FontSize > 13
            && !t.Classes.Contains("page-title") && !t.Classes.Contains("display") && !t.Classes.Contains("section-title"));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "last run " + DateTimeOffset.Parse("2026-01-01T00:00:55Z", CultureInfo.InvariantCulture).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));   // the Services card reads the services view
        EveryControlIsNamed(window);
        Frame(window, "health");
    });

    // F1P-c (code index spec 6.4): the Context map card is up front on Health, a glyph and a word for the state with Sindri's sentence under it, every part named.
    [Fact]
    public Task The_context_map_card_shows_the_publish_state_in_a_word_and_a_glyph_with_Sindris_sentence() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.MapPublishAnswer = new(System.Net.HttpStatusCode.OK,
            """{"state":"held","word":"held for the code index","reason":null,"stale":false,"staleRepos":[],"version":14,"until":"2026-10-21T12:15:00Z","lines":["Map v14 is held for the code index until 12:15Z.","aw-split-panes: building (the map is for 1234567, the index is at abcdef0)"]}""");
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Health.ContextMap.IsVisible);
        shell.GoTo(PageId.System);
        Settle(window);

        Assert.True(Visible(window, "ContextMapPanel"));
        Assert.Equal((StateGlyph.Stale, "Held for the code index", Labels.ContextMapCard), (Glyph(window, "ContextMapGlyph"), Text(window, "ContextMapState"), Text(window, "ContextMapEyebrow")));
        Assert.Equal(["Map v14 is held for the code index until 12:15Z.", "aw-split-panes: building (the map is for 1234567, the index is at abcdef0)"],
            [.. Named<ItemsControl>(window, "ContextMapLines").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)]);
        Assert.StartsWith("Context map: Held for the code index. Map v14", AutomationProperties.GetName(Named<Border>(window, "ContextMapPanel")), StringComparison.Ordinal);
        EveryControlIsNamed(window);
    });

    // The card shows nothing while there is no map to publish.
    [Fact]
    public Task The_context_map_card_is_absent_while_there_is_no_map() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.MapPublishAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.NotFound, "SND-CONTEXT-030", "there is no map yet");
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Health.ViewReads > 0);
        shell.GoTo(PageId.System);
        Settle(window);

        Assert.False(Visible(window, "ContextMapPanel"));
    });

    // F3 review (456-1): what the person must act on stays up front; only the per-service detail waits behind Details.
    [Fact]
    public Task The_actions_are_up_front_with_the_cards() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Live());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        shell.GoTo(PageId.System);
        Settle(window);

        Assert.True(Visible(window, "ActionsSection"));
        Assert.True(Visible(window, "ProcessRows"));
    });

    [Fact]
    public Task Homes_health_strip_shows_the_health_line_and_opens_Health() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        // I4 (home.html): labelled cells, the G . hint and a trailing ›; still one button named by the health line.
        var cells = Named<ItemsControl>(window, "HealthCells");
        var texts = cells.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["Process", "Running", "Services", "1 service", "Profile", "live"], texts);
        Assert.Equal([StateGlyph.Ok, StateGlyph.Ok], cells.GetVisualDescendants().OfType<StateIcon>().Where(i => i.IsEffectivelyVisible).Select(i => i.Glyph));
        Assert.True(Visible(window, "HealthChevron"));
        Assert.True(Visible(window, "HealthKeys"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => string.Equals(b.Name, "GoKeyCap", StringComparison.Ordinal) || string.Equals(b.Name, "HealthKeyCap", StringComparison.Ordinal));   // the bare G and . caps are gone from the strip
        var strip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HealthStrip");
        Assert.Equal(shell.Health.StripName, AutomationProperties.GetName(strip));
        EveryControlIsNamed(window);
        Frame(window, "home-health-strip");

        strip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.Equal(PageId.System, shell.CurrentPage);

        shell.GoTo(PageId.Home);
        Settle(window);
        Assert.True(strip.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        Settle(window);
        Assert.Equal(PageId.System, shell.CurrentPage);
    });

    [Fact]
    public Task A_deep_link_opens_the_sheet_and_Esc_leaves_the_edge_tab() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));

        shell.Navigate(link);
        Settle(window);
        Assert.True(Visible(window, "Sheet"));
        Assert.Equal("ITM-01JC8", Text(window, "SheetSubjectText"));
        Assert.Equal(UiPreferences.DefaultSheetWidth, window.Get<Border>("Sheet").Bounds.Width);
        Frame(window, "sheet");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
        Assert.False(Visible(window, "Sheet"));
        Assert.True(Visible(window, "EdgeTab"));
        Frame(window, "edge-tab");
    });

    [Fact]
    public Task Below_1100_px_the_sheet_covers_the_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 700) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("decision/DEC-7", out var link));

        shell.Navigate(link);
        Settle(window);

        Assert.Equal(1000, window.Width);
        Assert.Contains("covers", window.Get<Border>("Sheet").Classes, StringComparer.Ordinal);
        Assert.True(Visible(window, "SheetBack"));
        Assert.Equal(window.Get<Border>("Sheet").Bounds.Width, window.ClientSize.Width, precision: 0);
        Frame(window, "sheet-covers");
    });

    // Review run 3 (accessibility-tester-3; WCAG 2.4.3, 2.4.7): at 1000 px the sheet covers the page, so the page and its
    // controls leave the tab order while it does. Tab and Shift+Tab from Back, and F6, never land under the sheet, and
    // closing it gives the page back its focus and its stops.
    [Fact]
    public Task At_1000_px_the_covering_sheet_takes_the_page_out_of_the_tab_order() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 600) });
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);
        Assert.Contains("covers", window.Get<Border>("Sheet").Classes, StringComparer.Ordinal);
        var page = window.Get<Border>("Page");
        bool UnderTheSheet() => Focused(window) is Visual focused && (focused == page || page.IsVisualAncestorOf(focused));

        foreach (var (key, modifiers) in new[] { (Key.Tab, RawInputModifiers.Shift), (Key.Tab, RawInputModifiers.None), (Key.F6, RawInputModifiers.None), (Key.F6, RawInputModifiers.Shift) })
        {
            Assert.True(window.Get<Button>("SheetBack").Focus(NavigationMethod.Tab));
            for (var i = 0; i < 12; i++)
            {
                window.KeyPress(key, modifiers, key == Key.Tab ? PhysicalKey.Tab : PhysicalKey.F6, keySymbol: null);
                Settle(window);
                Assert.False(UnderTheSheet(), string.Create(CultureInfo.InvariantCulture, $"{modifiers}+{key} #{i + 1} reached {(Focused(window) as Control)?.Name ?? Focused(window)?.GetType().Name} under the sheet"));
            }
        }

        Assert.True(window.Get<Button>("SheetBack").Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);

        Assert.Same(page, Focused(window));
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
        Settle(window);
        Assert.True(UnderTheSheet() && Focused(window) != page, "Tab moves inside the page again once the sheet closes");
    });

    // §3.2: window minimum 1000 × 600, and the top bar still fits there in every pill state, at text scale 1.0 and 1.3: the
    // ⌘K field gives way first (down to its magnifier), then the pill's age (its accessible name keeps it); the pill's
    // word stays whole with room for its focus ring, nothing in the bar overlaps or runs past the window, and nothing
    // overflows the bar's 48 px. "Sindri not responding: run sindri doctor" stays readable in full in a banner under it.
    /// <summary>A state of the minimum-width test: the pill's states, and the two the pill isn't in.</summary>
    public enum BarState
    {
        /// <summary>Running and fresh.</summary>
        Normal,

        /// <summary>Stale, with Refresh.</summary>
        Stale,

        /// <summary>Stale after three failed refreshes.</summary>
        NotResponding,

        /// <summary>Stale, with a refresh in flight past 300 ms: "Sending…" beside Refresh.</summary>
        Sending,

        /// <summary>Not running.</summary>
        NotRunning,

        /// <summary>SND-UI-003.</summary>
        Unreadable,

        /// <summary>The full-window panel (no top bar).</summary>
        VersionMismatch,

        /// <summary>First load.</summary>
        Connecting,

        /// <summary>Degraded, with a problem long enough that its text has to give way (#2547). Last, so the frame names of the others don't move.</summary>
        DegradedLongProblems,

        /// <summary>Degraded, with a short problem ("service broken failed") that reads in full at 1280 px (#2547). Last, as above.</summary>
        DegradedShortProblem,
    }

    // The Projects page spec §7: every tab (nine since Workflows, PR 17 part 2a) at the 1,000 px minimum, each label whole (not trimmed, not past the window's edge)
    // at text scale 1.0 and 1.3. The counts may give way first (TopBar.Counts); the labels never do.
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public Task At_the_1000_px_minimum_all_nine_tab_labels_are_whole(double scale) => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 600) });
        var window = await ShowIn(BarState.Normal);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            var shell = (ShellViewModel)window.DataContext!;
            var labels = Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label")).ToList();

            Assert.Equal(shell.Navigation.Select(n => n.Label), labels.Select(t => t.Text), StringComparer.Ordinal);
            Assert.Equal(9, labels.Count);
            foreach (var label in labels)
            {
                using var layout = new TextLayout(label.Text, new Typeface(label.FontFamily, label.FontStyle, label.FontWeight), label.FontSize, foreground: null);
                var box = new Rect(label.TranslatePoint(new Point(0, 0), window) ?? default, label.Bounds.Size);
                var at = string.Create(CultureInfo.InvariantCulture, $"{label.Text} {box} at {scale:0.0}");
                Assert.True(label.IsEffectivelyVisible, $"{at}: hidden");
                Assert.True(label.Bounds.Width >= layout.Width - 0.5, string.Create(CultureInfo.InvariantCulture, $"{at}: trimmed to {label.Bounds.Width} of {layout.Width}"));
                Assert.True(box.Left >= 0 && box.Right <= window.ClientSize.Width, $"{at}: outside the window");
            }
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // One page, one name: every tab draws its full label at 900 px and at 1,280 px (the bar shrinks padding, never the words).
    [Theory]
    [InlineData(900)]
    [InlineData(1280)]
    public Task Nav_tab_labels_are_the_same_at_900_and_1280(int width) => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var window = await ShowIn(BarState.Normal);
        Settle(window);

        var shell = (ShellViewModel)window.DataContext!;
        var labels = Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label")).ToList();
        Assert.Equal(shell.Navigation.Select(n => n.Label), labels.Select(t => t.Text), StringComparer.Ordinal);
        Assert.Contains("Workflows", labels.Select(t => t.Text), StringComparer.Ordinal);
        Assert.Contains(Labels.ReviewsTab, labels.Select(t => t.Text), StringComparer.Ordinal);
    });

    // Focus rings are for the keyboard: opening the window and clicking or driving to another page with no key pressed leaves no tab with a ring.
    [Fact]
    public Task Only_keyboard_focus_draws_a_ring_on_the_tabs_and_the_page() => OnUi(async () =>
    {
        var window = await ShowIn(BarState.Normal);
        var shell = (ShellViewModel)window.DataContext!;
        Settle(window);
        Assert.False(Named<Border>(window, "Page").Classes.Contains(":focus-visible"), "the page region draws a ring");   // Home's page region: no outline after the window opens

        shell.GoTo(PageId.Work);
        Settle(window);
        shell.GoTo(PageId.Projects);
        Settle(window);

        var tabs = Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Equal(9, tabs.Count);
        Assert.DoesNotContain(tabs, t => t.Classes.Contains(":focus-visible"));
        Assert.False(Named<Border>(window, "Page").Classes.Contains(":focus-visible"), "the page region draws a ring");

        // The keyboard half: once a key was pressed, the focus the window puts on a tab a command just selected draws the ring.
        window.KeyPress(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, keySymbol: null);
        shell.SelectedNav = shell.Navigation.First(n => n.Page == PageId.Agents);
        Settle(window);
        Settle(window);
        var selected = tabs.Single(t => t.IsSelected);
        Assert.True(selected.Classes.Contains(":focus-visible"), "the selected tab has no ring after a key press and a tab selection");
    });

    // The folded search icon keeps the shortcut in its tooltip.
    [Fact]
    public Task The_folded_search_icon_names_its_shortcut_in_its_tooltip() => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 900, 600) });
        var window = await ShowIn(BarState.Normal);
        Settle(window);

        var field = Named<Border>(window, "SearchField");
        Assert.True(field.Classes.Contains("compact"), "the field did not fold at 900 px");
        Assert.Contains(OperatingSystem.IsMacOS() ? "⌘K" : "Ctrl+K", ToolTip.GetTip(field) as string ?? string.Empty, StringComparison.Ordinal);
    });

    // Sending… beside Refresh, and the not-responding sentence in full in its banner.
    private static void StateWords(Window window, BarState state, double scale)
    {
        if (state == BarState.Sending)
        {
            // R2: the spinner replaces ⟳ inside Refresh and "Sending…" is only announced, so the bar keeps the stale
            // state's layout: the brand word stays wherever the stale bar kept it.
            Assert.True(Visible(window, "RefreshSpinner"), "no spinner while sending");
            Assert.Equal(Labels.Sending, Named<TextBlock>(window, "RefreshSending").Text);
            Assert.Equal(0, Named<TextBlock>(window, "RefreshSending").Bounds.Width);
            // Eight tabs (Projects page spec §7) leave no room for the word at 1.3x; the bar steps down to the mark alone, as stale does.
            Assert.True(scale > 1.0 || Visible(window, "BrandWord"), "the brand word dropped while sending");
        }

        if (state == BarState.NotResponding)
        {
            var banner = Named<Control>(window, "NotRespondingBanner");
            Assert.True(banner.IsEffectivelyVisible, "the not-responding banner is hidden");
            AllInside(window, banner, state, scale);
            var words = banner.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 1 }).Select(t => t.Text);
            Assert.Equal(Labels.NotResponding, string.Join(' ', words));
            Assert.Equal(Labels.NotResponding, AutomationProperties.GetName(banner));
        }
    }

    // The window in one of the minimum-width test's states.
    private async Task<ShellWindow> ShowIn(BarState state)
    {
        if (state is BarState.NotRunning)
        {
            return (await Show(s => s.State == ShellState.NotRunning)).Window;
        }

        if (state is BarState.Connecting)
        {
            return (await Show(s => s.State == ShellState.Connecting, run: false)).Window;
        }

        _records.Write(A.Record());
        _server.UnreadableStatus = state == BarState.Unreadable;
        _server.Current = StatusIn(state);
        var expected = state == BarState.Unreadable ? ShellState.Unreadable
            : state == BarState.VersionMismatch ? ShellState.VersionMismatch
            : ShellState.Normal;
        var (window, shell) = await Show(s => s.State == expected && (state is not (BarState.DegradedLongProblems or BarState.DegradedShortProblem) || s.Pill.HasProblems));
        if (state is BarState.Stale or BarState.NotResponding or BarState.Sending)
        {
            await GoStale(window, shell, state);
        }

        Settle(window);
        return window;
    }

    // The status the server answers in a bar state: version 2 for the mismatch, a problem for the two degraded states.
    private static StatusView StatusIn(BarState state) =>
        state == BarState.VersionMismatch ? A.Status() with { ApiVersion = 2 }
        : state == BarState.DegradedLongProblems ? A.Status() with { Health = new HealthView("degraded", [new HealthProblemView("SND-PROFILE-001", "the profile changed since approval", "the profile changed since approval and a much longer clause")]) }
        : state == BarState.DegradedShortProblem ? A.Status() with { Health = new HealthView("degraded", [new HealthProblemView("SND-SERVICE-001", "service broken failed")]) }
        : A.Status();

    // Stale (no ping for 30 s); then a refresh in flight past 300 ms, or three failed refreshes.
    private async Task GoStale(Window window, ShellViewModel shell, BarState state)
    {
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Assert.Equal(ShellState.Stale, shell.State);
        if (state is BarState.Sending)
        {
            _server.StatusGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            _clock.Advance(TimeSpan.FromMilliseconds(300));
            await Until(() => shell.Health.IsSending);
            Assert.True(shell.Health.IsSending);
        }

        if (state is BarState.NotResponding)
        {
            _server.FailStatus = true;
            for (var i = 0; i < 3; i++)
            {
                await shell.Health.Refresh(Token);
            }
        }
    }

    private static void TopBarFits(Window window, BarState state, double scale)
    {
        var at = string.Create(CultureInfo.InvariantCulture, $"{state} at {scale:0.0}");
        var bar = _topBarControls.Where(n => Visible(window, n)).Select(n => (Name: n, Box: Box(window, n))).ToList();
        var pill = bar[^1];
        Assert.Equal("HealthPill", pill.Name);
        Assert.True(Visible(window, "HealthPillText"), $"{at}: the pill's word is hidden");
        var padding = ((Border)Named<TopBar>(window, "TopBar").Content!).Padding.Right;
        Assert.True(pill.Box.Right <= window.ClientSize.Width - padding,
            string.Create(CultureInfo.InvariantCulture, $"{at}: the pill ends at {pill.Box.Right}, past the window's {window.ClientSize.Width} less the bar's {padding} px padding"));
        Assert.True(pill.Box.Right <= window.ClientSize.Width - 4, $"{at}: the pill's ring is cut");
        for (var i = 1; i < bar.Count; i++)
        {
            Assert.True(bar[i - 1].Box.Right <= bar[i].Box.Left, $"{at}: {bar[i - 1].Name} {bar[i - 1].Box} overlaps {bar[i].Name} {bar[i].Box}");
        }

        var top = Box(window, "TopBar");
        foreach (var (name, box) in bar)
        {
            Assert.True(box.Top >= top.Top && box.Bottom <= top.Bottom, $"{at}: {name} {box} overflows the top bar {top}");
        }

        AllInside(window, Named<Control>(window, "TopBar"), state, scale);

        // The bar settles: another layout pass moves nothing (no flip between folding the field and dropping the age).
        Settle(window);
        Assert.Equal(bar.Select(b => b.Box), _topBarControls.Where(n => Visible(window, n)).Select(n => Box(window, n)));
    }

    // Every visible text under the control lies inside the window, whole (no text runs past an edge).
    private static void AllInside(Window window, Control within, BarState state, double scale)
    {
        var outside = within.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 })
            .Select(t => (t.Text, Box: new Rect(t.TranslatePoint(new Point(0, 0), window) ?? default, t.Bounds.Size)))
            .Where(t => t.Box.Left < 0 || t.Box.Right > window.ClientSize.Width || t.Box.Bottom > window.ClientSize.Height)
            .Select(t => $"{t.Text} {t.Box}")
            .ToList();
        Assert.True(outside.Count == 0, string.Create(CultureInfo.InvariantCulture, $"{state} at {scale:0.0}: outside the window: {string.Join(", ", outside)}"));
    }

    private static readonly string[] _settledParts = ["BarGrid", "Brand", "BrandWord", "NavSegments", "Navigation", "SearchField", "SearchText", "SearchKeycap", "PillGroup", "PillProblems", "PillAge"];

    // Shows the window in a state at a width and a text scale, runs the check, and puts the scale back.
    private async Task WithBar(BarState state, double width, double scale, Action<ShellWindow> check)
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var window = await ShowIn(state);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            Settle(window);
            check(window);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            _ = _server.StatusGate?.TrySetResult();
            Settle(window);
        }
    }

    private static List<(string Name, Rect Box)> PartBoxes(Window window)
    {
        var names = _settledParts.Select(n => (Name: n, Control: Named<Control>(window, n)))
            .Concat(Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<ListBoxItem>().Select((t, i) => (Name: "Tab" + i.ToString(CultureInfo.InvariantCulture), Control: (Control)t)));
        return [.. names.Select(p => (p.Name, new Rect(p.Control.TranslatePoint(new Point(0, 0), window) ?? default, p.Control.Bounds.Size)))];
    }

    // #2547: a bar that has settled is still after one more layout pass: no part moves, and no part is left waiting for a pass.
    private static void BarHasSettled(Window window, string at)
    {
        var stale = _settledParts.Select(n => Named<Control>(window, n))
            .Concat(Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<ListBoxItem>())
            .Where(c => c.IsEffectivelyVisible && !(c.IsMeasureValid && c.IsArrangeValid))
            .Select(c => c.Name ?? c.GetType().Name)
            .ToList();
        Assert.True(stale.Count == 0, $"{at}: still waiting for a layout pass after Settle: {string.Join(", ", stale)}");

        // One UpdateLayout drains Avalonia's layout queues, so a bar that keeps measuring itself still ends still; the pass
        // count is what pins the loop (13 passes at 1280 before the fix).
        var before = PartBoxes(window);
        var topBar = Named<TopBar>(window, "TopBar");
        topBar.ResetMeasurePasses();
        topBar.InvalidateMeasure();
        window.UpdateLayout();
        Assert.True(topBar.MeasurePasses <= 2, string.Create(CultureInfo.InvariantCulture, $"{at}: the bar measured {topBar.MeasurePasses} times for one pass"));
        var after = PartBoxes(window);
        var moved = before.Zip(after, (b, a) => (b.Name, Before: b.Box, After: a.Box))
            .Where(p => Math.Abs(p.Before.X - p.After.X) > 0.5 || Math.Abs(p.Before.Y - p.After.Y) > 0.5
                || Math.Abs(p.Before.Width - p.After.Width) > 0.5 || Math.Abs(p.Before.Height - p.After.Height) > 0.5)
            .Select(p => $"{p.Name} {p.Before} -> {p.After}")
            .ToList();
        Assert.True(before.Count == after.Count && moved.Count == 0, $"{at}: moved on one more pass: {string.Join("; ", moved)}");
    }

    // A tab's content, in order: the dot, the label and the count (the item template's stack).
    private static List<Control> TabContent(ListBoxItem tab) =>
        [.. tab.GetVisualDescendants().OfType<StackPanel>().First(p => p.Children.OfType<TextBlock>().Any()).Children.Where(c => c.IsEffectivelyVisible)];

    // The empty room between the tabs' texts must not close up: the label's (or count's) right edge to the next tab's dot.
    // QA's floor: 8 px bounds. 6 px bounds measured 7 px ink at 130% (#2547 shots), under QA's 8 px ink.
    private const double TabGapFloor = 8;

    // A tab's selected (or hover) fill covers the whole tab, so its edge must stay this far from the next tab's word, or the
    // fill reads as part of that word (QA, #2547: 4 px at the tight step before).
    private const double FillClearFloor = 6;

    // #2547 (QA): at 1000, 1099 and 1280 px, text scale 1.0 and 1.3, every pill state fits: the pill ends inside the bar's
    // 16 px padding, the bar has settled, two tabs keep the gap floor, a tab's fill stays 6 px from its neighbour's word, and
    // the tabs keep their normal padding at 1.0 (the tight step is only for when normal padding can't fit). The placeholder
    // is whole at Full in every pill state, width and scale, also when Full keeps the field at its 184 px minimum (#2710).
    private static void BarFits(Window window, BarState state, double width, double scale, string at)
    {
        TopBarFits(window, state, scale);
        Assert.True(Box(window, "HealthPill").Right <= width - 16, string.Create(CultureInfo.InvariantCulture, $"{at}: the pill ends at {Box(window, "HealthPill").Right}"));

        TabsKeepTheirSpacing(window, at);

        if (!Named<Border>(window, "SearchField").Classes.Contains("compact"))
        {
            var placeholder = Named<TextBlock>(window, "SearchText");
            using var layout = new TextLayout(placeholder.Text, new Typeface(placeholder.FontFamily, placeholder.FontStyle, placeholder.FontWeight), placeholder.FontSize, foreground: null);
            Assert.True(layout.Width <= placeholder.Bounds.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"{at}: the placeholder needs {layout.Width}, laid out {placeholder.Bounds.Width}"));
        }

        if (scale == 1.0)
        {
            Assert.False(Named<ListBox>(window, "Navigation").Classes.Contains("tight"), $"{at}: the tabs went tight at text scale 1.0");
        }
    }

    // §3.2 at the 1,000 px minimum (the comment above BarState): the window keeps its width, the version-mismatch panel's
    // text stays inside it, and the state's words read in full. The bar's own fit is BarFits, run on the same window.
    private static void FitsAtTheMinimum(Window window, BarState state, double scale)
    {
        Assert.Equal(1000, window.ClientSize.Width);
        if (state == BarState.VersionMismatch)
        {
            Assert.False(Visible(window, "TopBar"));
            AllInside(window, Named<Control>(window, "VersionMismatchPanel"), state, scale);
        }

        StateWords(window, state, scale);

        Frame(window, string.Create(CultureInfo.InvariantCulture, $"min-width-{state.ToString().ToLowerInvariant()}-{scale * 100:0}"));
    }

    public static TheoryData<BarState, double> BarStatesAndWidths()
    {
        var data = new TheoryData<BarState, double>();
        foreach (var state in Enum.GetValues<BarState>())
        {
            // VersionMismatch shows VersionMismatchPanel in place of the bar, so only the minimum's checks apply to it.
            double[] widths = state == BarState.VersionMismatch ? [1000.0] : [1000.0, 1099.0, 1103.0, 1280.0];
            foreach (var width in widths)
            {
                data.Add(state, width);
            }
        }

        return data;
    }

    // One window per state and width, checked at text scale 1.0 and then at 1.3: a row used to be one window per state, width
    // and scale for each of three tests, and those 164 windows were most of this class's run (#2841). The scale was already
    // applied after the window opened, so each check sees the window it saw before; the settled check runs first, and when it
    // passes the extra layout pass it makes has moved nothing.
    [Theory]
    [MemberData(nameof(BarStatesAndWidths))]
    public Task The_top_bar_settles_and_fits_in_every_pill_state_width_and_text_scale(BarState state, double width) => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, width, 600) });
        var window = await ShowIn(state);
        var resources = Application.Current!.Resources;
        try
        {
            foreach (var scale in (double[])[1.0, 1.3])
            {
                TextScale.Apply(resources, scale);
                Settle(window);
                var at = string.Create(CultureInfo.InvariantCulture, $"{state} at {width} px, {scale:0.0}");
                if (state != BarState.VersionMismatch)
                {
                    BarHasSettled(window, at);
                    BarFits(window, state, width, scale, at);
                }

                if (width == 1000)
                {
                    FitsAtTheMinimum(window, state, scale);
                }
            }
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            _ = _server.StatusGate?.TrySetResult();
            Settle(window);
        }
    });

    // Between every two tabs: the words keep the gap floor, and either tab's fill (selected or hover covers the whole tab)
    // stays the fill floor from the other tab's word.
    private static void TabsKeepTheirSpacing(Window window, string at)
    {
        static double X(Visual v, Visual root) => (v.TranslatePoint(new Point(0, 0), root) ?? default).X;
        var tabs = Named<ListBox>(window, "Navigation").GetVisualDescendants().OfType<ListBoxItem>().ToList();
        var gaps = new List<(string Pair, double Gap)>();
        var fills = new List<(string Pair, double Clear)>();
        for (var i = 0; i + 1 < tabs.Count; i++)
        {
            var lastWord = TabContent(tabs[i]).OfType<TextBlock>().Last();
            var nextWord = TabContent(tabs[i + 1])[0];
            var wordRight = X(lastWord, window) + lastWord.Bounds.Width;
            var pair = $"{AutomationProperties.GetName(tabs[i].GetVisualDescendants().OfType<TextBlock>().First())}|{AutomationProperties.GetName(tabs[i + 1].GetVisualDescendants().OfType<TextBlock>().First())}";
            gaps.Add((pair, X(nextWord, window) - wordRight));
            fills.Add((pair + " fill->word", X(nextWord, window) - (X(tabs[i], window) + tabs[i].Bounds.Width)));
            fills.Add((pair + " word->fill", X(tabs[i + 1], window) - wordRight));
        }

        var all = string.Join(", ", gaps.Select(g => string.Create(CultureInfo.InvariantCulture, $"{g.Pair} {g.Gap:0.#}")));
        Assert.True(gaps.TrueForAll(g => g.Gap >= TabGapFloor - 0.01), string.Create(CultureInfo.InvariantCulture, $"{at}: a gap is under {TabGapFloor} px: {all}"));
        var clears = string.Join(", ", fills.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Pair} {f.Clear:0.#}")));
        Assert.True(fills.TrueForAll(f => f.Clear >= FillClearFloor - 0.01), string.Create(CultureInfo.InvariantCulture, $"{at}: a tab's fill is under {FillClearFloor} px from the next word: {clears}"));
    }

    // #2547 (ideas): the problem text's 8 px margin stays even when its cap reaches 0, so the step the bar picks counts it. Narrowing
    // the window 2 px at a time through the steps, the pill never runs into the bar's 16 px right padding.
    [Theory]
    [InlineData(BarState.DegradedShortProblem)]
    [InlineData(BarState.DegradedLongProblems)]
    public Task Narrowing_a_degraded_bar_at_130_percent_keeps_the_pill_inside_the_padding(BarState state) => OnUi(() => WithBar(state, 1100, 1.3, window =>
    {
        var past = new List<string>();
        for (var width = 1100.0; width >= 1000; width -= 2)
        {
            window.Width = width;
            Settle(window);
            var right = Box(window, "HealthPill").Right;
            if (right > width - 16 + 0.5)
            {
                past.Add(string.Create(CultureInfo.InvariantCulture, $"{width}: {right}"));
            }
        }

        Assert.True(past.Count == 0, $"{state}: the pill ran into the padding at {string.Join(", ", past)}");
    }));

    // #2547 (ideas): a bar that has to fold Refresh's word folds it before the word is ever laid out. The measure Refresh first
    // shows in learns the word's width (Remember) before Choose runs, and folds in that same measure. The stale pill's wider
    // word and age take the bar up to three measures in that one layout update.
    [Fact]
    public Task Refresh_that_first_shows_on_a_folding_bar_folds_before_its_word_is_laid_out() => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 600) });
        var window = await ShowIn(BarState.Normal);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, 1.3);
        try
        {
            Settle(window);
            var bar = Named<TopBar>(window, "TopBar");
            var shell = (ShellViewModel)window.DataContext!;
            Assert.False(Named<Button>(window, "RefreshButton").IsVisible, "Refresh shows before the bar went stale");
            bar.ResetMeasurePasses();
            var laidOut = 0.0;
            window.LayoutUpdated += (_, _) =>
            {
                // Refresh's content joins the visual tree only once Refresh shows.
                var word = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "RefreshWord");
                laidOut = Math.Max(laidOut, word is { IsEffectivelyVisible: true } ? word.Bounds.Width : 0);
            };
            _server.FailPing = true;
            _clock.Advance(ShellViewModel.StaleAfter);
            await Until(() => shell.State == ShellState.Stale);
            Settle(window);

            Assert.True(Named<Button>(window, "RefreshButton").Classes.Contains("folded"), "Refresh did not fold at 1000 px, 1.3");
            Assert.True(laidOut == 0, string.Create(CultureInfo.InvariantCulture, $"Refresh's word was laid out {laidOut} px wide before it folded"));
            Assert.True(bar.MeasurePasses <= 3, string.Create(CultureInfo.InvariantCulture, $"the bar measured {bar.MeasurePasses} times when Refresh first showed"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    });

    // #2547 (QA): Refresh's word is the last thing the bar drops; folded, Refresh keeps its name, its tooltip and its tab stop.
    [Theory]
    [InlineData(1000.0, 1.3, true)]
    [InlineData(1000.0, 1.0, false)]
    [InlineData(1280.0, 1.3, false)]
    public Task Refresh_folds_to_its_icon_only_when_nothing_else_is_left_to_drop(double width, double scale, bool folded) => OnUi(() => WithBar(BarState.Stale, width, scale, window =>
    {
        var refresh = Named<Button>(window, "RefreshButton");
        var at = string.Create(CultureInfo.InvariantCulture, $"Stale at {width} px, {scale:0.0}");
        Assert.True(refresh.IsEffectivelyVisible, $"{at}: Refresh is hidden");
        Assert.Equal(folded, refresh.Classes.Contains("folded"));
        Assert.Equal(!folded, Named<TextBlock>(window, "RefreshWord").IsEffectivelyVisible);
        Assert.Equal(Labels.Refresh, AutomationProperties.GetName(refresh));
        Assert.Equal(Labels.Refresh, ToolTip.GetTip(refresh));
        Assert.True(refresh.Focusable && refresh.IsTabStop, $"{at}: Refresh is not a tab stop");
    }));

    // With room the ⌘K field keeps its full 300 px and the pill sits at the right edge (inside the top bar's 16 px padding).
    [Fact]
    public Task With_room_the_search_field_keeps_its_full_width() => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1440, 800) });
        var window = await ShowIn(BarState.Normal);
        Settle(window);

        Assert.Equal(300, Box(window, "SearchField").Width, precision: 0);
        Assert.Equal(window.ClientSize.Width - 16, Box(window, "HealthPill").Right, precision: 0);
    });

    // §11.2: the page region's accessible name follows the page (it read "Home" on every page).
    [Fact]
    public Task The_page_region_is_named_after_the_page_shown() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        shell.GoTo(PageId.Work);
        Settle(window);
        Assert.Equal(Labels.Work, AutomationProperties.GetName(window.Get<Border>("Page")));

        shell.GoTo(PageId.Settings);
        Settle(window);
        Assert.Equal(Labels.Settings, AutomationProperties.GetName(window.Get<Border>("Page")));
    });

    // Settings sits at the same left inset as every other page, so switching pages doesn't shift the title sideways.
    [Fact]
    public Task Settings_title_sits_where_every_page_title_does() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var settings = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        Point Title() => window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.IsEffectivelyVisible && t.Classes.Contains("page-title")) is var title
            ? title.TranslatePoint(new Point(0, 0), window) ?? default : default;

        var home = Title();
        shell.GoTo(PageId.System);
        Settle(window);
        var health = Title();
        shell.GoTo(PageId.Settings);
        Settle(window);
        var onSettings = Title();

        Assert.Equal(home.X, health.X);
        Assert.Equal(home.X, onSettings.X);
        Frame(window, "settings");
    });

    // #2779: "Last event" sits outside ProcessRows but on the same 36 px rhythm (the card's 8 px stack gap made it 42).
    private static void LastEventRowKeepsTheRhythm(Window window)
    {
        var card = Named<Control>(window, "ProcessRows").Parent as Visual ?? throw new InvalidOperationException("ProcessRows has no parent");
        var api = Named<ItemsControl>(window, "ProcessRows").GetVisualDescendants().OfType<Grid>().Last(g => g.Classes.Contains("data-row"));
        var apiTop = api.TranslatePoint(default, card)!.Value.Y;
        var lastTop = Named<Control>(window, "LastEventRow").TranslatePoint(default, card)!.Value.Y;
        Assert.True(Math.Abs(lastTop - apiTop - 36) < 0.5, string.Create(CultureInfo.InvariantCulture, $"Last event starts {lastTop - apiTop} px under the API row, not 36"));
    }

    // S2 and S3 on Health: each section head's state word sits on its eyebrow's baseline, and the profile's file is a
    // path in mono like the Not running card's.
    private static void HealthHeadsAndPathsAreAligned(Window window)
    {
        Assert.Contains("mono", Named<ItemsControl>(window, "ProfileRows").GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "/home/profile.yaml").Classes, StringComparer.Ordinal);

        double Baseline(string name)
        {
            var text = Named<TextBlock>(window, name);
            return text.TranslatePoint(default, window)!.Value.Y + text.TextLayout.TextLines[0].Baseline;
        }

        // Within half a pixel at text scale 1.0 and 1.3 (the layout rounds to whole pixels).
        var resources = Application.Current!.Resources;
        try
        {
            foreach (var scale in (double[])[1.0, 1.3])
            {
                TextScale.Apply(resources, scale);
                Settle(window);
                Assert.InRange(Baseline("ProcessWord") - Baseline("ControlPlaneEyebrow"), -0.5, 0.5);
                Assert.InRange(Baseline("ProfileHeadText") - Baseline("ProfileEyebrow"), -0.5, 0.5);
            }
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    }

    private static readonly string[] _topBarControls = ["Navigation", "SearchField", "RefreshSending", "RefreshButton", "HealthPill"];

    private static Rect Box(Window window, string name)
    {
        var control = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);
        return new Rect(control.TranslatePoint(new Point(0, 0), window) ?? default, control.Bounds.Size);
    }

    [Fact]
    public Task F6_cycles_top_bar_page_and_sheet() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-1", out var link));
        shell.Navigate(link);
        Settle(window);

        string Region()
        {
            var focused = window.FocusManager?.GetFocusedElement() as Visual;
            string[] names = ["TopBar", "Page", "Sheet"];
            return names.FirstOrDefault(n => focused is not null && window.Get<Control>(n) is var r && (r == focused || r.IsVisualAncestorOf(focused))) ?? "none";
        }

        Settle(window);
        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
            Settle(window);
            seen.Add(Region());
        }

        // The window opens with focus on the page region, so the first F6 goes to the sheet.
        Assert.Equal(["Sheet", "TopBar", "Page", "Sheet"], seen);
    });

    // Owner decision F6 (2026-10-12, review run 4): on macOS ⌘⌥→ and ⌘⌥← cycle the regions forward and back, like F6
    // and Shift+F6 (F6 there is the Focus key, which reaches the app only with fn held). RegionKeysTests covers the
    // mapping on every OS; this drives the window on the platform that binds it.
    [Fact]
    public Task Command_option_arrows_cycle_the_regions_on_macOS() => OnUi(async () =>
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "⌘⌥←/→ is the macOS binding");
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-1", out var link));
        shell.Navigate(link);
        Settle(window);

        string Region()
        {
            var focused = window.FocusManager?.GetFocusedElement() as Visual;
            string[] names = ["TopBar", "Page", "Sheet"];
            return names.FirstOrDefault(n => focused is not null && window.Get<Control>(n) is var r && (r == focused || r.IsVisualAncestorOf(focused))) ?? "none";
        }

        Settle(window);
        var seen = new List<string>();
        foreach (var (key, physical) in new[] { (Key.Right, PhysicalKey.ArrowRight), (Key.Right, PhysicalKey.ArrowRight), (Key.Right, PhysicalKey.ArrowRight), (Key.Left, PhysicalKey.ArrowLeft), (Key.Left, PhysicalKey.ArrowLeft) })
        {
            window.KeyPress(key, RawInputModifiers.Meta | RawInputModifiers.Alt, physical, keySymbol: null);
            Settle(window);
            seen.Add(Region());
        }

        // The window opens with focus on the page region (as in F6_cycles_top_bar_page_and_sheet), so the first ⌘⌥→ goes to the sheet.
        Assert.Equal(["Sheet", "TopBar", "Page", "TopBar", "Sheet"], seen);
    });

    [Fact]
    public Task Command_comma_opens_Settings() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { }) };
        var focusEvents = 0;
        window.GotFocus += (_, _) => focusEvents++;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        Settle(window);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
        Assert.True(Visible(window, "SettingsSlot"));
        Assert.Equal(1, focusEvents);   // a11y-5: the chord moves focus once, into Settings
        Assert.True(Focused(window) is Visual focused && window.SettingsSlot.IsVisualAncestorOf(focused), "focus isn't in Settings");
    });

    [Fact]
    public Task Closing_remembers_the_window_and_reopening_restores_it() => OnUi(async () =>
    {
        await new Preferences(_preferences).Update(_ => UiPreferences.Defaults with { Window = new WindowBounds(40, 50, 1100, 720) });
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);
        Assert.Equal((1100d, 720d), (window.Width, window.Height));

        window.Close();

        var stored = (await new Preferences(_preferences).Load(Token)).Window;
        Assert.NotNull(stored);
        Assert.Equal((1100d, 720d), (stored.Width, stored.Height));
    });

    // W3 (a11y-0): the focus regions are control elements of type Group named for the region; a Border has no peer otherwise.
    [Fact]
    public Task The_focus_regions_are_exposed_as_named_groups() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);
        var regions = new (Control Region, string Name)[]
        {
            (window.Get<Border>("Page"), shell.PageName),
            (window.Get<Border>("Sheet"), shell.SheetName),
            (window.Get<TopBar>("TopBar").GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("focus-region")), shell.Text.Sindri),
        };

        foreach (var (region, name) in regions)
        {
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(region);
            Assert.True(peer.IsControlElement(), name);
            Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Equal(name, peer.GetName());
        }

        // testing-2: the shortcuts card is a focus region from the same style.
        shell.Shortcuts.Open();
        Settle(window);
        var card = Named<Border>(window, "ShortcutsCard");
        var cardPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(card);
        Assert.True(cardPeer.IsControlElement());
        Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Window, cardPeer.GetAutomationControlType());   // a modal layer is a dialog (U2 review), the regions behind it are groups
        Assert.False(string.IsNullOrEmpty(cardPeer.GetName()));
    });

    // W3: the element F6 and a page change focus is a control named by the page.
    [Fact]
    public Task The_focused_page_region_has_a_peer_named_by_the_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        shell.GoTo(PageId.Work);
        Settle(window);
        Assert.True(window.Get<Border>("Page").Focus(NavigationMethod.Tab));

        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement((Control)window.FocusManager!.GetFocusedElement()!);

        Assert.True(peer.IsControlElement());
        Assert.Equal(shell.PageName, peer.GetName());
    });

    // W4 (a11y-1): the sheet title's peer is named for the kind noun and subject ("Service serena"), not the bare subject.
    [Fact]
    public Task The_sheet_title_focus_target_is_text_named_for_the_kind_and_subject() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("service/serena", out var link));
        shell.Navigate(link);
        Settle(window);
        var title = window.Get<Border>("SheetTitle");

        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(title);

        Assert.True(peer.IsControlElement());
        Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Equal("Service serena", peer.GetName());
        Assert.NotEqual(shell.SheetSubject, peer.GetName(), StringComparer.Ordinal);
        Assert.Equal(shell.SheetSubject, title.GetVisualDescendants().OfType<TextBlock>().Single().Text);
    });

    // W5 (a11y-2): a page change with focus already on the Page region raises a fresh focus event on it, so the new page is
    // announced; from a control on the old page, focus lands on the region once (the existing rule).
    [Theory]
    [InlineData("Home", "System", false, 1)]
    [InlineData("System", "Home", true, 1)]
    [InlineData("Home", "Work", true, 1)]
    public Task A_page_change_with_focus_on_the_page_region_raises_one_fresh_focus_event(string from, string to, bool startOnRegion, int events) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        shell.GoTo(Enum.Parse<PageId>(from));
        Settle(window);
        var page = window.Get<Border>("Page");
        var start = startOnRegion ? page : Named<Control>(window, "HealthStrip");
        Assert.True(start.Focus(NavigationMethod.Tab));
        var raised = 0;
        window.AddHandler(InputElement.GotFocusEvent, (_, e) => raised += ReferenceEquals(e.Source, page) ? 1 : 0, RoutingStrategies.Bubble);

        shell.GoTo(Enum.Parse<PageId>(to));
        Settle(window);

        Assert.Same(page, Focused(window));
        Assert.Equal(events, raised);
    });
}
