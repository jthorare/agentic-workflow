using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1859: the concepts the page sheets offer carry the bare id a page is written with ("scheduling"), not the map key of the concept card ("concept:scheduling").</summary>
[Lane(TestLane.Unit)]
public sealed class PageSheetConceptIdTests
{
    private const string Key = "surface:billing-overview";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ContextPageViewModel Page, FakePageWrites Writes)> Shown()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("live", [], "file") };
        writes.Add(Key, new Aw.Sindri.Ui.ViewModels.Context.Whole.PageDraft(Id: null, "Billing overview", "web", Key, Owner: false,
            [new Aw.Sindri.Ui.ViewModels.Context.Whole.PageActionDraft(Id: null, "Pay", Action: null, ["scheduling"])]));
        var api = new FakeSindriApi
        {
            ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Ok([AConcepts.Concept("concept:scheduling", "Self scheduling"), AConcepts.Concept("concept:billing", "Billing")], links: [])),
        };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }, Pages: writes));
        await page.Opened(Token);
        return (page, writes);
    }

    [Fact]
    public async Task The_accept_all_row_shows_one_ticked_box_for_a_concept_its_action_expresses()
    {
        var (page, _) = await Shown();
        page.OpenAcceptAllSuggested("concept:scheduling", [Key]);
        var sheet = page.AddSeveralPages!;
        await sheet.LoadSuggested(Token);

        var ticks = sheet.Rows[0].Actions[0].Ticks;

        Expect.Sequence(["Self scheduling", "Billing"], ticks.Select(t => t.Name));
        Expect.Sequence([true, false], ticks.Select(t => t.IsTicked));
    }

    [Fact]
    public async Task The_single_page_sheet_opened_from_a_concept_ticks_the_bare_id()
    {
        var (page, _) = await Shown();
        await page.OpenConcept("concept:scheduling", Token);
        page.Whole.Pages.NewPage();
        var sheet = page.PageSheet!;
        await sheet.OpenNew(Token);

        Expect.Sequence(["scheduling", "billing"], sheet.Columns.Select(static c => c.Id));
        Expect.Sequence(["scheduling"], sheet.Actions[0].TickedIds);
    }

    [Fact]
    public async Task A_no_concept_pick_of_billing_saves_the_bare_id_on_every_action()
    {
        var (page, writes) = await Shown();
        page.OpenAcceptAllNoConcept([Key]);
        var sheet = page.AddSeveralPages!;
        await sheet.LoadSuggested(Token);

        await sheet.SaveAll(Token);

        var saved = Assert.Single(Assert.Single(writes.Saves).Save.Set);
        Expect.True(saved.Actions.All(static a => a.Concepts.Contains("billing", StringComparer.Ordinal) && !a.Concepts.Contains("concept:billing", StringComparer.Ordinal)), "billing is written as its bare id");
    }
}
