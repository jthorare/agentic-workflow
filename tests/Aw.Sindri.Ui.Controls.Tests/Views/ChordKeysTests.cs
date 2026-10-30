using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>The G-chords as the window routes them (screens spec §3, rev 7 §8): gated like the other navigation, never typed into a text field.</summary>
[Lane(TestLane.Integration)]
public sealed class ChordKeysTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ChordKeysTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(ShellState state)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.State != state; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(state, shell.State);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task G_then_W_navigates_to_Workflows() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
    });

    [Fact]
    public Task Ctrl_G_does_not_start_a_chord() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G, RawInputModifiers.Control);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    // WCAG 2.4.3: a chord that switches the page hides the page the focused control was on; focus goes to the page region, which is named, so the new page is spoken.
    private async Task FocusStaysVisibleAfterTheChord(PageId from, Func<ShellWindow, Control> focusable)
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.GoTo(from);
        Dispatcher.UIThread.RunJobs();
        var control = focusable(window);
        Assert.True(control.Focus(NavigationMethod.Tab));

        Press(window, Key.G);
        Press(window, Key.H);
        for (var i = 0; i < 20 && window.FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: false }; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        var page = window.Get<Border>("Page");
        var focused = Assert.IsType<Control>(window.FocusManager?.GetFocusedElement(), exactMatch: false);
        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.True(focused.IsEffectivelyVisible, "focus is on something the user can see");
        Assert.True(focused == page || page.IsVisualAncestorOf(focused), "focus is on the page region or inside it");
    }

    [Fact]
    public Task A_chord_from_Settings_keeps_focus_on_the_new_page() => OnUi(() =>
        FocusStaysVisibleAfterTheChord(PageId.Settings, w =>
        {
            var button = new Button { Content = "Probe" };
            w.SettingsSlot.Content = button;
            return button;
        }));

    [Fact]
    public Task Typing_in_a_text_field_never_chords() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        var field = new TextBox();
        window.SettingsSlot.Content = field;
        shell.GoTo(PageId.Settings);
        Dispatcher.UIThread.RunJobs();
        _ = field.Focus();

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Settings, shell.CurrentPage);
    });

    [Fact]
    public Task The_palette_open_takes_the_keys_and_chords_nothing() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.Palette.Open();
        Dispatcher.UIThread.RunJobs();

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    // The palette's own TextBox would stop a chord by itself (a key typed into a field never chords); the ? sheet holds no field, so only the overlay gate can.
    [Fact]
    public Task The_shortcuts_sheet_open_takes_the_keys_and_chords_nothing() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.Shortcuts.Open();
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.Shortcuts.IsOpen);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    [Fact]
    public Task A_version_mismatch_card_leaves_the_page_the_last_page_and_the_chord_alone() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, shell) = await Show(ShellState.VersionMismatch);
        shell.GoTo(PageId.Context);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Context, shell.CurrentPage);
        Assert.Equal("Context", (await new Preferences(_preferences).Load(Token)).LastPage);
    });

    [Fact]
    public Task Not_running_leaves_the_page_and_the_last_page_alone() => OnUi(async () =>
    {
        var (window, shell) = await Show(ShellState.NotRunning);
        shell.GoTo(PageId.Context);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Context, shell.CurrentPage);
        Assert.Equal("Context", (await new Preferences(_preferences).Load(Token)).LastPage);
    });

    [Theory]
    [InlineData(Key.Down)]
    [InlineData(Key.Enter)]
    [InlineData(Key.D1)]
    [InlineData(Key.Escape)]
    public Task Any_other_unmodified_key_cancels_a_waiting_chord(Key other) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G);
        Assert.True(shell.IsChordPending);
        Press(window, other);

        Assert.False(shell.IsChordPending);
        Press(window, Key.W);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    // Esc is the key that ends a chord; it is spent on that, so the sheet behind the chord stays open (a11y-2).
    [Fact]
    public Task Esc_with_a_chord_waiting_only_cancels_the_chord_and_the_sheet_stays_open() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        Assert.True(DeepLink.TryParse("decision/DEC-7", out var link));
        shell.Navigate(link);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsSheetShown);
        Press(window, Key.G);
        Assert.True(shell.IsChordPending);

        Press(window, Key.Escape);

        Assert.False(shell.IsChordPending);
        Assert.True(shell.IsSheetShown);
        Press(window, Key.Escape);
        Assert.False(shell.IsSheetShown);   // with no chord waiting, Esc closes the sheet as before
    });

    // The clock fires on another thread. Advancing it there queues the wait's continuation to the UI thread, so the flag
    // changes and PropertyChanged is raised there, never on the advancing thread.
    private void AdvanceOnAnotherThread(TimeSpan by)
    {
        var thread = new Thread(() => _clock.Advance(by));
        thread.Start();
        thread.Join();
    }

    [Fact]
    public Task An_expiring_chord_raises_its_change_on_the_UI_thread() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (_, shell) = await Show(ShellState.Normal);
        var seenOn = new List<(string? Property, bool OnUi)>();
        shell.PropertyChanged += (_, e) => seenOn.Add((e.PropertyName, Dispatcher.UIThread.CheckAccess()));
        Assert.True(shell.PressKey("G"));
        seenOn.Clear();

        AdvanceOnAnotherThread(ShellViewModel.ChordWait);

        Assert.True(shell.IsChordPending);
        Assert.Empty(seenOn);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.IsChordPending);
        Assert.Contains((nameof(ShellViewModel.IsChordPending), true), seenOn);
        Assert.All(seenOn, seen => Assert.True(seen.OnUi, seen.Property));
    });

    [Fact]
    public Task A_finished_wait_queued_for_an_old_chord_cannot_end_a_newer_one() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (_, shell) = await Show(ShellState.Normal);
        Assert.True(shell.PressKey("G"));
        AdvanceOnAnotherThread(ShellViewModel.ChordWait);   // the first chord's wait is over; its continuation waits for the UI thread
        Assert.True(shell.PressKey("W"));
        Assert.True(shell.PressKey("G"));

        Dispatcher.UIThread.RunJobs();   // the old continuation runs now

        Assert.True(shell.IsChordPending);
        Assert.True(shell.PressKey("Q"));
        Assert.Equal(PageId.Work, shell.CurrentPage);
    });

    // Re-check R1: the hint sits wholly inside the top bar, so the page region's opaque background can't paint over it, and clear of the pill.
    private static void AssertInsideTheBar(Window window, Rect at, string detail)
    {
        var bar = window.Get<Control>("TopBar");
        var barAt = new Rect(bar.TranslatePoint(default, window)!.Value, bar.Bounds.Size);
        Assert.True(barAt.Contains(at), $"the hint is outside the top bar {barAt}: " + detail);
        var pill = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "HealthPill");
        Assert.False(at.Intersects(new Rect(pill.TranslatePoint(default, window)!.Value, pill.Bounds.Size)), "the hint overlaps the health pill: " + detail);
    }

    // The hint is an overlay: it takes no room in the bar, so it can be seen at the narrowest window and the largest text
    // scale. It is drawn at its desired width, inside the bar, over the ⌘K field's keycap end and clear of its text.
    [Theory]
    [InlineData(900, 1.0)]
    [InlineData(900, 1.3)]
    [InlineData(1100, 1.0)]
    [InlineData(1100, 1.3)]
    [InlineData(1280, 1.0)]
    [InlineData(1280, 1.3)]
    public Task The_chord_hint_is_drawn_at_its_width_inside_the_bar_clear_of_the_search_text(double width, double scale) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            window.Width = width;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var hint = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ChordHint");
            var search = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SearchField");
            Assert.False(hint.IsEffectivelyVisible);

            Press(window, Key.G);
            window.UpdateLayout();

            Assert.True(shell.IsChordPending);
            Assert.True(hint.IsEffectivelyVisible);
            var at = new Rect(hint.TranslatePoint(default, window)!.Value, hint.Bounds.Size);
            var field = new Rect(search.TranslatePoint(default, window)!.Value, search.Bounds.Size);
            var label = hint.GetVisualDescendants().OfType<TextBlock>().First();
            var detail = $"hint {at}, field {field}, window {window.ClientSize}";
            Assert.True(hint.Bounds.Width >= hint.DesiredSize.Width - hint.Margin.Left - hint.Margin.Right - 0.5 && hint.Bounds.Width > 0, detail);
            Assert.True(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 0.5, "the hint's text is clipped: " + detail);
            Assert.True(field.Contains(at), "the hint is not over the search field's keycap end: " + detail);
            var text = search.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "SearchText");
            Assert.False(text.IsEffectivelyVisible && at.Intersects(new Rect(text.TranslatePoint(default, window)!.Value, text.Bounds.Size)), "the hint covers the field's text: " + detail);
            Assert.True(new Rect(window.ClientSize).Contains(at), "the hint leaves the window: " + detail);

            AssertInsideTheBar(window, at, detail);
            Assert.Equal("G …", label.Text);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    });

    [Fact]
    public Task The_chord_hint_is_exposed_as_text_named_for_the_chord() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(ShellState.Normal);
        var hint = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ChordHint");
        Press(window, Key.G);

        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(hint);

        Assert.True(peer.IsControlElement());
        Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Equal("G …", peer.GetName());
    });
}
