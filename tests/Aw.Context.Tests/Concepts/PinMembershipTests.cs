using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Concepts;

/// <summary>What the concepts reads of two map versions say a pin gained and lost (the pins read's 'moved' field).</summary>
[Trait("Lane", "unit")]
public sealed class PinMembershipTests
{
    private static ConceptRead Read(ConceptGraph graph)
    {
        var billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", "Charges.", features: null, paths: ["billing/**"]);
        var repo = new ContextRepo(ContextRepoName.From("api"), "/api") { Concepts = [billing] };
        var built = graph.Build();
        return new ConceptRead(ConceptGraph.Basis, built, ConceptGraph.Text) { Pins = PinSet.Of([repo], [repo], built) };
    }

    private static ConceptGraph Map() => new ConceptGraph().Concept("concept:billing", "Billing").Concept("concept:refunds", "Refunds")
        .Node("feature:invoices").Node("feature:charges").Node("feature:credits");

    [Fact]
    public void A_feature_that_left_the_pin_for_another_concept_is_lost_and_named_where_it_went()
    {
        var before = Read(Map().Member("feature:invoices", "concept:billing").Member("feature:charges", "concept:billing"));
        var after = Read(Map().Member("feature:invoices", "concept:billing").Member("feature:charges", "concept:refunds").Member("feature:credits", "concept:billing"));

        var move = Assert.Single(PinMembership.Moves(41, 42, before, after));

        Assert.Equal(("billing", 41, 42, 1, 1), (move.Pin.Value, move.FromVersion, move.ToVersion, move.Gained, move.Lost));
        var to = Assert.Single(move.To);
        Assert.Equal(("refunds", "Refunds", 1), (to.Id!.Value.Value, to.Name, to.Features));
    }

    [Fact]
    public void A_profile_with_no_pins_has_no_moves()
    {
        var plain = new ConceptRead(ConceptGraph.Basis, Map().Build(), ConceptGraph.Text);

        Assert.Empty(PinMembership.Moves(41, 42, plain, plain));
    }
}
