using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2b: a concept's Pages section opens Accept all suggested over its suggested rows (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AcceptAllEntryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api, FakePageWrites writes)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }, Pages: writes));
    }

    [Fact]
    public void The_button_counts_the_suggested_rows_and_opens_the_sheet_with_their_keys_in_order()
    {
        var writes = new FakePageWrites();
        var page = Page(new FakeSindriApi(), writes);
        page.Whole.Pages.Show(PagesSectionTests.Scheduling());

        Expect.True(page.Whole.Pages.CanAcceptAll, "writes are wired and two rows are suggested");
        Expect.Equal("Accept all suggested (2)", page.Whole.Pages.AcceptAllText);
        page.Whole.Pages.AcceptAll();

        Expect.Equal("Accept all suggested", page.AddSeveralPages?.Title);
        Expect.True(page.AddSeveralPages?.HasPasteBox == false, "no paste box");
    }

    [Fact]
    public void The_button_asks_for_the_suggested_keys_only_in_the_sections_order()
    {
        var section = new PagesSectionViewModel { Writes = new FakePageWrites() };
        IReadOnlyList<string> asked = [];
        section.AcceptAllRequested += (_, e) => asked = e.Value;
        section.Show(PagesSectionTests.Scheduling());

        section.AcceptAll();

        Expect.Sequence(["surface:screen/week", "surface:screen/availability"], asked);
    }

    [Fact]
    public void No_button_without_writes_or_without_a_suggested_row()
    {
        var answer = PagesSectionTests.Scheduling();
        var noWrites = new PagesSectionViewModel();
        noWrites.Show(answer);
        var ownerOnly = new PagesSectionViewModel { Writes = new FakePageWrites() };
        ownerOnly.Show(answer with { Pages = ["surface:screen/time-off"] });
        var noConcept = new PagesSectionViewModel { Writes = new FakePageWrites() };
        noConcept.Show(answer, noConcept: true);
        Expect.True(!noWrites.CanAcceptAll, "no writes, no button");
        Expect.True(!ownerOnly.CanAcceptAll, "no suggested row, no button");
        Expect.True(noConcept.CanAcceptAll, "the pages in no concept have the button too");
    }

    [Fact]
    public async Task A_saved_sheet_reads_the_section_again()
    {
        var api = new FakeSindriApi();
        var writes = new FakePageWrites { Profile = new ProfileWaiting("live", [], "file") };
        writes.Add("surface:p1", new PageDraft(Id: null, "P1", Repo: null, "surface:p1", Owner: false, [new PageActionDraft(Id: null, "Approve", Action: null, ["approval"])]));
        var page = Page(api, writes);
        await page.Opened(Token);
        var before = api.WholeMapAsked.Count;
        page.OpenAcceptAllSuggested("concept:approval", ["surface:p1"]);
        var sheet = page.AddSeveralPages!;
        await sheet.LoadSuggested(Token);

        await sheet.SaveAll(Token);

        Expect.Equal(before + 1, api.WholeMapAsked.Count);
        Expect.True(page.AddSeveralPages is null, "the sheet went");
    }
}
