using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using static Aw.Sindri.Ui.Tests.Context.Concepts.PinSheetFixtures;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The Pin sheet's write guards (review of #1845): the hash it was opened with, no write without one, a retry after a clash, an emptied definition, and counts arriving over edits.</summary>
public sealed class PinSheetReviewTests
{
    private static PinSheetOpen PinThis(string? hash = FileHash) => new("api", Card("billing", terms: ["payments", "invoice"]), Pin: null, [], FileHash: hash);

    private static PinSheetFixtures Billing()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests([new PinSuggestTerm("payments", 41)], []);
        return kit;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_pages_hash_nothing_can_be_written_and_the_sheet_says_so()
    {
        var kit = Billing();
        var sheet = kit.Sheet();
        await sheet.Open(PinThis(hash: null), Token);

        await sheet.Submit(Token);
        sheet.AskUnpin();
        await sheet.Unpin(Token);

        Assert.False(sheet.CanSubmit);
        Assert.Equal("Couldn't read your pins just now. Close this and open it again.", sheet.SubmitError);
        Assert.Empty(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edited_pin_without_the_hash_is_not_unpinned()
    {
        var kit = Billing();
        var sheet = kit.Sheet();
        var pin = Pin("billing", "Billing");
        await sheet.Open(new PinSheetOpen("api", Card: null, pin, [pin], FileHash: null), Token);

        sheet.AskUnpin();
        await sheet.Unpin(Token);

        Assert.Empty(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_clash_the_next_press_carries_the_hash_read_again()
    {
        var kit = Billing();
        kit.Api.ContextPinsAnswer = _ => Served(new PinsView(1, "api", "h2", "v1", [], new MapRebuildView("idle", Progress: null, RetryAt: null, Stale: false, Failed: null)));
        var sheet = kit.Sheet();
        await sheet.Open(PinThis(), Token);
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");
        await sheet.Submit(Token);
        Assert.StartsWith("The settings changed while you were editing.", sheet.SubmitError, StringComparison.Ordinal);
        Assert.True(sheet.CanSubmit);
        Assert.Equal(FileHash, Assert.Single(kit.Api.PinnedConcepts).IfHash);   // the re-read writes nothing by itself
        kit.Succeeds();

        await sheet.Submit(Token);

        Assert.Equal([FileHash, "h2"], [.. kit.Api.PinnedConcepts.Select(b => b.IfHash!)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_clash_whose_reread_fails_nothing_more_is_written()
    {
        var kit = Billing();
        kit.Api.ContextPinsAnswer = _ => new Aw.Sindri.Ui.Testing.RouteAnswer(System.Net.HttpStatusCode.NotFound, "{}");
        var sheet = kit.Sheet();
        await sheet.Open(PinThis(), Token);
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");

        await sheet.Submit(Token);
        await sheet.Submit(Token);

        Assert.Equal(("Couldn't read your pins just now. Close this and open it again.", false), (sheet.SubmitError, sheet.CanSubmit));
        _ = Assert.Single(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Clearing_an_edited_pins_definition_sends_an_empty_summary()
    {
        var kit = Billing();
        var pin = Pin("billing", "Billing") with { Summary = "Money in" };
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card: null, pin, [pin], FileHash: FileHash), Token);
        sheet.Definition = string.Empty;

        await sheet.Submit(Token);

        Assert.Equal(string.Empty, Assert.Single(Assert.Single(kit.Api.PinnedConcepts).Set!).Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_pin_with_no_definition_sends_none()
    {
        var kit = Billing();
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card: null, Pin: null, Pins: [], FileHash: FileHash), Token);
        sheet.Name = "Billing";

        await sheet.Submit(Token);

        Assert.Null(Assert.Single(Assert.Single(kit.Api.PinnedConcepts).Set!).Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_untick_made_while_the_counts_were_out_survives_them()
    {
        var sheet = new PinSheetViewModel();
        await sheet.Open(PinThis(), Token);
        sheet.Words[1].Ticked = false;

        sheet.FillTerms([new PinSuggestTerm("payments", 41), new PinSuggestTerm("invoice", 7)], []);

        Assert.Equal([("payments", true), ("invoice", false)], sheet.Words.Select(w => (w.Word, w.Ticked)));
        Assert.Equal("payments · about 41 more records", sheet.Words[0].Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_typed_before_the_terms_arrive_is_not_added_twice()
    {
        var sheet = new PinSheetViewModel();
        await sheet.Open(PinThis(), Token);
        sheet.NewWord = "Invoices";
        sheet.AddWord();

        sheet.FillTerms([new PinSuggestTerm("payments", 41), new PinSuggestTerm("invoice", 7)], []);

        Assert.Equal(["payments", "invoice"], [.. sheet.Words.Select(w => w.Word)], StringComparer.Ordinal);
    }
}
