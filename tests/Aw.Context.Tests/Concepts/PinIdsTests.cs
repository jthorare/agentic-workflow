using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>Spec 2026-10-27 §10.1: one shared function makes the pin ids, so the home leaf '~billing' and 'belongs-to concept:billing' agree.</summary>
public sealed class PinIdsTests
{
    private static ConceptPin Pin(string? id, string name) => ConceptPin.Of(id is null ? null : ConceptId.From(id), name, summary: null, features: ["feature:a"], paths: null);

    [Fact, Trait("Lane", "unit")]
    public void A_profile_pin_repeated_once_per_repo_is_one_pin()
    {
        var pin = Pin("billing", "Billing");

        var ids = PinIds.Resolve([pin, pin, Pin("auth", "Auth")]);

        Assert.Equal(["billing", "auth"], ids.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_without_an_id_takes_a_slug_of_its_name_and_a_clash_gets_a_number()
    {
        var ids = PinIds.Resolve([Pin(id: null, "Self Scheduling"), Pin("self-scheduling", "Other")]);

        Assert.Equal(["self-scheduling", "self-scheduling-2"], ids.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_deriver_makes_one_concept_for_a_pin_repeated_by_two_repos()
    {
        var graph = new GraphBuilder().Node("feature:a").Build();
        var pin = Pin("billing", "Billing");

        var concepts = ConceptDeriver.Derive(graph, [pin, pin], names: null);

        _ = Assert.Single(concepts.Nodes, n => n.Key.Kind == NodeKind.Concept && n.Key.Value.StartsWith("concept:billing", StringComparison.Ordinal));
        Assert.DoesNotContain(concepts.Nodes, n => n.Key.Value == "concept:billing-2");
    }
}
