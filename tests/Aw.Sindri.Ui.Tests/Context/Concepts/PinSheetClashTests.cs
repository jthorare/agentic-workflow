using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using static Aw.Sindri.Ui.Tests.Context.Concepts.PinSheetFixtures;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>What the Pin sheet shows after a version clash (#1847): the pins are read again, so it says an opened pin changed in the file and checks the words against the new pins.</summary>
public sealed class PinSheetClashTests
{
    private const string Changed = "Billing changed in the file since you opened it; Save replaces it.";

    private static readonly MapRebuildView _idle = new("idle", Progress: null, RetryAt: null, Stale: false, Failed: null);

    private static void Rereads(PinSheetFixtures kit, params PinView[] pins) =>
        kit.Api.ContextPinsAnswer = _ => Served(new PinsView(1, "api", "h2", "v1", pins, _idle));

    private static async Task<PinSheetViewModel> EditBilling(PinSheetFixtures kit, PinView opened, params PinView[] reread)
    {
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card: null, opened, [opened], FileHash: FileHash), Token);
        Rereads(kit, reread);
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");
        await sheet.Submit(Token);
        return sheet;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_says_the_pin_changed_in_the_file_and_that_save_replaces_it()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, Pin("billing", "Billing", aliases: ["payments", "invoice"]));

        Assert.Equal(Changed, sheet.ChangedNote);
        Assert.True(sheet.HasChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_that_changed_only_the_features_says_the_pin_changed()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, Pin("billing", "Billing", aliases: ["payments"], features: ["feature:billing.refund"]));

        Assert.Equal(Changed, sheet.ChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_that_changed_only_the_summary_says_the_pin_changed()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, pin with { Summary = "Money in" });

        Assert.Equal(Changed, sheet.ChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_on_another_pin_has_no_changed_note()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, pin, Pin("auth", "Auth", aliases: ["login"]));

        Assert.Equal(string.Empty, sheet.ChangedNote);
        Assert.False(sheet.HasChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_another_pin_took_during_the_clash_is_refused_after_the_reread()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests([new PinSuggestTerm("payments", 41)], []);
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card("billing", terms: ["payments", "invoice"]), Pin: null, [], FileHash: FileHash), Token);
        sheet.NewWord = "ledger";
        sheet.AddWord();
        Rereads(kit, Pin("ledger", "Ledger", aliases: ["ledger", "payments"]));
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");

        await sheet.Submit(Token);

        var ledger = Assert.Single(sheet.Words, w => w.Word == "ledger");
        Assert.Equal((false, true, "Already a word of the pin Ledger (\"ledger\")"), (ledger.Ticked, ledger.IsRefused, ledger.Detail));
        var payments = Assert.Single(sheet.Words, w => w.Word == "payments");
        Assert.Equal((false, true), (payments.Ticked, payments.IsRefused));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_word_another_pin_let_go_is_free_after_the_reread()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests([new PinSuggestTerm("payments", 41)], []);
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card("billing", terms: ["payments"]), Pin: null, [Pin("auth", "Auth", aliases: ["login"])], FileHash: FileHash), Token);
        sheet.NewWord = "login";
        sheet.AddWord();
        Assert.True(Assert.Single(sheet.Words, w => w.Word == "login").IsRefused);
        Rereads(kit);
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");

        await sheet.Submit(Token);

        var login = Assert.Single(sheet.Words, w => w.Word == "login");
        Assert.Equal((false, string.Empty), (login.IsRefused, login.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_that_unpinned_it_in_the_file_says_save_pins_it_again()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, Pin("auth", "Auth", aliases: ["login"]));

        Assert.Equal("Billing was unpinned in the file since you opened it; Save pins it again.", sheet.ChangedNote);
        Assert.True(sheet.HasChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edit_pin_after_a_clash_that_renamed_it_in_the_file_says_the_pin_changed()
    {
        var kit = new PinSheetFixtures();
        var pin = Pin("billing", "Billing", aliases: ["payments"]);

        var sheet = await EditBilling(kit, pin, Pin("billing", "Payments", aliases: ["payments"]));

        Assert.Equal(Changed, sheet.ChangedNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_the_reread_leaves_alone_keeps_its_tick()
    {
        var kit = new PinSheetFixtures();
        kit.Suggests([new PinSuggestTerm("payments", 41)], []);
        var sheet = kit.Sheet();
        await sheet.Open(new PinSheetOpen("api", Card("billing", terms: ["payments", "invoice"]), Pin: null, [], FileHash: FileHash), Token);
        sheet.NewWord = "ledger";
        sheet.AddWord();
        Assert.Single(sheet.Words, w => w.Word == "payments").Ticked = false;
        var ledgerBefore = Assert.Single(sheet.Words, w => w.Word == "ledger");
        Rereads(kit, Pin("auth", "Auth", aliases: ["login"]));
        kit.Fails("SND-PROFILE-003", "the profile changed since you read it");

        await sheet.Submit(Token);

        var ledger = Assert.Single(sheet.Words, w => w.Word == "ledger");
        Assert.Same(ledgerBefore, ledger);
        Assert.True(ledger.Ticked);
        Assert.False(Assert.Single(sheet.Words, w => w.Word == "payments").Ticked);
    }
}
