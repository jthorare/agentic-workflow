using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' "Home as rows" switch (home spec §7.1, U1) and its preference: panels until it is turned on, kept in preferences.json as homeLayout.</summary>
[Lane(TestLane.Unit)]
public sealed class SettingsHomeLayoutTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SettingsViewModel Settings() => new(new Preferences(_store), new FakeMotionPreference(), _ => { });

    private Task<UiPreferences> Stored() => new Preferences(_store).Load(Token).AsTask();

    [Fact]
    public async Task A_first_run_shows_the_switch_off_and_writes_nothing()
    {
        using var settings = Settings();

        await settings.Load(Token);

        Assert.Equal((false, 0), (settings.HomeAsRows, _store.Writes));
    }

    [Fact]
    public async Task Turning_the_switch_on_and_off_is_kept_in_the_preferences()
    {
        using var settings = Settings();
        await settings.Load(Token);

        settings.HomeAsRows = true;
        var on = (await Stored()).HomeLayout;
        settings.HomeAsRows = false;

        Assert.Equal((UiPreferences.HomeRows, UiPreferences.HomePanels), (on, (await Stored()).HomeLayout));
    }

    [Fact]
    public async Task Load_reads_the_stored_layout_back()
    {
        await new Preferences(_store).Update(p => p with { HomeLayout = UiPreferences.HomeRows });
        using var settings = Settings();

        await settings.Load(Token);

        Assert.True(settings.HomeAsRows);
    }

    [Theory]
    [InlineData("rows", "rows")]
    [InlineData("panels", "panels")]
    [InlineData("grid", "panels")]
    public void The_file_keeps_a_known_layout_and_reads_any_other_as_panels(string written, string read)
    {
        var bytes = PreferencesJson.Write(UiPreferences.Defaults with { HomeLayout = written });

        Assert.Equal(read, PreferencesJson.Read(bytes).HomeLayout);
    }

    [Fact]
    public void A_file_from_before_the_layout_reads_as_panels() =>
        Assert.Equal(UiPreferences.HomePanels, PreferencesJson.Read("""{"version":1}"""u8).HomeLayout);
}
