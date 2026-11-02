using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>Match ranking (<see cref="CommandPaletteViewModelTests"/>, continued; #1059): the best match is the top row and the selection.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    private CommandPaletteViewModel RankedPalette() =>
        new(CatalogWithApproveAll("extra-approve-all-ready"), _pages.Add, _history.Add, startWorkflow: () => { }, approveAll: () => { });

    [Fact]
    public void Typing_work_selects_the_Work_page_not_Approve_all_workflows()
    {
        var palette = RankedPalette();
        palette.Open();

        palette.Query = "work";

        Assert.Equal(("go:" + PageId.Work, "go:" + PageId.Work), (palette.Selected?.Id, palette.Entries[0].Id));
        Assert.True(palette.Selected!.IsSelected);
    }

    [Fact]
    public void A_page_match_ranks_above_a_word_inside_an_action_and_the_closer_page_comes_first()
    {
        var palette = RankedPalette();
        palette.Open();

        palette.Query = "work";

        var ids = palette.Entries.Select(e => e.Id).ToList();
        Assert.Equal(["go:" + PageId.Work, "go:" + PageId.Workflows], ids.Take(2), StringComparer.Ordinal);
        Assert.True(ids.IndexOf("go:" + PageId.Workflows) < ids.IndexOf("ApproveAllRecipes"));
        Assert.Equal(Labels.PaletteGoTo, palette.Sections[0].Heading, StringComparer.Ordinal);
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
