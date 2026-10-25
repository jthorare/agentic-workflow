using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>The Approve all workflows entry (<see cref="CommandPaletteViewModelTests"/>, continued): the palette opens the dialog and approves nothing itself.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    // The ready row is spec-shaped (the engine's catalog does not list it yet): PROVISIONAL.
    private CatalogViewModel CatalogWithApproveAll(string scenario)
    {
        var controls = new CatalogViewModel(_clipboard, _clock);
        controls.Show(_api, _server, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("commands", scenario)), SindriApiClient.Json).Value);
        return controls;
    }

    [Fact]
    public async Task Approve_all_opens_the_dialog_and_closes_the_palette_when_the_catalog_says_ready()
    {
        var opened = 0;
        var palette = new CommandPaletteViewModel(CatalogWithApproveAll("extra-approve-all-ready"), _pages.Add, _history.Add, approveAll: () => opened++);
        palette.Open();
        var entry = palette.Entries.Single(e => e.Id == "ApproveAllRecipes");

        await palette.Run(entry, Token);

        Assert.Equal((true, "Approve all workflows", 1, false), (entry.IsEnabled, entry.Label, opened, palette.IsOpen));
        Assert.Equal(["ApproveAllRecipes"], _history);
    }

    [Fact]
    public void Approve_all_without_a_dialog_to_open_is_not_runnable()
    {
        var palette = new CommandPaletteViewModel(CatalogWithApproveAll("extra-approve-all-ready"), _pages.Add, _history.Add);
        palette.Open();

        Assert.False(palette.Entries.Single(e => e.Id == "ApproveAllRecipes").IsEnabled);
    }
}
