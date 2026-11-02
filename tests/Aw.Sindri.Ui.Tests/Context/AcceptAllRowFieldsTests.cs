using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2b: a row of Accept all suggested opens into the page's name and, per action, its concept ticks (mockup B).</summary>
[Lane(TestLane.Unit)]
public sealed class AcceptAllRowFieldsTests
{
    private const string Key = "surface:week";

    private static readonly ConceptChoice[] _known = [new("approval", "Approval"), new("billing", "Billing"), new("payroll", "Payroll")];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(AddSeveralPagesViewModel Sheet, FakePageWrites Writes)> Opened()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("live-1", [], "file-1") };
        writes.Add(Key, new PageDraft(Id: null, "Week", "web", Key, Owner: false,
            [new PageActionDraft(Id: null, "Submit", "action:Week.submit", ["approval"]), new PageActionDraft(Id: null, "Copy", "action:Week.copy", ["billing"])]));
        var sheet = AddSeveralPagesViewModel.AcceptAll(writes, concept: null, [Key], _known);
        await sheet.LoadSuggested(Token);
        return (sheet, writes);
    }

    private static void Retick(AddSeveralPagesRowViewModel row)
    {
        row.IsOpen = true;
        row.Name = "Renamed";
        row.Actions[1].Ticks[1].IsTicked = false;   // Billing off
        row.Actions[1].Ticks[2].IsTicked = true;    // Payroll on
    }

    [Fact]
    public async Task An_open_row_sends_its_new_name_and_exactly_the_ticks_made()
    {
        var (sheet, _) = await Opened();
        var row = sheet.Rows[0];

        Expect.True(row.HasFields && !row.IsOpen, "a suggested page can open, and starts shut");
        Retick(row);

        Expect.True(row.IsOpen, "the row opened");
        Expect.Equal("Renamed", row.Page?.Name);
        Expect.Equal("approval", string.Join(',', row.Page?.Actions[0].Concepts ?? []));
        Expect.Equal("payroll", string.Join(',', row.Page?.Actions[1].Concepts ?? []));
        Expect.True(sheet.HasChanges, "a retick counts as a change to discard");
    }

    [Fact]
    public async Task A_clash_keeps_the_reticks()
    {
        var (sheet, writes) = await Opened();
        Retick(sheet.Rows[0]);
        writes.Answer(new PageSaved(PageSavedState.Clash));

        await sheet.SaveAll(Token);

        Expect.Equal(2, writes.SuggestedReads);
        Expect.Equal("Renamed", sheet.Rows[0].Name);
        Expect.Equal("payroll", string.Join(',', sheet.Rows[0].Page?.Actions[1].Concepts ?? []));
    }

    [Fact]
    public async Task A_pasted_block_has_no_fields_and_stays_shut()
    {
        var writes = new FakePageWrites { PreviewAnswer = new PagesPreviewRead(new PagesPreview("v1", "h", [Block()])) };
        var sheet = new AddSeveralPagesViewModel(writes, repo: null, _known) { Text = "Week" };
        await sheet.ReadList(Token);

        sheet.Rows[0].IsOpen = true;

        Expect.True(!sheet.Rows[0].HasFields && !sheet.Rows[0].IsOpen, "a paste row behaves as before");
    }

    private static PagesPreviewBlock Block() =>
        new(1, 1, "Week", Match: null, Replaces: null, SameAs: null, Ticked: true, Reason: null, [], [], new PagesPreviewPage(Id: null, "Week", "web", Surface: null, []));
}
