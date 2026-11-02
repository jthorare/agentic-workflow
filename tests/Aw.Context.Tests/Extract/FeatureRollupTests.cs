using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §3 and §6 step 4: the file edges roll up to the home features, with counts and the first pairs as evidence.</summary>
public sealed class FeatureRollupTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_seven_file_fixture_rolls_up_to_the_eight_edges_with_their_counts_and_evidence()
    {
        var rolled = RollupFixture.Rolled(FeatureRollup.Apply(RollupFixture.Files()));

        Assert.Equal(
            [
                (RollupFixture.Billing, EdgeType.DependsOn, RollupFixture.Orders, 3, "billing/b1.ts -> orders/o1.ts billing/b2.ts -> orders/o1.ts billing/b2.ts -> orders/o2.ts"),
                (RollupFixture.Billing, EdgeType.Reads, "thing:invoices", 1, "billing/b2.ts"),
                (RollupFixture.Billing, EdgeType.Writes, "thing:invoices", 1, "billing/b1.ts"),
                (RollupFixture.Billing, EdgeType.Writes, "thing:orders_t", 1, "billing/b3.ts"),
                (RollupFixture.Orders, EdgeType.DependsOn, RollupFixture.Users, 1, "orders/o1.ts -> users/u1.ts"),
                (RollupFixture.Orders, EdgeType.Reads, "thing:invoices", 1, "orders/o1.ts"),
                (RollupFixture.Orders, EdgeType.Writes, "thing:orders_t", 1, "orders/o2.ts"),
                (RollupFixture.Users, EdgeType.Reads, "thing:orders_t", 1, "users/u1.ts"),
            ],
            rolled);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_rolled_up_edge_is_deterministic_confidence_one_and_allowed_by_the_edge_table()
    {
        var map = FeatureRollup.Apply(RollupFixture.Files());

        var edges = map.Edges.Where(e => e.Count is not null).ToList();
        Assert.Equal(8, edges.Count);
        Assert.All(edges, e => Assert.Equal((Provenance.Deterministic, 1.0), (e.Provenance, e.Confidence)));
        Assert.All(edges, e => Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_edge_whose_both_files_are_in_one_feature_rolls_up_to_nothing_and_the_file_edges_stay()
    {
        var input = RollupFixture.Files([("a1", "billing"), ("a2", "billing")], [("a1", "a2")], []);

        var map = FeatureRollup.Apply(input);

        Assert.DoesNotContain(map.Edges, e => e.From.Kind == NodeKind.Feature);
        Assert.Contains(map.Edges, e => e is { Type: EdgeType.DependsOn, From.Kind: NodeKind.Code });
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_gives_the_same_edges_and_the_same_evidence_order_whatever_order_the_input_came_in()
    {
        var input = RollupFixture.Files();
        var shuffled = new ExtractedMap([.. input.Nodes.Reverse()], [.. input.Edges.Reverse()]);

        var first = RollupFixture.Rolled(FeatureRollup.Apply(input));
        var again = RollupFixture.Rolled(FeatureRollup.Apply(FeatureRollup.Apply(input)));
        var other = RollupFixture.Rolled(FeatureRollup.Apply(shuffled));

        Assert.Equal(first, again);
        Assert.Equal(first, other);
    }

    [Fact, Trait("Lane", "unit")]
    public void Six_pairs_give_a_count_of_six_and_five_evidence_entries_the_first_five_by_path()
    {
        var homes = new[] { ("a1", "billing"), ("a2", "billing"), ("a3", "billing"), ("o1", "orders"), ("o2", "orders"), ("o3", "orders") };
        var imports = new[] { ("a3", "o3"), ("a1", "o1"), ("a1", "o2"), ("a2", "o1"), ("a2", "o2"), ("a1", "o3") };

        var edge = Assert.Single(FeatureRollup.Apply(RollupFixture.Files(homes, imports, [])).Edges, e => e.From.Kind == NodeKind.Feature);

        Assert.Equal(6, edge.Count);
        Assert.Equal(FeatureRollup.EvidencePairs, edge.EvidenceOrEmpty.Count);
        Assert.Equal(
            [("a1", "o1"), ("a1", "o2"), ("a1", "o3"), ("a2", "o1"), ("a2", "o2")],
            edge.EvidenceOrEmpty.Select(r => (r.Key.Value["repo:work/billing/".Length..^3], r.Span!["repo:work/orders/".Length..^3])));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_group_feature_that_holds_no_file_gets_no_edge_and_a_file_without_a_home_adds_none()
    {
        var input = RollupFixture.Files([("a1", "billing"), ("o1", "orders")], [("a1", "o1"), ("a1", "orphan")], [("a1", EdgeType.Writes, "t")]);
        var withGroup = new ExtractedMap([.. input.Nodes, new GraphNode(NodeKey.From("feature:work"), "work", Provenance.Deterministic)],
            [.. input.Edges, new GraphEdge(NodeKey.From("feature:work.billing"), NodeKey.From("feature:work"), EdgeType.PartOf, Provenance.Deterministic)]);

        var rolled = FeatureRollup.Apply(withGroup).Edges.Where(e => e.Count is not null).ToList();

        Assert.DoesNotContain(rolled, e => e.From.Value == "feature:work" || e.To.Value == "feature:work");
        Assert.Equal(2, rolled.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_edge_fact_of_a_roll_up_edge_carries_the_count_and_its_first_example()
    {
        var edge = FeatureRollup.Apply(RollupFixture.Files()).Edges.Single(e => e.Count is 3);

        var fact = EdgeFact.Of(edge);

        Assert.Equal(3, fact.Count);
        Assert.Equal("billing/b1.ts -> orders/o1.ts", fact.Example);
        Assert.Null(EdgeFact.Of(new GraphEdge(edge.From, edge.To, edge.Type)).Count);
    }
}
