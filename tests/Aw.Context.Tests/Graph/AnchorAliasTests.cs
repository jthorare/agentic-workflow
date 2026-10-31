using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>Concept pins spec §3.4: a pin's aliases anchor a question or a node name on its concept.</summary>
public sealed class AnchorAliasTests
{
    private static readonly ConceptPin _billing =
        ConceptPin.Of(ConceptId.From("billing"), "Billing", "Charges and refunds.", features: null, paths: ["services/billing/**"]).WithAliases(["payments", "ledger", "customer invoicing"]);

    private static ContextRepo Api() => new(ContextRepoName.From("api"), "/api") { Concepts = [_billing] };

    private static GraphSnapshot Map() => new GraphBuilder().Node("concept:billing", "Billing").Node("feature:notify.email", "Email notices").Node("feature:ledger-sync", "Ledger sync").Build();

    private static PinSet Pins(GraphSnapshot graph) => PinSet.Of([Api()], [Api()], graph);

    [Fact, Trait("Lane", "unit")]
    public void Node_ledger_resolves_to_the_billing_pin_by_its_alias()
    {
        var graph = Map();

        var node = AnchorMatch.Resolve(graph, "Ledger", pins: Pins(graph));

        Assert.Equal("concept:billing", node.Value.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_using_an_alias_anchors_on_the_pin_and_says_which_word_did()
    {
        var graph = Map();

        var anchors = AnchorMatch.FromText(graph, "how are payments refunded", Pins(graph));

        Assert.Equal(["concept:billing"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal("payments", anchors.Aliases["concept:billing"], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_beats_an_alias_of_the_same_length_and_an_alias_without_a_pin_set_matches_nothing()
    {
        var graph = Map();

        Assert.Equal(["feature:ledger-sync"], AnchorMatch.FromText(graph, "ledger sync is slow", Pins(graph)).Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Empty(AnchorMatch.FromText(graph, "how are payments refunded").Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Concept_equal_to_an_alias_names_the_pin()
    {
        var graph = Map();

        var scope = PinScope.Of([Api()], [Api()], "Customer Invoicing", graph, "v41");

        Assert.Equal("billing", scope.Value.Pin.Value);
    }
}
