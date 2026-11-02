using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>Spec 2026-10-27 §10.2 to §10.4: the feature roll-up edges roll up to the pinned concepts, a declared edge and a rolled one are one Human edge with the count, and the two notes never refuse.</summary>
public sealed class PinRollupTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static ConceptPin Pin(string id, params string[] features) => ConceptPin.Of(ConceptId.From(id), id, summary: null, features, paths: null);

    private static GraphEdge Rolled(string from, EdgeType type, string to, int count) =>
        new(NodeKey.From(from), NodeKey.From(to), type, Provenance.Deterministic, 1.0, Count: count);

    // The §3 fixture: pin money holds billing and orders, pin people holds users.
    private static GraphSnapshot Fixture()
    {
        var graph = new GraphBuilder()
            .Node("feature:billing").Node("feature:orders").Node("feature:users").Node("concept:money").Node("concept:people")
            .Node("thing:invoices").Node("thing:orders-t")
            .Edge("feature:billing", EdgeType.BelongsTo, "concept:money", provenance: Provenance.Deterministic)
            .Edge("feature:orders", EdgeType.BelongsTo, "concept:money", provenance: Provenance.Deterministic)
            .Edge("feature:users", EdgeType.BelongsTo, "concept:people", provenance: Provenance.Deterministic)
            .Build();
        GraphEdge[] counted =
        [
            Rolled("feature:orders", EdgeType.DependsOn, "feature:users", 1), Rolled("feature:billing", EdgeType.DependsOn, "feature:orders", 2),
            Rolled("feature:billing", EdgeType.Writes, "thing:invoices", 1), Rolled("feature:billing", EdgeType.Writes, "thing:orders-t", 1), Rolled("feature:orders", EdgeType.Writes, "thing:orders-t", 1),
            Rolled("feature:billing", EdgeType.Reads, "thing:invoices", 1), Rolled("feature:orders", EdgeType.Reads, "thing:invoices", 1), Rolled("feature:users", EdgeType.Reads, "thing:orders-t", 1),
        ];
        return graph with { Edges = [.. graph.Edges, .. counted] };
    }

    private static ConceptPin[] Pins(ConceptPin? money = null) => [money ?? Pin("money", "feature:billing", "feature:orders"), Pin("people", "feature:users")];

    private static PinRollupResult Apply(params ConceptPin[] pins)
    {
        var graph = Fixture();
        return PinRollup.Apply(graph, pins, PinEdges.Declared(graph, pins));
    }

    private static string[] Lines(PinRollupResult result) =>
        [.. result.Edges.Select(e => $"{e.From.Name} {EdgeRules.Name(e.Type)} {e.To.Name} {e.Count}").Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void The_pins_roll_up_the_feature_edges_with_counts_and_an_edge_inside_one_pin_rolls_up_to_nothing() =>
        Assert.Equal(
            ["money depends-on people 1", "money reads invoices 2", "money writes invoices 1", "money writes orders-t 2", "people reads orders-t 1"],
            Lines(Apply(Pins())), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_rolled_edge_is_deterministic_and_cites_the_first_five_evidence_refs()
    {
        var edges = Apply(Pins()).Edges;

        Assert.All(edges, e => Assert.Equal(Provenance.Deterministic, e.Provenance));
        Assert.All(edges, e => Assert.True(e.EvidenceOrEmpty.Count <= FeatureRollup.EvidencePairs));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_derived_concept_gets_no_roll_up_edge()
    {
        var result = Apply(Pin("money", "feature:billing", "feature:orders"));

        Assert.DoesNotContain(result.Edges, e => e.From.Value == "concept:people");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_edge_the_code_also_shows_is_one_human_edge_with_the_rolled_count()
    {
        var money = Pin("money", "feature:billing", "feature:orders").Declaring(["people"], owns: null);

        var edge = Assert.Single(Apply(Pins(money)).Edges, e => e.To.Value == "concept:people");

        Assert.Equal((Provenance.Human, 1), (edge.Provenance, edge.Count));
        Assert.Contains(edge.EvidenceOrEmpty, x => x.Key.Value == "profile:index.concepts.money.dependsOn");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_edge_the_code_does_not_show_stays_and_is_a_declared_not_observed_note()
    {
        var people = Pin("people", "feature:users").Declaring(["money"], owns: null);

        var result = Apply(Pin("money", "feature:billing", "feature:orders"), people);

        var edge = Assert.Single(result.Edges, e => e.From.Value == "concept:people" && e.Type == EdgeType.DependsOn);
        Assert.Equal((Provenance.Human, null), (edge.Provenance, edge.Count));
        Assert.Contains("declared, not observed: people depends-on money", result.Notes, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_edge_the_code_shows_and_a_declaring_pin_did_not_is_an_observed_not_declared_note_and_still_an_edge()
    {
        var money = Pin("money", "feature:billing", "feature:orders").Declaring(dependsOn: null, ["invoices"]);

        var result = Apply(money, Pin("people", "feature:users"));

        Assert.Contains("observed, not declared: money depends-on people (1 imports)", result.Notes, StringComparer.Ordinal);
        Assert.Contains(result.Edges, e => e is { Provenance: Provenance.Deterministic } && e.To.Value == "concept:people");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_declares_nothing_has_no_observed_not_declared_note() =>
        Assert.Empty(Apply(Pins()).Notes);

    [Fact, Trait("Lane", "unit")]
    public void The_cut_drops_a_merged_edge_and_the_next_build_makes_it_again_as_a_human_edge_from_the_profile()
    {
        var money = Pin("money", "feature:billing", "feature:orders").Declaring(["people"], owns: null);
        var first = Apply(Pins(money));
        var stored = Fixture() with { Edges = [.. Fixture().Edges, .. first.Edges] };

        var (cut, _, _) = MapAccess.WithoutBuiltStructure(stored, [], ExtractedMap.Empty, _at);
        var again = Apply(Pins(money));

        Assert.DoesNotContain(cut.Edges, e => e.From.Value == "concept:money" && e.To.Value == "concept:people");
        var edge = Assert.Single(again.Edges, e => e.To.Value == "concept:people");
        Assert.Equal(Provenance.Human, edge.Provenance);
    }
}
