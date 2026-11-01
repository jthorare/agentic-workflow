using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2b: Accept all suggested over the pages in no concept gives each row a concept picker, pre-filled from the page's name (pages spec §3.2, §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AcceptAllNoConceptTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly ConceptChoice[] _known = [new("concept:scheduling", "Schedules", Pages: 3), new("concept:billing", "Billing", Pages: 5)];

    private static PageDraft Page(string name, int actions = 2) =>
        new(Id: null, name, "web", "surface:" + name.Replace(' ', '-'), Owner: false,
            [.. Enumerable.Range(1, actions).Select(i => new PageActionDraft(Id: null, "Action " + i.ToString(CultureInfo.InvariantCulture), Action: null, []))]);

    private static async Task<(AddSeveralPagesViewModel Sheet, FakePageWrites Writes)> Opened(IReadOnlyList<ConceptChoice> known, params PageDraft[] pages)
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("live", [], "file") };
        foreach (var page in pages)
        {
            writes.Add(page.Surface!, page);
        }

        var sheet = AddSeveralPagesViewModel.AcceptAllNoConcept(writes, [.. pages.Select(static p => p.Surface!)], known);
        await sheet.LoadSuggested(Token);
        return (sheet, writes);
    }

    [Fact]
    public async Task A_name_word_prefills_the_concept_and_ticks_the_row()
    {
        var (sheet, _) = await Opened(_known, Page("Schedule week"));

        var row = sheet.Rows[0];
        Expect.Equal("concept:scheduling", row.Picker?.Selected?.Id);
        Expect.True(row.Ticked, "a picked row is ticked");
    }

    [Fact]
    public async Task A_related_word_is_not_a_match_there_is_no_stemming()
    {
        ConceptChoice[] scheduling = [new("concept:scheduling", "Scheduling", Pages: 3)];

        var (sheet, _) = await Opened(scheduling, Page("Weekly schedule"));

        Expect.True(sheet.Rows[0].Picker is { Selected: null } && !sheet.Rows[0].Ticked, "schedule and scheduling are different words, as in a paste");
    }

    [Fact]
    public async Task A_tie_goes_to_the_concept_with_more_pages_then_to_the_first()
    {
        ConceptChoice[] tied = [new("concept:board", "Schedule board", Pages: 2), new("concept:queue", "Schedule queue", Pages: 9), new("concept:other", "Schedule other", Pages: 9)];

        var (sheet, _) = await Opened(tied, Page("Weekly schedule"));

        Expect.Equal("concept:queue", sheet.Rows[0].Picker?.Selected?.Id);
    }

    [Fact]
    public async Task No_matching_word_leaves_the_picker_empty_and_the_row_unticked()
    {
        var (sheet, _) = await Opened(_known, Page("Misc"));

        Expect.True(sheet.Rows[0].Picker is { Selected: null }, "empty picker");
        Expect.True(!sheet.Rows[0].Ticked, "unticked");
    }

    [Fact]
    public async Task None_unticks_the_row_and_says_why()
    {
        var (sheet, _) = await Opened(_known, Page("Weekly schedule"));

        sheet.Rows[0].Picker!.Selected = sheet.Rows[0].Picker!.Choices[0];

        Expect.True(!sheet.Rows[0].Ticked, "unticked");
        Expect.Equal("Not added: no concept picked", sheet.Rows[0].Reason);
    }

    [Fact]
    public async Task A_page_with_no_actions_is_not_added_and_has_no_picker()
    {
        var (sheet, _) = await Opened(_known, Page("Weekly schedule", actions: 0));

        Expect.True(sheet.Rows[0].Picker is null && !sheet.Rows[0].Ticked, "no picker, unticked");
        Expect.Equal("Not added: this page has no actions to link a concept to", sheet.Rows[0].Reason);
    }

    [Fact]
    public async Task Save_writes_the_pick_onto_every_action()
    {
        var (sheet, writes) = await Opened(_known, Page("Weekly schedule", actions: 3));

        await sheet.SaveAll(Token);

        var saved = Assert.Single(Assert.Single(writes.Saves).Save.Set);
        Expect.True(saved.Actions.All(static a => a.Concepts.SequenceEqual(["concept:scheduling"], StringComparer.Ordinal)), "each action carries the pick");
    }

    [Fact]
    public async Task A_clash_keeps_a_changed_pick()
    {
        var (sheet, writes) = await Opened(_known, Page("Weekly schedule"));
        sheet.Rows[0].Picker!.Selected = sheet.Rows[0].Picker!.Choices.Single(static c => c.Id == "concept:billing");
        writes.Answer(new PageSaved(PageSavedState.Clash));

        await sheet.SaveAll(Token);

        Expect.Equal(2, writes.SuggestedReads);
        Expect.Equal("concept:billing", sheet.Rows[0].Picker?.Selected?.Id);
    }

    [Fact]
    public async Task The_concept_row_carries_its_page_count()
    {
        var view = AConcepts.Ok([AConcepts.Concept("concept:billing", "Billing") with { Pages = 4 }], links: []);
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(view) };

        var read = await api.ContextConcepts(new Uri("http://127.0.0.1:1/"), new ConceptsQuery("repo"), Token);

        Expect.Equal(4, read.Value.Concepts?[0].Pages);
    }
}
