using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>#3362: the palette lists a Settings entry per setting ("Text size"); running one shows Settings filtered to that setting.</summary>
[Lane(TestLane.Unit)]
public sealed class CommandPaletteSettingsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();
    private readonly List<string> _opened = [];
    private readonly CatalogViewModel _controls;

    public CommandPaletteSettingsTests()
    {
        _controls = new CatalogViewModel(new FakeClipboard(), _clock);
        _controls.Show(_api, _server, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CommandPaletteViewModel Palette(bool withSettings = true) =>
        new(_controls, static _ => { }, static _ => { }) { OpenSetting = withSettings ? _opened.Add : null };

    [Theory]
    [InlineData("setting:TextSize", "Text size", "text size")]
    [InlineData("setting:ReduceMotion", "Reduce motion", "reduce motion")]
    [InlineData("setting:Sandbox", "Sandbox", "sandbox")]
    public void Every_setting_has_an_entry_that_names_it_and_carries_its_filter_words(string id, string label, string words)
    {
        var palette = Palette();

        palette.Open();

        var entry = palette.Entries.Single(e => string.Equals(e.Id, id, StringComparison.Ordinal));
        Assert.Equal(label, entry.Label);
        Assert.Equal(words, entry.SettingWords);
    }

    [Fact]
    public void Typing_text_size_finds_the_text_size_setting()
    {
        var palette = Palette();
        palette.Open();

        palette.Query = "text size";

        Assert.Contains(palette.Entries, static e => string.Equals(e.Id, "setting:TextSize", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Running_a_setting_entry_shows_Settings_filtered_to_it_and_closes_the_palette()
    {
        var palette = Palette();
        palette.Open();
        var entry = palette.Entries.Single(static e => string.Equals(e.Id, "setting:TextSize", StringComparison.Ordinal));

        await palette.Run(entry, Token);

        Assert.Equal(["text size"], _opened);
        Assert.False(palette.IsOpen);
    }

    [Fact]
    public void A_palette_with_no_settings_page_lists_no_setting_entries()
    {
        var palette = Palette(withSettings: false);

        palette.Open();

        Assert.DoesNotContain(palette.Entries, static e => e.Id.StartsWith("setting:", StringComparison.Ordinal));
    }
}
