using Avalonia.Controls;
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
}
