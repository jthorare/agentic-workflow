using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>Spec 2026-10-27 §10.3: a pin's 'dependsOn' and 'owns' become Human edges the build makes fresh, and an entry with no endpoint in the map is skipped with a count.</summary>
public sealed class PinEdgesTests
{
    private static ConceptPin Pin(string id, string[]? dependsOn = null, string[]? owns = null) =>
        ConceptPin.Of(ConceptId.From(id), id, summary: null, features: null, paths: null).Declaring(dependsOn, owns);

    private static GraphSnapshot Map() =>
        new GraphBuilder().Node("concept:billing").Node("concept:orders").Node("thing:invoices").Node("thing:payments-t").Build();

    [Fact, Trait("Lane", "unit")]
    public void A_depends_on_entry_is_a_human_concept_to_concept_edge_with_the_profile_key_as_evidence()
    {
        var declared = PinEdges.Declared(Map(), [Pin("billing", dependsOn: ["orders"]), Pin("orders")]);

        var edge = Assert.Single(declared.Edges);
        Assert.Equal(("concept:billing", EdgeType.DependsOn, "concept:orders", Provenance.Human), (edge.From.Value, edge.Type, edge.To.Value, edge.Provenance));
        Assert.Equal("profile:index.concepts.billing.dependsOn", Assert.Single(edge.Evidence!).Key.Value);
        Assert.Equal(0, declared.Skipped);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_owns_entry_is_a_human_writes_edge_to_the_thing_the_extractor_named()
    {
        var declared = PinEdges.Declared(Map(), [Pin("billing", owns: ["invoices", "payments_t"])]);

        Assert.Equal(["concept:billing writes thing:invoices", "concept:billing writes thing:payments-t"], declared.Edges.Select(e => $"{e.From.Value} {EdgeRules.Name(e.Type)} {e.To.Value}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(declared.Edges, e => Assert.Equal(Provenance.Human, e.Provenance));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_edge_whose_endpoint_is_not_in_the_map_is_skipped_and_counted()
    {
        var declared = PinEdges.Declared(Map(), [Pin("billing", dependsOn: ["ghost"], owns: ["no_such_table"]), Pin("orders")]);

        Assert.Empty(declared.Edges);
        Assert.Equal(2, declared.Skipped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_repeated_for_two_repos_declares_each_edge_once()
    {
        var pin = Pin("billing", dependsOn: ["orders"]);

        var declared = PinEdges.Declared(Map(), [pin, pin, Pin("orders")]);

        _ = Assert.Single(declared.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_without_dependencies_declares_nothing()
    {
        var declared = PinEdges.Declared(Map(), [Pin("billing"), Pin("orders")]);

        Assert.Empty(declared.Edges);
        Assert.Equal(0, declared.Skipped);
    }
}
