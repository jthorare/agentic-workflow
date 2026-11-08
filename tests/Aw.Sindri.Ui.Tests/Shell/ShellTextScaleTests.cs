using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The text-scale preference reaches the Needs-you and Workflows lists (screens §4.1: the caps at 1.3 hold in the product).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellTextScaleTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Settings wired to the shell the way the app wires them: Settings' apply callback also tells the shell.
    private (ShellViewModel Shell, SettingsViewModel Settings) Wired()
    {
        var preferences = new Preferences(_store);
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), preferences, new FakeClipboard());
        return (shell, new SettingsViewModel(preferences, new FakeMotionPreference(), shell.ApplyAppearance));
    }

    [Fact]
    public async Task The_stored_text_scale_reaches_the_list_when_settings_load()
    {
        await new Preferences(_store).Update(_ => UiPreferences.Defaults with { TextScale = 1.3 });
        var (shell, settings) = Wired();

        await settings.Load(Token);

        Assert.Equal(1.3, shell.NeedsYou.TextScale);
    }

    [Fact]
    public async Task A_change_in_settings_reaches_the_list_at_once()
    {
        var (shell, settings) = Wired();
        await settings.Load(Token);

        settings.TextScale = 1.3;
        Assert.Equal(1.3, shell.NeedsYou.TextScale);

        settings.TextScale = 1.1;
        Assert.Equal(1.1, shell.NeedsYou.TextScale);
    }
}
