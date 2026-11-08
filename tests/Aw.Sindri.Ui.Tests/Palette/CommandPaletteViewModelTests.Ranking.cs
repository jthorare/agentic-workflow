using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>Match ranking (<see cref="CommandPaletteViewModelTests"/>, continued; #1059): the best match is the top row and the selection.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    private CommandPaletteViewModel RankedPalette()
    {
        var controls = new CatalogViewModel(_clipboard, _clock);
        controls.Show(_api, _server, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("commands", "extra-approve-all-ready")), SindriApiClient.Json).Value);
        return new(controls, _pages.Add, _history.Add);
    }

    [Fact]
    public void Typing_work_selects_the_Work_page()
    {
        var palette = RankedPalette();
        palette.Open();

        palette.Query = "work";

        Assert.Equal(("go:" + PageId.Work, "go:" + PageId.Work), (palette.Selected?.Id, palette.Entries[0].Id));
        Assert.True(palette.Selected!.IsSelected);
    }

    [Fact]
    public void An_empty_query_keeps_the_sections_in_their_order()
    {
        var palette = RankedPalette();
        palette.Open();

        palette.Query = "work";
        palette.Query = string.Empty;

        Assert.Equal(["Actions", "Go to", "Sindri"], palette.Sections.Select(s => s.Heading), StringComparer.Ordinal);
    }
}
