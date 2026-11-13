using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Input;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;

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

    private static (Aw.Sindri.Ui.Views.Shell.ShellWindow Window, SettingsViewModel Settings) PutSettings(Aw.Sindri.Ui.Views.Shell.ShellWindow window)
    {
        var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), static _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        return (window, settings);
    }

    [Fact]
    public Task After_Command_comma_the_title_is_not_under_the_top_bar() => OnUi(async () =>
    {
        var (window, _) = await Connected();
        _ = PutSettings(window);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        await Until(window, () => Named<TextBox>(window, "SettingsFilterBox").IsFocused, "Ctrl+, never put focus in the Settings filter box");

        var bar = Named<Control>(window, "TopBar");
        var barBottom = bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y;
        var titleTop = Named<TextBlock>(window, "TitleText").TranslatePoint(default, window)!.Value.Y;
        Assert.True(titleTop >= barBottom - 0.5, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the title starts at {titleTop}, under the top bar that ends at {barBottom}"));
    });

    [Fact]
    public Task Command_comma_with_Settings_already_open_puts_focus_back_in_the_filter_box() => OnUi(async () =>
    {
        var (window, _) = await Connected();
        _ = PutSettings(window);
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
        var (window, _) = await Connected();
        var (_, settings) = PutSettings(window);

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
        var (window, _) = await Connected();
        var (_, settings) = PutSettings(window);
        Settle(window);
        var labels = Named<Border>(window, "ThisAppCard").GetVisualDescendants().OfType<TextBlock>().Select(static t => t.Text).Where(static s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(labels);

        foreach (var label in labels)
        {
            settings.Filter = label!;
            Assert.True(settings.ShowsThisApp, "typing '" + label + "' hides the section that shows it");
        }
    });
}
