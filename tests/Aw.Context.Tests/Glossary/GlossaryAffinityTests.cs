using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using static Aw.Context.Tests.Glossary.GlossaryFixtures;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryAffinityTests
{
    [Fact, Trait("Lane", "unit")]
    public void Features_with_the_same_terms_have_affinity_one_and_features_with_none_in_common_zero()
    {
        var glossary = Build(TwoGroups());

        Assert.Equal(1.0, GlossaryAffinity.Between(glossary, NodeKey.From("feature:billing.invoice"), NodeKey.From("feature:billing.refund")));
        Assert.Equal(0.0, GlossaryAffinity.Between(glossary, NodeKey.From("feature:billing.invoice"), NodeKey.From("feature:scheduling.shift")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_the_glossary_does_not_know_has_affinity_zero()
    {
        var glossary = Build(TwoGroups());

        Assert.Equal(0.0, GlossaryAffinity.Between(glossary, NodeKey.From("feature:billing.invoice"), NodeKey.From("feature:unknown")));
    }
}
