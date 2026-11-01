using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2b: Accept all suggested is the Add several pages sheet built from the section's suggested pages, not from a paste (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AcceptAllSuggestedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (AddSeveralPagesViewModel Sheet, FakePageWrites Writes) Sheet(int pages, int owners = 0)
    {
        var writes = new FakePageWrites { Owners = owners, Profile = new ProfileWaiting("live-1", [], "file-1") };
        List<string> keys = [];
        for (var i = 1; i <= pages; i++)
        {
            var key = "surface:p" + i.ToString(CultureInfo.InvariantCulture);
            keys.Add(key);
            writes.Add(key, new PageDraft(Id: null, "Page " + i.ToString(CultureInfo.InvariantCulture), "web", key, Owner: false, [new PageActionDraft(Id: null, "Go", Action: null, ["approval"])]));
        }

        return (AddSeveralPagesViewModel.AcceptAll(writes, concept: null, keys), writes);
    }

    [Fact]
    public async Task It_lists_every_suggested_page_ticked_with_no_paste_box()
    {
        var (sheet, _) = Sheet(3);

        await sheet.LoadSuggested(Token);

        Expect.Equal("Accept all suggested", sheet.Title);
        Expect.True(!sheet.HasPasteBox, "no paste box");
        Expect.Equal(3, sheet.Rows.Count);
        Expect.True(sheet.Rows.All(static r => r.Ticked), "every row starts ticked");
        Expect.True(sheet.CanSave, "Save all can be pressed");
        Expect.True(!sheet.HasMore, "nothing is left out");
    }

    [Fact]
    public async Task It_lists_the_first_200_and_counts_the_rest()
    {
        var (sheet, _) = Sheet(205);

        await sheet.LoadSuggested(Token);

        Expect.Equal(200, sheet.Rows.Count);
        Expect.Equal("5 more: accept these first", sheet.MoreText);
        Expect.Equal("Page 1", sheet.Rows[0].Name);
    }

    [Fact]
    public async Task Rows_past_the_profiles_page_cap_stay_unticked_with_the_reason()
    {
        var (sheet, _) = Sheet(20, owners: 190);

        await sheet.LoadSuggested(Token);

        Expect.Equal(20, sheet.Rows.Count);
        Expect.Equal(10, sheet.Rows.Count(static r => r.Ticked));
        Expect.True(sheet.Rows.Skip(10).All(static r => !r.Ticked && r.Reason == "The profile holds at most 200 pages"), "the last ten say the profile is full");
    }

    [Fact]
    public async Task A_clash_reads_again_and_keeps_each_rows_name_and_tick()
    {
        var (sheet, writes) = Sheet(3);
        await sheet.LoadSuggested(Token);
        sheet.Rows[0].Name = "Renamed";
        sheet.Rows[1].Ticked = false;
        writes.Reverse = true;
        writes.Answer(new PageSaved(PageSavedState.Clash));
        writes.Profile = new ProfileWaiting("live-2", [], "file-2");

        await sheet.SaveAll(Token);

        Expect.Equal(2, writes.SuggestedReads);
        var first = sheet.Rows.Single(static r => r.Key == "surface:p1");
        var second = sheet.Rows.Single(static r => r.Key == "surface:p2");
        var third = sheet.Rows.Single(static r => r.Key == "surface:p3");
        Expect.Equal("Renamed", first.Name);
        Expect.True(first.Ticked && !second.Ticked && third.Ticked, "each row keeps its tick");
        Expect.Equal("surface:p3", sheet.Rows[0].Key);   // the re-read's order is the new order
        await sheet.SaveAll(Token);
        Expect.Equal("file-2", writes.Saves[^1].IfHash);   // the new hash, not the old one
    }

    [Fact]
    public async Task A_clash_whose_reread_fails_says_so_and_not_that_the_profile_changed()
    {
        var (sheet, writes) = Sheet(2);
        await sheet.LoadSuggested(Token);
        writes.SuggestedFails = true;
        writes.Answer(new PageSaved(PageSavedState.Clash));

        await sheet.SaveAll(Token);

        Expect.Equal(Labels.AddSeveralPagesUnreadable, sheet.Note);
    }

    [Fact]
    public async Task A_concept_page_with_no_actions_is_not_added_and_says_why()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("live", [], "file") };
        writes.Add("surface:empty", new PageDraft(Id: null, "Empty", "web", "surface:empty", Owner: false, []));
        var sheet = AddSeveralPagesViewModel.AcceptAll(writes, "concept:scheduling", ["surface:empty"]);

        await sheet.LoadSuggested(Token);

        Expect.True(!sheet.Rows[0].Ticked, "unticked");
        Expect.Equal("Not added: this page has no actions to link a concept to", sheet.Rows[0].Reason);
    }

    [Fact]
    public async Task Save_all_sends_the_ticked_pages_once_with_the_files_hash()
    {
        var (sheet, writes) = Sheet(3);
        await sheet.LoadSuggested(Token);
        sheet.Rows[1].Ticked = false;

        await sheet.SaveAll(Token);

        var call = Assert.Single(writes.Saves);
        Expect.Equal("file-1", call.IfHash);
        Expect.Equal(2, call.Save.Set.Count);
    }

    [Fact]
    public async Task Without_a_hash_save_is_off_and_the_sheet_says_why()
    {
        var (sheet, writes) = Sheet(3);
        writes.Profile = null;

        await sheet.LoadSuggested(Token);

        Expect.True(!sheet.CanSave, "Save all is off");
        Expect.Equal(Labels.PageSheetNoHash, sheet.Note);
    }
}
