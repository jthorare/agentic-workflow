using System.Globalization;
using Aw.Context.Core.Glossary;
using static Aw.Context.Tests.Glossary.GlossaryFixtures;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryClusterTests
{
    [Fact, Trait("Lane", "unit")]
    public void Two_near_features_cluster_and_a_far_one_does_not()
    {
        var glossary = Build(TwoGroups());

        Assert.Equal(
            [["feature:billing.invoice", "feature:billing.refund"], ["feature:scheduling.shift"]],
            glossary.Clusters.Select(c => Keys(c.Features)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_threshold_is_respected_exactly_at_the_similarity()
    {
        // (1,0,0) and (4,3,0) have cosine 4/5, which is exactly 0.8 in double arithmetic.
        List<FeatureVectors> features = [Feature("feature:a", "a", ("alpha", [1f, 0f, 0f])), Feature("feature:b", "b", ("beta", [4f, 3f, 0f]))];

        _ = Assert.Single(Build(features, new GlossaryRules { Similarity = 0.8 }).Clusters);
        Assert.Equal(2, Build(features, new GlossaryRules { Similarity = 0.8000001 }).Clusters.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Single_link_chains_features_through_a_middle_one()
    {
        List<FeatureVectors> features =
        [
            Feature("feature:a", "a", ("alpha", [1f, 0f, 0f])),
            Feature("feature:b", "b", ("beta", [4f, 3f, 0f])),
            Feature("feature:c", "c", ("gamma", [0f, 1f, 0f])),
        ];

        var glossary = Build(features, new GlossaryRules { Similarity = 0.8 });

        // a-b is 0.8 and b-c is 0.6; c joins nothing at 0.8.
        Assert.Equal([["feature:a", "feature:b"], ["feature:c"]], glossary.Clusters.Select(c => Keys(c.Features)));
        _ = Assert.Single(Build(features, new GlossaryRules { Similarity = 0.6 }).Clusters);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_output_is_the_same_under_shuffled_input()
    {
        var input = TwoGroups();
        var shuffled = new List<FeatureVectors> { input[2], input[0], input[1] };

        Assert.Equal(Keys(Build(input).Clusters.SelectMany(c => c.Features)), Keys(Build(shuffled).Clusters.SelectMany(c => c.Features)));
        Assert.Equal(Build(input).Terms.Select(t => (t.Term, t.Weight, Keys(t.Features))), Build(shuffled).Terms.Select(t => (t.Term, t.Weight, Keys(t.Features))));
        Assert.Equal(Build(input).Clusters.Select(c => c.Terms), Build(shuffled).Clusters.Select(c => c.Terms));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_without_vectors_is_listed_and_not_clustered()
    {
        List<FeatureVectors> features = [Feature("feature:a", "a", ("alpha", [1f, 0f, 0f])), Feature("feature:empty", "nothing here"), Feature("feature:zero", "z", ("zeros", [0f, 0f, 0f]))];

        var glossary = Build(features);

        Assert.Equal(["feature:empty", "feature:zero"], Keys(glossary.Unvectored));
        Assert.Equal([["feature:a"]], glossary.Clusters.Select(c => Keys(c.Features)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Past_the_cap_only_the_first_features_in_key_order_are_clustered_and_truncated_is_set()
    {
        var features = Enumerable.Range(0, 5).Reverse().Select(i => NumberedFeature(i.ToString(CultureInfo.InvariantCulture))).ToList();

        var glossary = Build(features, new GlossaryRules { MaxFeatures = 3 });

        Assert.True(glossary.Truncated);
        Assert.Equal(["feature:f0", "feature:f1", "feature:f2"], Keys(glossary.Clusters.Single().Features));
        Assert.False(Build(features).Truncated);
    }

    private static FeatureVectors NumberedFeature(string n) => Feature("feature:f" + n, "f" + n, ("name" + n, [1f, 0f, 0f]));
}
