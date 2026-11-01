using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Add several, reading the list (concept pins spec §3.6 "Add several", phase Q7b): the split rows, the one suggest call, the words and features of each matched concept.</summary>
public sealed class PinSeveralReadTests
{
    [Fact, Trait("Lane", "unit")]
    public void Read_the_list_is_disabled_on_a_blank_box()
    {
        var sheet = new PinSeveralFixtures().Sheet();

        Assert.False(sheet.CanRead);
        sheet.Text = "  \n";
        Assert.False(sheet.CanRead);
        sheet.Text = "Billing";
        Assert.True(sheet.CanRead);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Read_the_list_makes_one_row_per_concept_showing_how_it_split()
    {
        var sheet = await new PinSeveralFixtures().Read("Payments: How we take money\n\n- Refunds\nPutting money back");

        Assert.Equal(["Name: Payments · Definition: How we take money", "Name: Refunds · Definition: Putting money back"], sheet.Rows.Select(r => r.SplitText), StringComparer.Ordinal);
        Assert.Equal(["line 1", "line 2"], sheet.Rows.Select(r => r.LineText), StringComparer.Ordinal);
        Assert.All(sheet.Rows, r => Assert.True(r.Ticked));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_split_is_fixed_in_its_row_by_editing_the_name_and_definition()
    {
        var sheet = await new PinSeveralFixtures().Read("Payments: How we take money");

        var row = sheet.Rows[0];
        row.Name = "Payment flow";
        row.Definition = "Taking money";

        Assert.Equal("Name: Payment flow · Definition: Taking money", row.SplitText);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Read_the_list_asks_for_every_readable_line_in_one_call_and_pre_picks_each_match()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Matches["Payments"] = "concept:billing";
        fixtures.Matches["Refunds"] = "concept:refunds";

        var sheet = await fixtures.Read("Payments: money\nRefunds\n: nothing here\nSync");

        var asked = Assert.Single(fixtures.Api.ContextPinSuggestAsked, q => q.Lines is not null);
        Assert.Equal(["Payments", "Refunds", "Sync"], asked.Lines!.Select(l => l.Name), StringComparer.Ordinal);
        Assert.Equal(["Billing", "Refunds", "New, no match yet", "New, no match yet"], sheet.Rows.Select(r => r.SelectedChoice.Name), StringComparer.Ordinal);
        Assert.Equal("line 1, Payments, matched to Billing", sheet.Rows[0].AccessibleName);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_matched_row_gets_its_concepts_words_with_counts_and_its_features_ticked()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Matches["Payments"] = "concept:billing";
        fixtures.Concepts["concept:billing"] = PinSeveralFixtures.WithFeatures([new("payments", 41), new("invoice", 7)], new PinSuggestFeature("feature:checkout", "Checkout"));

        var sheet = await fixtures.Read("Payments: money (also: ledger)");

        var row = sheet.Rows[0];
        Assert.Equal(["ledger", "payments", "invoice"], row.Words.Select(w => w.Word), StringComparer.Ordinal);
        Assert.Equal("payments · about 41 more records", row.Words[1].Text);
        Assert.All(row.Words, w => Assert.True(w.Ticked));
        Assert.Equal("feature:checkout", Assert.Single(row.Features).Key);
        Assert.True(row.Features[0].Ticked);
        Assert.Equal(1, fixtures.Api.ContextPinSuggestAsked.Count(q => q.Concept == "concept:billing"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_two_rows_matched_is_read_once()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Matches["Payments"] = "concept:billing";
        fixtures.Matches["Invoices"] = "concept:billing";
        fixtures.Concepts["concept:billing"] = PinSeveralFixtures.Terms(new PinSuggestTerm("payments", 1));

        var sheet = await fixtures.Read("Payments\nInvoices");

        Assert.Equal(1, fixtures.Api.ContextPinSuggestAsked.Count(q => q.Concept is not null));
        Assert.Equal("Also matched to Billing; pick another or New", sheet.Rows[1].MatchNote);
        Assert.Equal(string.Empty, sheet.Rows[0].MatchNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_read_that_fails_says_its_words_could_not_be_read()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Matches["Payments"] = "concept:billing";

        var sheet = await fixtures.Read("Payments");

        Assert.Equal("Couldn't read this concept's words just now. Type them, or try again later.", sheet.Rows[0].WordsNote);
        Assert.Equal("Billing", sheet.Rows[0].SelectedChoice.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_match_leaves_every_row_new_and_says_they_can_still_be_pinned()
    {
        var fixtures = new PinSeveralFixtures { MatchFails = true };
        fixtures.Matches["Payments"] = "concept:billing";

        var sheet = await fixtures.Read("Payments\nRefunds");

        Assert.All(sheet.Rows, r => Assert.Equal("New, no match yet", r.SelectedChoice.Name));
        Assert.All(sheet.Rows, r => Assert.Equal("Couldn't match against the map just now; you can still pin them as new.", r.MatchNote));
        Assert.True(sheet.CanSubmit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_rows_say_matching_and_the_buttons_wait_while_the_suggest_read_runs()
    {
        var fixtures = new PinSeveralFixtures();
        var sheet = fixtures.Sheet();
        sheet.Text = "Payments\nRefunds";
        var during = new List<string>();
        fixtures.Api.ContextPinSuggestAnswer = _ =>
        {
            during.AddRange(sheet.Rows.Select(r => r.MatchNote));
            during.Add($"submit={sheet.CanSubmit} read={sheet.CanRead}");
            return AConcepts.Served(AConcepts.Matched(null, null));
        };

        await sheet.ReadListAsync(PinSeveralFixtures.Token);

        Assert.Equal(["Matching your list…", "Matching your list…", "submit=False read=False"], during, StringComparer.Ordinal);
        Assert.All(sheet.Rows, r => Assert.Equal(string.Empty, r.MatchNote));
        Assert.True(sheet.CanSubmit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changing_the_picker_reads_that_concept_and_refills_the_row()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Concepts["concept:refunds"] = PinSeveralFixtures.WithFeatures([new("refund", 3)], new PinSuggestFeature("feature:refunds", "Refunds"));
        var sheet = await fixtures.Read("Payments (also: ledger)");

        var row = sheet.Rows[0];
        row.SelectedChoice = row.Choices.Single(c => c.Key == "concept:refunds");
        await sheet.RepickAsync(row, PinSeveralFixtures.Token);

        Assert.Equal(["ledger", "refund"], row.Words.Select(w => w.Word), StringComparer.Ordinal);
        Assert.Equal("feature:refunds", Assert.Single(row.Features).Key);
        Assert.Equal("line 1, Payments, matched to Refunds", row.AccessibleName);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_picker_offers_the_derived_concepts_but_not_other_or_a_concept_a_person_set()
    {
        var fixtures = new PinSeveralFixtures
        {
            Layer = [PinSeveralFixtures.Card("concept:billing", "Billing"), PinSeveralFixtures.Card("concept:other", "Other"), new ConceptTileViewModel("concept:mine", "Mine", string.Empty, new ConceptSize(1, 1, 1, 1), setByPerson: true, terms: null, _ => { })],
        };

        var sheet = await fixtures.Read("Payments");

        Assert.Equal(["New, no match yet", "Billing"], sheet.Rows[0].Choices.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_list_over_100_lines_reads_the_first_100_and_says_so()
    {
        var sheet = await new PinSeveralFixtures().Read(string.Join('\n', Enumerable.Range(1, 101).Select(i => "Concept " + i.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Equal(100, sheet.Rows.Count);
        Assert.Equal("Up to 100 at a time; the first 100 are read.", sheet.TooManyNote);
    }
}
