using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using static Aw.Sindri.Ui.Tests.Context.Concepts.PinSheetFixtures;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The Pin sheet's write (concept pins spec §3.6): one profile write, and what each way it ends does to the sheet.</summary>
public sealed class PinSheetWritesTests
{
    private static async Task<(PinSheetViewModel Sheet, List<PinSheetClosedEventArgs> Closed)> Opened(PinSheetFixtures kit, PinSheetOpen open)
    {
        var sheet = kit.Sheet();
        List<PinSheetClosedEventArgs> closed = [];
        sheet.Closed += (_, e) => closed.Add(e);
        await sheet.Open(open, Token);
        return (sheet, closed);
    }

    private static PinSheetOpen PinThis(params PinView[] pins) => new("api", Card("billing", terms: ["payments"]), Pin: null, pins);

    private static PinSheetFixtures Billing()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests([new PinSuggestTerm("payments", 41)], [new PinSuggestFeature("feature:billing.charges", "Charges")]);
        return kit;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pin_and_approve_writes_the_ticked_fields_with_the_file_hash_approves_and_closes()
    {
        var kit = Billing();
        kit.Api.ProfileAnswer = Profile("h1", "index.concepts.billing");
        var (sheet, closed) = await Opened(kit, PinThis());
        sheet.Definition = "Money in";

        await sheet.Submit(Token);

        var body = Assert.Single(kit.Api.PinnedConcepts);
        Assert.Equal(FileHash, body.IfHash);
        var set = Assert.Single(body.Set!);
        Assert.Equal(("Billing", null, "Money in"), (set.Name, set.Id, set.Summary));
        Assert.Equal(["payments"], set.Aliases);
        Assert.Equal(["feature:billing.charges"], set.Features);
        Assert.Equal("h1", Assert.Single(kit.Api.ProfileApprovals).Hash);
        Assert.True(Assert.Single(closed).Written);
        Assert.False(sheet.HasSubmitError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pin_leaves_the_pin_waiting_and_closes_when_something_else_waits()
    {
        var kit = Billing();
        kit.Api.ProfileAnswer = Profile("h1", "index.pages.schedule-week", "index.concepts.billing");
        var (sheet, closed) = await Opened(kit, PinThis());
        Assert.Equal("Pin", sheet.SubmitText);

        await sheet.Submit(Token);

        Assert.Empty(kit.Api.ProfileApprovals);
        Assert.True(Assert.Single(closed).Written);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ticked_off_word_and_feature_are_left_out_and_a_typed_word_is_in()
    {
        var kit = Billing();
        var (sheet, _) = await Opened(kit, PinThis());
        sheet.Words[0].Ticked = false;
        sheet.Features[0].Ticked = false;
        sheet.NewWord = "ledger";
        sheet.AddWord();

        await sheet.Submit(Token);

        var set = Assert.Single(Assert.Single(kit.Api.PinnedConcepts).Set!);
        Assert.Equal(["ledger"], set.Aliases);
        Assert.Empty(set.Features!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clash_keeps_the_sheet_open_with_every_field_and_says_to_press_the_button_again()
    {
        var kit = Billing();
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");
        var (sheet, closed) = await Opened(kit, PinThis());
        sheet.Name = "Payments & billing";

        await sheet.Submit(Token);

        Assert.Empty(closed);
        Assert.Equal("The settings changed while you were editing. Your changes are still here. Press Pin and approve again.", sheet.SubmitError);
        Assert.Equal(("Payments & billing", "Pin and approve", true), (sheet.Name, sheet.SubmitText, sheet.CanSubmit));
        Assert.Equal(["payments"], [.. sheet.Words.Where(w => w.Ticked).Select(w => w.Word)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_changed_after_the_sheet_opened_is_refused_and_keeps_the_typed_fields()
    {
        var kit = Billing();
        kit.Api.ContextPinsAnswer = _ => Served(new PinsView(1, "api", "h1", "v1", [], new MapRebuildView("idle", Progress: null, RetryAt: null, Stale: false, Failed: null)));
        var (sheet, closed) = await Opened(kit, PinThis());
        sheet.Definition = "Money in";
        sheet.NewWord = "ledger";
        sheet.AddWord();
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");

        await sheet.Submit(Token);

        Assert.Equal("h1", Assert.Single(kit.Api.PinnedConcepts).IfHash);
        Assert.Empty(closed);
        Assert.Equal("The settings changed while you were editing. Your changes are still here. Press Pin and approve again.", sheet.SubmitError);
        Assert.Equal(("Billing", "Money in"), (sheet.Name, sheet.Definition));
        Assert.Equal(["payments", "ledger"], [.. sheet.Words.Where(w => w.Ticked).Select(w => w.Word)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_shows_the_servers_sentence_under_the_button()
    {
        var kit = Billing();
        kit.Fails("SND-CONTEXT-172", "feature:billing.refundz is not in map v41");
        var (sheet, closed) = await Opened(kit, PinThis());

        await sheet.Submit(Token);

        Assert.Empty(closed);
        Assert.Equal("feature:billing.refundz is not in map v41", sheet.SubmitError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_still_running_after_the_wait_says_its_own_sentence()
    {
        var kit = Billing();
        kit.Runs();
        var (sheet, closed) = await Opened(kit, PinThis());

        await kit.Pump(sheet.Submit(Token));

        Assert.Empty(closed);
        Assert.Equal("The pin write is still running. Check Jobs, then try again.", sheet.SubmitError);
        Assert.False(sheet.IsWriting);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task While_the_write_runs_the_button_says_so_and_cannot_be_pressed()
    {
        var kit = Billing();
        kit.Runs();
        var (sheet, closed) = await Opened(kit, PinThis());

        var writing = sheet.Submit(Token);
        for (var i = 0; i < 20; i++)
        {
            kit.Clock.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(1, Token);
        }

        Assert.Equal(("Pinning…", false, true), (sheet.SubmitText, sheet.CanSubmit, sheet.IsWriting));
        kit.Succeeds();
        await kit.Pump(writing);
        _ = Assert.Single(closed);
        _ = Assert.Single(kit.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_keeps_the_pins_id_and_says_saving()
    {
        var kit = Billing();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);
        var (sheet, closed) = await Opened(kit, new PinSheetOpen("api", Card: null, pin, [pin]));
        sheet.Name = "Payments";

        await sheet.Submit(Token);

        var set = Assert.Single(Assert.Single(kit.Api.PinnedConcepts).Set!);
        Assert.Equal(("Payments", "billing"), (set.Name, set.Id));
        _ = Assert.Single(closed);
        Assert.Equal("Save and approve", sheet.SubmitText);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_is_needed_and_nothing_is_sent_without_one()
    {
        var kit = Billing();
        var (sheet, closed) = await Opened(kit, new PinSheetOpen("api", Card: null, Pin: null, Pins: []));

        await sheet.Submit(Token);

        Assert.Empty(kit.Api.PinnedConcepts);
        Assert.Empty(closed);
        Assert.Equal("Give the pin a name.", sheet.SubmitError);
        Assert.False(sheet.CanSubmit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Closing_while_the_write_waits_throws_nothing()
    {
        var kit = Billing();
        kit.Runs();
        var (sheet, closed) = await Opened(kit, PinThis());
        using var cancel = new CancellationTokenSource();

        var writing = sheet.Submit(cancel.Token);
        await cancel.CancelAsync();
        await kit.Pump(writing);

        Assert.Empty(closed);
        Assert.False(sheet.IsWriting);
    }
}
