using Aw.Context.Core.Glossary;
using static Aw.Context.Tests.Glossary.GlossaryFixtures;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryTermTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_word_in_every_cluster_scores_zero_and_is_dropped()
    {
        var glossary = Build(TwoGroups());

        Assert.DoesNotContain(glossary.Terms, t => t.Term == "compute");
        Assert.DoesNotContain(glossary.Clusters.SelectMany(c => c.Terms), t => t == "compute");
        Assert.Contains("invoice", glossary.Clusters[0].Terms, StringComparer.Ordinal);
        Assert.Contains("shift", glossary.Clusters[1].Terms, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stoplist_drops_code_noise_and_keywords()
    {
        List<FeatureVectors> features =
        [
            Feature("feature:a", "Handler service", ("GetOrderHandler", [1f, 0f, 0f]), ("public_static_Order", [1f, 0f, 0f])),
            Feature("feature:b", "Other", ("ParcelRouter", [0f, 1f, 0f])),
        ];

        var terms = Build(features).Clusters.SelectMany(c => c.Terms).ToList();

        Assert.Contains("order", terms, StringComparer.Ordinal);
        Assert.Contains("parcel", terms, StringComparer.Ordinal);
        string[] noise = ["get", "handler", "service", "public", "static"];
        Assert.Empty(terms.Intersect(noise, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void CamelCase_PascalCase_snake_case_and_kebab_case_are_split_and_short_words_and_digits_dropped()
    {
        List<FeatureVectors> features =
        [
            Feature("feature:a", "x", ("fooBarBaz", [1f, 0f, 0f]), ("HTTPServerPool", [1f, 0f, 0f]), ("snake_case_name2go", [1f, 0f, 0f]), ("kebab-word-ok", [1f, 0f, 0f])),
            Feature("feature:b", "x", ("zzzUnrelated", [0f, 1f, 0f])),
        ];

        var terms = Build(features, new GlossaryRules { TermsPerCluster = 50 }).Clusters[0].Terms;

        string[] expected = ["foo", "bar", "baz", "http", "server", "pool", "snake", "name", "kebab", "word"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), terms.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("go", terms, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_term_in_several_clusters_merges_their_features_and_keeps_its_top_weight()
    {
        List<FeatureVectors> features =
        [
            Feature("feature:a", "x", ("ledger", [1f, 0f, 0f]), ("ledger", [1f, 0f, 0f]), ("audit", [1f, 0f, 0f])),
            Feature("feature:b", "x", ("ledger", [0f, 1f, 0f]), ("payout", [0f, 1f, 0f]), ("payout", [0f, 1f, 0f])),
            Feature("feature:c", "x", ("roster", [0f, 0f, 1f])),
        ];

        var term = Build(features).Terms.Single(t => t.Term == "ledger");

        Assert.Equal(["feature:a", "feature:b"], Keys(term.Features));
        Assert.True(term.Weight > 0);
    }
}
