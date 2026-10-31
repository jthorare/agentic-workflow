using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>Concept pins spec §3.4: a pin's definition and aliases are read from the approved profile, over the map node's.</summary>
[Trait("Lane", "unit")]
public sealed class PinnedConceptReadTests
{
    private static ConceptGraph Map() => new ConceptGraph()
        .ConceptWithTerms("concept:billing", "Billing", "invoice", "refund")
        .ConceptWithTerms("concept:auth", "Auth", "login");

    private static PinSet Pins(ConceptGraph graph)
    {
        var billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", "Charges and refunds.", features: null, paths: ["services/billing/**"]).WithAliases(["payments", "invoice"]);
        var repo = new ContextRepo(ContextRepoName.From("api"), "/api") { Concepts = [billing] };
        return PinSet.Of([repo], [repo], graph.Build());
    }

    [Fact]
    public void A_pin_row_is_marked_pinned_and_lists_its_aliases_and_its_definition_from_the_profile()
    {
        var graph = Map();

        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), graph.Build(), ConceptGraph.Text, Pins(graph)).Value;

        var billing = view.Concepts.Single(c => c.Id.Value == "concept:billing");
        Assert.True(billing.Pinned);
        Assert.Equal(["payments", "invoice"], billing.Aliases, StringComparer.Ordinal);
        Assert.Equal("Charges and refunds.", billing.Summary);
        Assert.False(view.Concepts.Single(c => c.Id.Value == "concept:auth").Pinned);
    }

    [Fact]
    public void The_glossary_puts_a_pins_aliases_first_marked_as_the_owners()
    {
        var graph = Map();

        var view = ConceptGlossaryBuilder.Build(ConceptGraph.Basis, graph.Build(), ConceptGraph.Text, Pins(graph));

        var billing = view.Concepts.Single(c => c.Id.Value == "concept:billing");
        Assert.Equal(["payments", "invoice", "refund"], billing.Terms, StringComparer.Ordinal);
        Assert.Equal(["payments", "invoice"], billing.OwnerTerms, StringComparer.Ordinal);
        Assert.Null(view.Concepts.Single(c => c.Id.Value == "concept:auth").OwnerTerms);
    }
}
