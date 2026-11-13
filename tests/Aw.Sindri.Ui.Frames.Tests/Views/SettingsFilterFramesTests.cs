using System.Diagnostics;
using System.Globalization;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Sandbox;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3362: Ctrl+, puts the keyboard cursor in the Settings filter box, and Esc in the box clears it.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SettingsFilterFramesTests : FrameHost
{
    [Fact]
    public Task Command_comma_focuses_the_filter_box_and_Escape_clears_it() => OnUi(async () =>
    {
        var (window, _) = await Connected();
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        await Until(window, () => Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Ctrl+, never put focus in the Settings filter box");

        settings.Filter = "motion";
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);

        Assert.Equal(string.Empty, settings.Filter);
        Assert.True(Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Esc moved focus out of the filter box");
    });

    // The page sits in a hidden slot until Settings is the current page: only then is it laid out and in the window's visual tree.
    private static (ShellWindow Window, SettingsViewModel Settings) PutSettings(ShellWindow window, ShellViewModel shell)
    {
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        shell.GoTo(PageId.Settings);
        Settle(window);
        return (window, settings);
    }

    [Fact]
    public Task After_Command_comma_the_title_is_not_under_the_top_bar() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = PutSettings(window, shell);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        await Until(window, () => Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Ctrl+, never put focus in the Settings filter box");

        var bar = Named<Control>(window, "TopBar");
        var barBottom = bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y;
        var titleTop = Named<TextBlock>(window, "TitleText").TranslatePoint(default, window)!.Value.Y;
        var page = Named<TextBlock>(window, "TitleText").FindAncestorOfType<ScrollViewer>()!;
        Assert.Equal(0, page.Offset.Y);
        Assert.True(titleTop >= barBottom - 0.5, string.Create(CultureInfo.InvariantCulture, $"the title starts at {titleTop}, under the top bar that ends at {barBottom}"));
    });

    [Fact]
    public Task Command_comma_with_Settings_already_open_puts_focus_back_in_the_filter_box() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = PutSettings(window, shell);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        await Until(window, () => Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Ctrl+, never put focus in the Settings filter box");
        _ = Named<TextBlock>(window, "TitleText").Focus();
        Settle(window);
        Assert.False(Named<TextBox>(window, "SettingsFilterBox").IsFocused);

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");

        await Until(window, () => Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Ctrl+, with Settings open did not refocus the filter box");
    });

    [Fact]
    public Task A_filter_that_matches_nothing_hides_the_sections_and_clearing_it_brings_them_back() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var (_, settings) = PutSettings(window, shell);

        settings.Filter = "zzzz";
        Settle(window);
        Assert.False(Visible(window, "ThisAppCard"));
        Assert.False(Visible(window, "ThisAppName"));
        Assert.True(Visible(window, "SettingsNoMatch"));

        settings.Filter = string.Empty;
        Settle(window);
        Assert.True(Visible(window, "ThisAppCard"));
        Assert.True(Visible(window, "ThisAppName"));
        Assert.False(Visible(window, "SettingsNoMatch"));
    });

    [Fact]
    public Task Every_label_shown_in_This_app_matches_its_own_section() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var (_, settings) = PutSettings(window, shell);
        Settle(window);
        var labels = Named<Border>(window, "ThisAppCard").GetVisualDescendants().OfType<TextBlock>().Select(static t => t.Text).Where(static s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(labels);

        foreach (var label in labels)
        {
            settings.Filter = label!;
            Assert.True(settings.ShowsThisApp, "typing '" + label + "' hides the section that shows it");
        }
    });

    [Fact]
    public Task The_checklist_follows_the_filter() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { }, shell.Setup);
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        shell.GoTo(PageId.Settings);
        Settle(window);
        Assert.True(Named<Control>(window, "SetupChecklist").IsVisible);

        settings.Filter = "sandbox";
        Settle(window);
        Assert.False(Named<Control>(window, "SetupChecklist").IsVisible);

        settings.Filter = "checklist";
        Settle(window);
        Assert.True(Named<Control>(window, "SetupChecklist").IsVisible);

        settings.Filter = string.Empty;
        Settle(window);
        Assert.True(Named<Control>(window, "SetupChecklist").IsVisible);
    });

    [Fact]
    public Task The_no_match_state_names_the_filter_and_how_to_clear_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var (_, settings) = PutSettings(window, shell);

        settings.Filter = "zzzz";
        Settle(window);

        Assert.Equal(Labels.SettingsNoMatch("zzzz"), Named<Border>(window, "SettingsNoMatchName").GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Contains(Labels.SettingsNoMatchHint, Named<Control>(window, "SettingsNoMatch").GetVisualDescendants().OfType<TextBlock>().Select(static t => t.Text), StringComparer.Ordinal);
    });

    private async Task<(ShellWindow Window, ShellViewModel Shell, SettingsViewModel Settings, SettingsView View)> FullPage()
    {
        var (window, shell) = await Connected();
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { }, shell.Setup, shell.Health.ModelChoice,
            new SandboxSettingsViewModel(shell.Health.Sandbox))
        {
            Library = shell.LibraryRoots,
            LinearConnection = shell.LinearConnection,
            TodoNoteCard = shell.TodoNoteCard,
        };
        var view = new SettingsView { DataContext = settings };
        window.SettingsSlot.Content = view;
        shell.GoTo(PageId.Settings);
        Settle(window);
        return (window, shell, settings, view);
    }

    [Fact]
    public Task Reading_the_text_of_a_full_Settings_page_takes_a_few_milliseconds() => OnUi(async () =>
    {
        var (_, _, settings, view) = await FullPage();
        view.CollectShownText(settings);   // warm: the first walk builds the lazy parts

        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            view.CollectShownText(settings);
        }

        watch.Stop();
        var each = watch.Elapsed.TotalMilliseconds / 10;
        var line = "CollectShownText on a full Settings page: " + each.ToString("F2", CultureInfo.InvariantCulture) + " ms per walk";
        TestContext.Current.SendDiagnosticMessage(line);
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        Assert.True(each < 50, "one walk took " + each.ToString("F1", CultureInfo.InvariantCulture) + " ms");
    });

    [Fact]
    public Task A_word_inside_a_collapsed_setup_step_still_keeps_the_checklist() => OnUi(async () =>
    {
        var (window, shell, settings, view) = await FullPage();
        await Until(window, () => shell.Setup.Steps.Count > 0, "the setup steps never loaded");
        foreach (var expander in Named<Control>(window, "SetupChecklist").GetVisualDescendants().OfType<Expander>())
        {
            expander.IsExpanded = false;
        }

        Settle(window);
        view.CollectShownText(settings);
        settings.Filter = "browse";

        Assert.True(settings.ShowsSetup, "'browse' is in step 1's folder row, which is collapsed: the walk missed it");
    });

    [Fact]
    public Task A_filter_after_a_library_row_changed_sees_the_new_words() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var url = new Uri("http://127.0.0.1:7410/");
        var card = new LibraryRootsViewModel(() => url, Server, Folders, Clock);
        await card.Refresh(url, Token);
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { }) { Library = card };
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        shell.GoTo(PageId.Settings);
        Settle(window);
        settings.Filter = "zzzz";   // empty -> text: the words are read
        settings.Filter = string.Empty;

        Server.LibraryAnswer = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("library", "ok").Replace("\"unreadable\": null", "\"unreadable\": \"permission denied\"", StringComparison.Ordinal));
        await card.Refresh(url, Token);
        Settle(window);
        settings.Filter = "permission";

        Assert.True(settings.ShowsLibrary, "the filter used the words read before the row became unreadable");
    });
}
