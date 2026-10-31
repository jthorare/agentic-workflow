using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The Pin sheet (concept pins spec §3.6, phase Q7b).</summary>
public sealed class PinSheetTests
{
    [Fact, Trait("Lane", "unit")]
    public void Pin_this_prefills_name_summary_terms_and_features_from_the_card()
    {
        var card = new ConceptTileViewModel("billing", "Billing", "How we take money", new ConceptSize(2, 3, 1, 40), setByPerson: false, terms: ["payments", "invoice"], _ => { });

        var sheet = PinSheetViewModel.ForCard(card, [new PinSuggestTerm("payments", 41), new PinSuggestTerm("invoice", 7)], [new PinSuggestFeature("feature:checkout", "Checkout"), new PinSuggestFeature("feature:refunds", "Refunds")]);

        Assert.Equal("Billing", sheet.Name);
        Assert.Equal("How we take money", sheet.Definition);
        Assert.Equal([new PinWordRow("payments", 41, Ticked: true), new PinWordRow("invoice", 7, Ticked: true)], sheet.Words);
        Assert.Equal([new PinFeatureRow("feature:checkout", "Checkout", Ticked: true), new PinFeatureRow("feature:refunds", "Refunds", Ticked: true)], sheet.Features);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pin_this_fills_at_most_20_words_and_unticks_one_that_would_link_over_200_records()
    {
        var card = new ConceptTileViewModel("billing", "Billing", string.Empty, new ConceptSize(0, 0, 0, 0), setByPerson: false, terms: null, _ => { });
        PinSuggestTerm[] terms = [new("payment", 1240), .. Enumerable.Range(1, 25).Select(i => new PinSuggestTerm("word" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), i))];

        var sheet = PinSheetViewModel.ForCard(card, terms, []);

        Assert.Equal(20, sheet.Words.Count);
        Assert.Equal(new PinWordRow("payment", 1240, Ticked: false), sheet.Words[0]);
        Assert.All(sheet.Words.Skip(1), w => Assert.True(w.Ticked));
    }
}
