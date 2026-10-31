using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1834: a block that names a concept matching nothing gets a concept picker in its row; the block is not saved until a concept is picked (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AddSeveralPagesConceptPickerTests
{
    private const string Unknown = "No concept called \"aproval\": pick one";

    private static readonly ConceptChoice[] _known = [new("approval-workflow", "Approval workflow"), new("self-scheduling", "Self-scheduling")];

    private static PagesPreviewBlock Ok(int number, string name) =>
        new(number, number, name, Match: null, Replaces: null, SameAs: null, Ticked: true, Reason: null, [], [],
            new PagesPreviewPage(Id: null, name, "web", Surface: null, [new PagesPreviewPageAction("Submit", Action: null, ["self-scheduling"])]));

    // The server's answer for a block with an unknown concept: a reason, the unknown names, no page to send.
    private static PagesPreviewBlock Unresolved(int number, string name) =>
        new(number, number, name, Match: null, Replaces: null, SameAs: null, Ticked: false, Unknown,
            [new PagesPreviewAction("Submit", Match: null, [new PagesPreviewConcept("aproval", Id: null), new PagesPreviewConcept("self-scheduling", "self-scheduling")])],
            ["aproval"], Page: null);

    private static async Task<(AddSeveralPagesViewModel Sheet, FakePageWrites Writes)> Read(params PagesPreviewBlock[] blocks)
    {
        var writes = new FakePageWrites { PreviewAnswer = new PagesPreviewRead(new PagesPreview("v41", "hash-preview", blocks)) };
        var sheet = new AddSeveralPagesViewModel(writes, repo: null, _known) { Text = "x" };
        await sheet.ReadList(TestContext.Current.CancellationToken);
        return (sheet, writes);
    }

    [Fact]
    public async Task An_unknown_concept_shows_its_line_and_a_picker_of_the_known_concepts()
    {
        var (sheet, _) = await Read(Unresolved(1, "Billing"));

        var row = sheet.Rows[0];
        var unknown = Assert.Single(row.Unknowns);
        Expect.Equal(Unknown, unknown.Text);
        Expect.Sequence(_known, unknown.Choices);
        Expect.True(unknown.Selected is null, "nothing is picked yet");
        Expect.True(!row.CanTick, "the block waits for a concept");
        Expect.True(!row.Ticked, "and stays unticked");
    }

    [Fact]
    public async Task Picking_every_concept_ticks_the_row()
    {
        var (sheet, _) = await Read(Unresolved(1, "Billing"));
        var row = sheet.Rows[0];

        row.Unknowns[0].Selected = _known[0];

        Expect.True(row.CanTick, "a picked concept settles the row");
        Expect.True(row.Ticked, "and ticks it");
        Expect.True(sheet.CanSave, "and Save all turns on");
    }

    [Fact]
    public async Task Clearing_the_pick_unticks_the_row_again()
    {
        var (sheet, _) = await Read(Unresolved(1, "Billing"));
        var row = sheet.Rows[0];
        row.Unknowns[0].Selected = _known[0];
        row.Ticked = true;

        row.Unknowns[0].Selected = null;

        Expect.True(!row.CanTick, "no pick, no tick");
        Expect.True(!row.Ticked, "the tick goes off");
    }

    [Fact]
    public async Task Save_all_sends_the_picked_concept_in_place_of_the_unknown_name()
    {
        var (sheet, writes) = await Read(Unresolved(1, "Billing"));
        sheet.Rows[0].Unknowns[0].Selected = _known[0];
        sheet.Rows[0].Ticked = true;

        await sheet.SaveAll(TestContext.Current.CancellationToken);

        var page = Assert.Single(Assert.Single(writes.Saves).Save.Set);
        Expect.Equal("Billing", page.Name);
        Expect.Sequence(["approval-workflow", "self-scheduling"], Assert.Single(page.Actions).Concepts);
    }

    [Fact]
    public async Task A_block_with_no_pick_is_not_written()
    {
        var (sheet, writes) = await Read(Ok(1, "Schedule week"), Unresolved(2, "Billing"));

        await sheet.SaveAll(TestContext.Current.CancellationToken);

        Expect.Sequence(["Schedule week"], Assert.Single(writes.Saves).Save.Set.Select(p => p.Name));
    }

    [Fact]
    public async Task A_row_with_no_unknown_concept_has_no_picker()
    {
        var (sheet, _) = await Read(Ok(1, "Schedule week"));

        Expect.Equal(0, sheet.Rows[0].Unknowns.Count);
        Expect.True(sheet.Rows[0].Ticked, "an ordinary row is unchanged");
    }
}
