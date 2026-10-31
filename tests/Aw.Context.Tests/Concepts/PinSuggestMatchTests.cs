using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;

namespace Aw.Context.Tests.Concepts;

/// <summary>The pin-suggest read's list match and search (concept pins spec §3.6, #1667 Q7d).</summary>
[Trait("Lane", "unit")]
public sealed class PinSuggestMatchTests
{
    private static List<(string? Concept, string? Name)> Match(GraphSnapshot graph, params (string Name, string? Definition)[] lines) =>
        [.. PinSuggestBuilder.Match(graph, ConceptGraph.Text, [.. lines.Select(l => new PinSuggestLine(l.Name, l.Definition))]).Select(m => (m.Concept?.Value, m.Name))];

    [Fact]
    public void A_name_with_the_same_words_as_a_label_matches_it_ignoring_a_plural()
    {
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Payments", "card").ConceptWithTerms("concept:auth", "Auth", "login").Build();

        var matched = Match(graph, ("Payment", null), ("auth", "who gets in"));

        Assert.Equal([("concept:billing", "Payments"), ("concept:auth", "Auth")], matched);
    }

    [Fact]
    public void A_name_equal_to_a_label_beats_a_concept_whose_terms_fit_the_definition_better()
    {
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "card", "money").ConceptWithTerms("concept:payments", "Payments", "wallet").Build();

        var matched = Match(graph, ("Payments", "how we take money from cards"));

        Assert.Equal([("concept:payments", "Payments")], matched);   // the label wins; the definition only counts when no label fits
    }

    [Fact]
    public void Without_a_label_match_the_concept_sharing_the_most_term_words_with_the_name_and_definition_wins()
    {
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "card", "invoice").ConceptWithTerms("concept:auth", "Auth", "login", "token").Build();

        var matched = Match(graph, ("Money", "how we take money from cards"), ("Sign in", "login with a token"));

        Assert.Equal([("concept:billing", "Billing"), ("concept:auth", "Auth")], matched);
    }

    [Fact]
    public void A_tie_goes_to_the_larger_concept_then_to_the_lower_key()
    {
        var graph = new ConceptGraph()
            .ConceptWithTerms("concept:a-small", "Small", "card").ConceptWithTerms("concept:b-large", "Large", "card").ConceptWithTerms("concept:c-large", "Also large", "card")
            .Node("feature:one", "One").Node("feature:two", "Two").Node("feature:three", "Three")
            .Member("feature:one", "concept:b-large").Member("feature:two", "concept:c-large").Member("feature:three", "concept:c-large").Member("feature:three", "concept:b-large")
            .Build();

        var matched = Match(graph, ("Money", "cards"));

        Assert.Equal([("concept:b-large", "Large")], matched);   // b-large and c-large both hold 2 members; the lower key wins, and the 1-member concept loses
    }

    [Fact]
    public void A_line_sharing_no_word_with_any_concept_has_no_match_and_other_is_never_offered()
    {
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "card").ConceptWithTerms("concept:other", "Other", "misc").Build();

        var matched = Match(graph, ("Weather", "rain and snow"), ("Other", "misc things"));

        Assert.Equal(2, matched.Count);
        Assert.All(matched, m => Assert.Null(m.Concept));
    }

    [Fact]
    public void An_empty_search_lists_the_maps_features_by_label()
    {
        var graph = new ConceptGraph().Node("feature:b", "Beta").Node("feature:a", "Alpha").Node("code:x.cs", "x").Build();

        var found = PinSuggestBuilder.Search(graph, ConceptGraph.Text, string.Empty, 50);

        Assert.Equal(["feature:a", "feature:b"], found.Select(f => f.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_search_lists_the_features_whose_label_or_key_holds_the_text_starts_first_and_stops_at_the_limit()
    {
        var graph = new ConceptGraph().Node("feature:invoices", "Invoices").Node("feature:pay-invoice", "Pay now").Node("feature:late-invoice", "Late invoice").Node("feature:invoice-pdf", "Invoice PDF").Node("feature:other", "Other").Node("code:invoice.cs", "invoice").Build();

        var found = PinSuggestBuilder.Search(graph, ConceptGraph.Text, "INVOICE", 50);

        Assert.Equal(["feature:invoice-pdf", "feature:invoices", "feature:late-invoice", "feature:pay-invoice"], found.Select(f => f.Key.Value), StringComparer.Ordinal);   // labels starting with it first, then by label
        Assert.Equal(["feature:invoice-pdf", "feature:invoices"], PinSuggestBuilder.Search(graph, ConceptGraph.Text, "invoice", 2).Select(f => f.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Matching_returns_the_words_the_alias_rule_finds_in_the_order_given_and_never_a_word_that_normalises_to_nothing()
    {
        var found = RulesLinker.Matching("Payments fail when the card is declined", ["invoice", "card", "payment", "the", "  "]);

        Assert.Equal(["card", "payment"], found, StringComparer.Ordinal);
    }
}
