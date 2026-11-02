using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Add several's row cases (concept pins spec §3.6 table): what each row says and whether it is ticked.</summary>
public sealed class PinSeveralRowCaseTests
{
    private static PinSeveralFixtures WithPins(params PinView[] pins) => new(pins);

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_that_is_a_pins_name_updates_that_pin_and_starts_unticked()
    {
        var sheet = await WithPins(PinSeveralFixtures.Pin("billing", "Billing")).Read("billing: Plans\nSync");

        Assert.Equal("Updates your pin Billing", sheet.Rows[0].Note);
        Assert.False(sheet.Rows[0].Ticked);
        Assert.True(sheet.Rows[0].CanTick);
        Assert.True(sheet.Rows[1].Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_name_that_is_a_pins_id_updates_that_pin()
    {
        var sheet = await WithPins(PinSeveralFixtures.Pin("self-scheduling", "Booking")).Read("Self scheduling");

        Assert.Equal("Updates your pin Booking", sheet.Rows[0].Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_line_with_the_same_name_says_which_line_and_cannot_be_ticked()
    {
        var sheet = await new PinSeveralFixtures().Read("Payments: one\nRefunds\nPayments: two\npayments  ");

        Assert.Equal(string.Empty, sheet.Rows[0].Note);
        Assert.Equal("Same name as line 1; only one is kept", sheet.Rows[2].Note);
        Assert.Equal("Same name as line 1; only one is kept", sheet.Rows[3].Note);
        Assert.False(sheet.Rows[2].Ticked);
        sheet.Rows[2].Ticked = true;
        Assert.False(sheet.Rows[2].Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Renaming_the_second_line_clears_its_note_and_lets_it_be_ticked()
    {
        var sheet = await new PinSeveralFixtures().Read("Payments: one\nPayments: two");

        sheet.Rows[1].Name = "Card payments";
        sheet.Rows[1].Ticked = true;

        Assert.Equal(string.Empty, sheet.Rows[1].Note);
        Assert.True(sheet.Rows[1].Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_with_no_name_or_a_name_over_80_cannot_be_read_keeps_its_text_and_is_never_written()
    {
        var long80 = new string('x', 81);
        var fixtures = new PinSeveralFixtures();
        var sheet = await fixtures.Read($": nothing here\n{long80}\nSync");

        Assert.Equal(["Couldn't read this line", "Couldn't read this line", string.Empty], sheet.Rows.Select(r => r.Note), StringComparer.Ordinal);
        Assert.Equal(": nothing here", sheet.Rows[0].Text);
        Assert.False(sheet.Rows[0].CanTick);
        sheet.Rows[0].Ticked = true;
        await sheet.PinAllAsync(PinSeveralFixtures.Token);
        var body = Assert.Single(fixtures.Api.PinnedConcepts);
        Assert.Equal(["Sync"], body.Set!.Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_another_pin_owns_is_unticked_with_its_reason()
    {
        var sheet = await WithPins(PinSeveralFixtures.Pin("auth", "Auth", "login")).Read("Payments: money (also: invoicing, login)");

        var login = sheet.Rows[0].Words.Single(w => w.Word == "login");
        Assert.False(login.Ticked);
        Assert.Equal("Already a word of the pin Auth (\"login\")", login.Note);
        login.Ticked = true;
        Assert.False(login.Ticked);
        Assert.True(sheet.Rows[0].Words.Single(w => w.Word == "invoicing").Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_word_over_200_new_records_is_unticked_as_too_broad_and_can_be_ticked()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Matches["Payments"] = "concept:billing";
        fixtures.Concepts["concept:billing"] = PinSeveralFixtures.Terms(new PinSuggestTerm("payment", 1240), new PinSuggestTerm("invoice", 200));

        var sheet = await fixtures.Read("Payments");

        var broad = sheet.Rows[0].Words[0];
        Assert.False(broad.Ticked);
        Assert.Equal("About 1,240 more records, likely too broad", broad.Note);
        Assert.True(sheet.Rows[0].Words[1].Ticked);
        broad.Ticked = true;
        Assert.True(broad.Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Words_after_the_20th_are_unticked_with_a_reason()
    {
        var words = string.Join(", ", Enumerable.Range(1, 22).Select(i => "word" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var sheet = await new PinSeveralFixtures().Read($"Payments (also: {words})");

        var row = sheet.Rows[0];
        Assert.Equal(22, row.Words.Count);
        Assert.All(row.Words.Take(20), w => Assert.True(w.Ticked));
        Assert.All(row.Words.Skip(20), w => Assert.False(w.Ticked));
        Assert.Equal("Only 20 words are kept; this one is left out", row.Words[21].Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_row_that_updates_a_pin_takes_only_the_words_its_line_gives()
    {
        var fixtures = WithPins(PinSeveralFixtures.Pin("billing", "Billing"));
        fixtures.Matches["Billing"] = "concept:billing";
        fixtures.Concepts["concept:billing"] = PinSeveralFixtures.WithFeatures([new("payments", 4)], new PinSuggestFeature("feature:checkout", "Checkout"));

        var sheet = await fixtures.Read("Billing: plans (also: invoicing)");

        Assert.Equal(["invoicing"], sheet.Rows[0].Words.Select(w => w.Word), StringComparer.Ordinal);
        Assert.Empty(sheet.Rows[0].Features);
    }
}
