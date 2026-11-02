using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryTermIndexTests
{
    private static GlossaryTermIndex Index() => new(GlossaryFixtures.Build(GlossaryFixtures.TwoGroups()));

    [Fact, Trait("Lane", "unit")]
    public void Two_groups_of_one_cluster_carry_the_same_terms() =>
        Assert.Equal(1.0, Index().Between(["feature:billing.invoice"], ["feature:billing.refund"]));

    [Fact, Trait("Lane", "unit")]
    public void Groups_of_different_clusters_share_no_term() =>
        Assert.Equal(0.0, Index().Between(["feature:billing.invoice", "feature:billing.refund"], ["feature:scheduling.shift"]));

    [Fact, Trait("Lane", "unit")]
    public void A_group_with_no_term_overlaps_with_nothing()
    {
        var index = Index();

        Assert.Equal(0.0, index.Between(["feature:unknown"], ["feature:billing.refund"]));
        Assert.Equal(0.0, index.Between([], []));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_overlap_is_the_shared_terms_over_all_the_terms_either_group_carries()
    {
        var index = new GlossaryTermIndex(new MapGlossary(
        [
            new GlossaryTerm("alpha", 1.0, [NodeKey.From("feature:x"), NodeKey.From("feature:y")]),
            new GlossaryTerm("beta", 1.0, [NodeKey.From("feature:x")]),
            new GlossaryTerm("gamma", 1.0, [NodeKey.From("feature:y")]),
        ], [], []));

        Assert.Equal(1.0 / 3.0, index.Between(["feature:x"], ["feature:y"]), precision: 9);
    }
}
