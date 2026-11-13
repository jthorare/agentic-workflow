using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>#3362: running a palette Settings entry shows Settings and sets the page's filter to the entry's words.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSettingFilterTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(ShellViewModel Shell, SettingsViewModel Settings)> Open()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(_store), new FakeClipboard());
        await shell.Restore(Token);
        var settings = new SettingsViewModel(new Preferences(_store), new FakeMotionPreference(), static _ => { });
        settings.FollowPalette(handler => shell.SettingFilterRequested += handler);
        return (shell, settings);
    }

    private static Task RunTextSize(ShellViewModel shell)
    {
        shell.Palette.Open();
        return shell.Palette.Run(shell.Palette.Entries.Single(static e => string.Equals(e.Id, "setting:TextSize", StringComparison.Ordinal)), Token);
    }

    [Fact]
    public async Task Running_Text_size_shows_Settings_and_filters_it()
    {
        var (shell, settings) = await Open();

        await RunTextSize(shell);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
        Assert.Equal("text size", settings.Filter);
    }

    [Fact]
    public async Task Running_Text_size_with_Settings_already_open_still_sets_the_filter()
    {
        var (shell, settings) = await Open();
        shell.GoTo(PageId.Settings);

        await RunTextSize(shell);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
        Assert.Equal("text size", settings.Filter);
    }

    [Fact]
    public async Task Running_Text_size_replaces_a_filter_that_already_has_text()
    {
        var (shell, settings) = await Open();
        settings.Filter = "linear";

        await RunTextSize(shell);

        Assert.Equal("text size", settings.Filter);
    }
}
