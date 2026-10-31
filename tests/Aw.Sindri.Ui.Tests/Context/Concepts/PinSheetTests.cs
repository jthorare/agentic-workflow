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
}
