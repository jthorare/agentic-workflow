using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using static Aw.Sindri.Ui.Tests.Context.Concepts.PinSheetFixtures;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Unpin and Esc on the Pin sheet (concept pins spec §3.6): the question before Unpin, the removal, and the question before a discard.</summary>
public sealed class PinSheetUnpinTests
{
    private static async Task<(PinSheetViewModel Sheet, List<PinSheetClosedEventArgs> Closed)> Edited(PinSheetFixtures kit, PinView pin)
    {
        var sheet = kit.Sheet();
        List<PinSheetClosedEventArgs> closed = [];
        sheet.Closed += (_, e) => closed.Add(e);
        await sheet.Open(new PinSheetOpen("api", Card: null, pin, [pin]), Token);
        return (sheet, closed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unpin_asks_first_and_keeping_the_pin_puts_focus_back_on_Unpin()
    {
        var kit = new PinSheetFixtures();
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing"));
        var focus = new List<string>();
        sheet.KeepPinFocusRequested += (_, _) => focus.Add("keep");
        sheet.UnpinFocusRequested += (_, _) => focus.Add("unpin");

        sheet.AskUnpin();
        Assert.Equal("Unpin Billing? It comes back as a suggestion once the map rebuilds.", sheet.UnpinQuestion);
        Assert.Empty(kit.Api.PinnedConcepts);
        sheet.KeepPin();

        Assert.False(sheet.IsConfirmingUnpin);
        Assert.Equal(["keep", "unpin"], focus, StringComparer.Ordinal);
        Assert.Empty(closed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_never_approved_is_dropped_and_folder_rules_are_named()
    {
        var kit = new PinSheetFixtures();
        var (sheet, _) = await Edited(kit, Pin("billing", "Billing", folderRules: 2, state: PinStates.Waiting));

        sheet.AskUnpin();

        Assert.Equal("Drop the pin Billing? It was never approved, so nothing in the map changes. It also has 2 folder rules set outside the app; Unpin removes them too.", sheet.UnpinQuestion);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unpin_and_approve_removes_the_pin_with_its_folder_rules_and_closes()
    {
        var kit = new PinSheetFixtures("index.concepts.billing");
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing", folderRules: 1));
        sheet.AskUnpin();
        Assert.Equal("Unpin and approve", sheet.UnpinConfirmText);

        await sheet.Unpin(Token);

        var body = Assert.Single(kit.Api.PinnedConcepts);
        Assert.Equal(["billing"], body.Remove!, StringComparer.Ordinal);
        Assert.Equal((true, FileHash), (body.WithFolderRules, body.IfHash));
        Assert.Null(body.Set);
        Assert.Equal("h1", Assert.Single(kit.Api.ProfileApprovals).Hash);
        Assert.True(Assert.Single(closed).Written);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unpin_is_not_sent_before_its_question_is_answered()
    {
        var kit = new PinSheetFixtures();
        var (sheet, _) = await Edited(kit, Pin("billing", "Billing"));

        await sheet.Unpin(Token);

        Assert.Empty(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Esc_closes_a_sheet_nothing_was_typed_in_without_writing()
    {
        var kit = new PinSheetFixtures();
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing"));

        Assert.True(sheet.Escape());

        Assert.False(Assert.Single(closed).Written);
        Assert.Empty(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Esc_after_typing_asks_to_discard_and_a_second_Esc_keeps_editing()
    {
        var kit = new PinSheetFixtures();
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing"));
        sheet.Definition = "Money in";

        Assert.True(sheet.Escape());
        Assert.Equal(("Discard your changes?", true), (sheet.DiscardQuestion, sheet.IsConfirmingDiscard));
        Assert.Empty(closed);
        Assert.True(sheet.Escape());

        Assert.False(sheet.IsConfirmingDiscard);
        Assert.Empty(closed);
        Assert.Equal("Money in", sheet.Definition);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Discard_closes_without_writing()
    {
        var kit = new PinSheetFixtures();
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing"));
        sheet.Name = "Payments";
        _ = sheet.Escape();

        sheet.Discard();

        Assert.False(Assert.Single(closed).Written);
        Assert.Empty(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Esc_answers_the_unpin_question_as_keep()
    {
        var kit = new PinSheetFixtures();
        var (sheet, closed) = await Edited(kit, Pin("billing", "Billing"));
        sheet.AskUnpin();

        Assert.True(sheet.Escape());

        Assert.False(sheet.IsConfirmingUnpin);
        Assert.Empty(closed);
    }
}
