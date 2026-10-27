using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The command palette and the `?` sheet in the window (UI spec §8, §11.2), against the in-memory API serving P1's
/// catalog: ⌘K / Ctrl+K and a click on the ⌘K field open it with focus in its field, ↓ and Enter run an entry, Esc
/// closes it and returns focus; `?` opens the shortcuts sheet (never while typing). Key frames palette.png,
/// palette-filtered.png and shortcuts.png go under the test output's frames/.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CommandPaletteTests : IDisposable
{
    private static readonly RawInputModifiers _command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandPaletteTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), _clipboard);
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !(shell.State == ShellState.Normal && shell.Health.Catalog.Controls.Count > 0); i++)
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

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static bool Visible(Window window, string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static Visual? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Visual;

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None, string? symbol = null)
    {
        window.KeyPress(key, modifiers, physical, symbol);
        Settle(window);
    }

    private static void Frame(Window window, string name)
    {
        Settle(window);
        FrameFile.Save(window, name);
    }

    // The entries drawn (each a button named by its label and status), in order.
    private static List<string> EntryNames(Window window) =>
        [.. Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => AutomationProperties.GetName(b) ?? "<unnamed>")];

    [Fact]
    public Task Command_K_opens_the_palette_with_focus_in_its_field_and_every_catalog_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Press(window, Key.K, PhysicalKey.K, _command, "k");

        Assert.True(shell.Palette.IsOpen);
        Assert.True(Visible(window, "CommandPalette"));
        Assert.Same(Named<TextBox>(window, "PaletteQuery"), Focused(window));
        var names = EntryNames(window);
        Assert.All(shell.Health.Catalog.Controls, c => Assert.Single(names, n => n.StartsWith(c.Label + ", ", StringComparison.Ordinal)));
        Assert.Contains("Run doctor, Copy: sindri doctor", names, StringComparer.Ordinal);
        Assert.Contains("Check launch targets again, " + Labels.CopyCli(shell.Health.Catalog.Row("ProbeLaunchTargets")!.Cli), names, StringComparer.Ordinal);   // why: the catalog serves the row
        Assert.Equal(["Actions", "Go to", "Sindri"], Named<ItemsControl>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("palette-heading")).Select(t => t.Text ?? string.Empty), StringComparer.Ordinal);
        Frame(window, "palette");
    });

    [Fact]
    public Task Typing_filters_and_Enter_copies_a_cli_only_row_then_Esc_returns_focus_to_the_field() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var field = Named<Border>(window, "SearchField");
        Assert.True(field.Focus(NavigationMethod.Tab));

        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("show line holdout");
        Settle(window);

        Assert.Equal(["Show hidden scenarios, Copy: sindri line holdout show <line> <phase>"], EntryNames(window), StringComparer.Ordinal);
        Press(window, Key.Enter, PhysicalKey.Enter);
        Assert.Equal("sindri line holdout show <line> <phase>", _clipboard.Text);
        Assert.Equal(["Show hidden scenarios, Copied"], EntryNames(window), StringComparer.Ordinal);
        Frame(window, "palette-filtered");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
        Assert.False(Visible(window, "CommandPalette"));
        Assert.Same(field, Focused(window));
    });

    [Fact]
    public Task Arrows_and_Enter_go_to_a_page_and_a_click_on_the_field_opens_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Named<Border>(window, "SearchField").RaiseEvent(new PointerPressedEventArgs(
            window, new Pointer(1, PointerType.Mouse, isPrimary: true), window, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Settle(window);
        Assert.True(shell.Palette.IsOpen);

        window.KeyTextInput("go:workflows");   // the Workflows page entry by id (the label "Workflow types" also matches a bare "workflows")
        Settle(window);
        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal("go:Workflows", shell.Palette.Selected?.Id);
        Press(window, Key.Enter, PhysicalKey.Enter);

        Assert.Equal((PageId.Workflows, false), (shell.CurrentPage, shell.Palette.IsOpen));
    });

    [Fact]
    public Task Question_mark_opens_the_shortcuts_sheet_and_Esc_closes_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var page = Named<Control>(window, "Page");
        Assert.True(page.Focus(NavigationMethod.Tab));

        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");

        Assert.True(shell.Shortcuts.IsOpen);
        Assert.True(Visible(window, "ShortcutsSheet"));
        Assert.Contains(Named<ItemsControl>(window, "ShortcutRows").GetVisualDescendants().OfType<Control>(),
            c => AutomationProperties.GetName(c) == "⌘K / Ctrl+K: Command palette");
        Frame(window, "shortcuts");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(Visible(window, "ShortcutsSheet"));
        Assert.Same(page, Focused(window));
    });

    // G5 (a11y-2): Ctrl+, / ⌘, with the palette or the ? sheet open does nothing: the card keeps focus and Settings does not
    // open behind it (the card is modal, §11.2).
    [Fact]
    public Task The_settings_chord_does_nothing_over_the_palette_or_the_sheet() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var page = shell.CurrentPage;

        Press(window, Key.K, PhysicalKey.K, _command, "k");
        Press(window, Key.OemComma, PhysicalKey.Comma, _command, ",");

        Assert.Equal((true, page), (shell.Palette.IsOpen, shell.CurrentPage));
        Assert.Same(Named<TextBox>(window, "PaletteQuery"), Focused(window));

        Press(window, Key.Escape, PhysicalKey.Escape);
        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        Press(window, Key.OemComma, PhysicalKey.Comma, _command, ",");

        Assert.Equal((true, page), (shell.Shortcuts.IsOpen, shell.CurrentPage));
        Assert.Same(Named<Control>(window, "ShortcutsCard"), Focused(window));
    });

    // §8: single keys never fire while typing in a text field.
    [Fact]
    public Task A_question_mark_typed_in_the_palette_stays_in_its_field() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.K, PhysicalKey.K, _command, "k");

        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        window.KeyTextInput("?");
        Settle(window);

        Assert.False(shell.Shortcuts.IsOpen);
        Assert.Equal("?", shell.Palette.Query);
    });

    private static async Task Until(Window window, Func<bool> condition, string what)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.True(condition(), what);
    }

    private static Button EntryButton(Window window, string name) =>
        Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) is { } n && n.StartsWith(name, StringComparison.Ordinal) && (n.Length == name.Length || n[name.Length] is ',' or ':'));

    // F1: the palette opens, and each refilter shows, at the top with its first heading in view and the first entry
    // that runs selected; only ↑/↓ scroll the list to follow the selection.
    [Fact]
    public Task The_palette_opens_and_refilters_at_the_top_and_only_the_arrows_scroll() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Press(window, Key.K, PhysicalKey.K, _command, "k");
        var scroll = Named<ScrollViewer>(window, "PaletteScroll");

        Assert.Equal(("ApproveAllRecipes", 0d), (shell.Palette.Selected?.Id, scroll.Offset.Y));   // the first enabled action by name: the catalog now has ApproveAllRecipes ready
        var heading = Named<ItemsControl>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Actions");
        Assert.True(heading.TranslatePoint(default, scroll) is { Y: >= 0 } at && at.Y < scroll.Viewport.Height, "the Actions heading is in view");
        Frame(window, "palette-top");

        for (var i = 0; i < 60 && scroll.Offset.Y <= 0; i++)
        {
            Press(window, Key.Down, PhysicalKey.ArrowDown);   // every row that runs is a stop now, so it takes more presses to leave the first screen
        }

        Assert.True(scroll.Offset.Y > 0, "the arrows bring the selection into view");

        window.KeyTextInput("o");
        Settle(window);
        Assert.Equal(0d, scroll.Offset.Y);
        Assert.Same(shell.Palette.Entries.First(e => e.IsEnabled), shell.Palette.Selected);
    });

    // F2 (provisional default): Enter on a ready entry keeps the palette open; the entry shows sending (disabled, the
    // label kept, a spinner), then done; Esc still closes it.
    [Fact]
    public Task A_ready_entry_shows_sending_then_done_in_the_open_palette() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _server.RebuildGate = new TaskCompletionSource();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("rebuild");
        Settle(window);

        Press(window, Key.Enter, PhysicalKey.Enter);

        Assert.True(shell.Palette.IsOpen);
        _clock.Advance(ControlViewModel.SendingAfter);   // G3: "Sending…" is said after 300 ms, as the control does
        Settle(window);
        var entry = EntryButton(window, "Rebuild views");
        Assert.Equal(("Rebuild views, Sending…", false), (AutomationProperties.GetName(entry), entry.IsEnabled));
        Assert.True(entry.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("entry-spinner")).IsEffectivelyVisible);
        Frame(window, "palette-sending");

        _server.RebuildGate.SetResult();
        await Until(window, () => AutomationProperties.GetName(EntryButton(window, "Rebuild views")) == "Rebuild views: done", "done");
        Assert.True(shell.Palette.IsOpen);
        Frame(window, "palette-done");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
    });

    // K2 (run 5, a11y-1 and corr-0): Esc while a command sends, with the views healthy (the Rebuild views control's only view, the stalled
    // banner, is hidden): the outcome is spoken by the shell's command-outcome region, which is in the tree, effectively visible and live.
    [Fact]
    public Task Esc_while_a_command_sends_is_spoken_by_the_shell_region_that_is_in_the_tree() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _server.RebuildGate = new TaskCompletionSource();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("rebuild");
        Settle(window);
        Press(window, Key.Enter, PhysicalKey.Enter);
        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
        Assert.False(shell.Health.Banner.HasRebuild);

        _server.RebuildGate.SetResult();
        await Until(window, () => Visible(window, "CommandOutcomeLive") && Named<TextBlock>(window, "CommandOutcomeLive").Text == "Rebuild views: done", "the outcome in the shell region");

        var region = Named<TextBlock>(window, "CommandOutcomeLive");
        Assert.True(region.IsEffectivelyVisible);
        Assert.Contains("live", region.Classes, StringComparer.Ordinal);
    });

    // F2: a refusal shows Sindri's code as the entry's status and the detail as served under it; the palette stays open.
    [Fact]
    public Task A_refused_entry_shows_sindris_code_and_detail_in_the_open_palette() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005",
            "POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it");
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("rebuild");
        Settle(window);

        Press(window, Key.Enter, PhysicalKey.Enter);
        await Until(window, () => shell.Palette.Selected?.IsFailed == true, "refused");

        Assert.True(shell.Palette.IsOpen);
        var detail = Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("entry-error-detail"));
        Assert.True(detail.IsEffectivelyVisible);
        Assert.Equal("POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it", detail.Text);
        Assert.StartsWith("Rebuild views, SND-API-005: ", AutomationProperties.GetName(EntryButton(window, "Rebuild views")), StringComparison.Ordinal);
        Frame(window, "palette-refused");
    });

    // F1 (a11y-0, a11y-1): the palette's live region is never empty; ↓ and filtering to nothing change what it says.
    [Fact]
    public Task The_palette_live_region_is_never_empty_and_follows_the_selection() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        var live = Named<TextBlock>(window, "PaletteAnnouncer");
        var texts = new List<string>();
        live.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                texts.Add(live.Text ?? string.Empty);
            }
        };

        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Assert.Contains(shell.Palette.Selected!.Name, live.Text, StringComparison.Ordinal);

        window.KeyTextInput("zzzz");
        Settle(window);
        Assert.Equal("Nothing matches", live.Text);
        Assert.All(texts, Assert.NotEmpty);
    });

    private static bool Inside(Window window, string card)
    {
        var focused = Focused(window);
        var root = Named<Control>(window, card);
        return focused is not null && (focused == root || root.IsVisualAncestorOf(focused));
    }

    // F6 (a11y-2): the overlays trap focus; F6 and Shift+F6 neither move it behind the scrim nor out of the card.
    [Fact]
    public Task F6_keeps_focus_inside_the_palette_and_the_shortcuts_sheet() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        Assert.True(Inside(window, "PaletteCard"));

        Press(window, Key.F6, PhysicalKey.F6);
        Assert.True(Inside(window, "PaletteCard"), "F6 stays in the palette");
        Press(window, Key.F6, PhysicalKey.F6, RawInputModifiers.Shift);
        Assert.True(Inside(window, "PaletteCard"), "Shift+F6 stays in the palette");
        Press(window, Key.Escape, PhysicalKey.Escape);

        Assert.True(Named<Control>(window, "Page").Focus(NavigationMethod.Tab));
        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        Assert.True(shell.Shortcuts.IsOpen);

        Press(window, Key.F6, PhysicalKey.F6);
        Assert.True(Inside(window, "ShortcutsCard"), "F6 stays in the sheet");
        Press(window, Key.F6, PhysicalKey.F6, RawInputModifiers.Shift);
        Assert.True(Inside(window, "ShortcutsCard"), "Shift+F6 stays in the sheet");
    });

    // F7 (a11y-3): one overlay at a time. Ctrl+K over the ? sheet closes the sheet and opens the palette; Esc then returns
    // focus to what had it before the sheet.
    [Fact]
    public Task Command_K_over_the_shortcuts_sheet_replaces_it_and_focus_returns_to_the_original_opener() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var page = Named<Control>(window, "Page");
        Assert.True(page.Focus(NavigationMethod.Tab));
        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        Assert.True(shell.Shortcuts.IsOpen);

        Press(window, Key.K, PhysicalKey.K, _command, "k");

        Assert.Equal((false, true), (shell.Shortcuts.IsOpen, shell.Palette.IsOpen));
        Assert.True(Inside(window, "PaletteCard"));
        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
        Assert.Same(page, Focused(window));
    });

    // F10 (a11y-7): the sheet is longer than its card; the keyboard reaches the last row (End).
    [Fact]
    public Task The_shortcuts_sheets_last_row_is_reachable_by_keyboard() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        // The card has no MaxHeight and fits the window, so a tall window can fit every row and the sheet never scrolls.
        // Size.WindowMinHeight (600) is the shortest the window gets, so the test asks for that and the rows overflow the card.
        window.Height = 600;
        Settle(window);
        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        var scroll = Named<ScrollViewer>(window, "ShortcutsScroll");

        Assert.True(Inside(window, "ShortcutsCard"));
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "the sheet is longer than its card");
        Press(window, Key.End, PhysicalKey.End);

        var last = Named<Control>(window, "ShortcutRows").GetVisualDescendants().OfType<Grid>().Last(g => g.IsEffectivelyVisible);
        var at = last.TranslatePoint(new Point(0, last.Bounds.Height), scroll);
        Assert.True(at is { } p && p.Y <= scroll.Viewport.Height + 1, "the last row is inside the viewport");
        Assert.True(shell.Shortcuts.Rows.Count > 0);
    });

    // F8 (a11y-4): the selected row is told apart by more than its PrimarySoft fill (1.1:1 on the card): a 2 px bar in
    // PrimaryHover (6.1:1 on Surface3, ContrastTests).
    [Fact]
    public Task The_selected_palette_row_has_a_two_pixel_primary_hover_bar() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        var selected = EntryButton(window, shell.Palette.Selected!.Label);

        var presenter = selected.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");

        Assert.Equal(2d, presenter.BorderThickness.Left);
        var bar = Assert.IsType<Avalonia.Media.SolidColorBrush>(presenter.BorderBrush);
        Assert.True(window.TryFindResource("Color.PrimaryHover", window.ActualThemeVariant, out var hover) && hover is Avalonia.Media.Color c && c == bar.Color);

        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal(0d, presenter.BorderThickness.Left);
    });
}
