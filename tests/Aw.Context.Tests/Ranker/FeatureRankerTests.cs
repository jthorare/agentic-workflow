using Aw.Context.Core.Graph;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Ranker;

public sealed class FeatureRankerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly RecordSignals _best = new(CascadeDistance: 0, EdgeType.Decides, Provenance.Human, Superseded: false, _now);

    internal static IReadOnlyList<FusedItem> Fused(params string[] keys) =>
        RankFusion.Fuse([new SourceAnswer(SourceId.From("docs"), [.. keys.Select((k, i) => new SourceHit(
            new SourceRef(SourceId.From("docs"), ExternalId.From(k), SourceVersion.From("v1"), Permalink: null, RecordKey.From("repo:" + k)), "Title " + k, i + 1, DateTimeOffset.UnixEpoch, Leaky: false))])]);

    [Fact, Trait("Lane", "unit")]
    public void The_boost_is_the_weighted_mean_of_the_five_features()
    {
        Assert.Equal(1d, FeatureRanker.Boost(_best, new FeatureWeights(), _now), 12);
        Assert.Equal(0.2, FeatureRanker.Boost(RecordSignals.None, new FeatureWeights(), _now), 12);   // only "nothing replaced it" holds
        Assert.Equal(0.5, FeatureRanker.Boost(_best with { CascadeDistance = 1 }, new FeatureWeights { EdgeType = 0, Provenance = 0, Supersession = 0, Recency = 0 }, _now), 12);
    }

    [Fact, Trait("Lane", "unit")]
    public void Weights_of_zero_everywhere_boost_nothing() =>
        Assert.Equal(0d, FeatureRanker.Boost(_best, new FeatureWeights { CascadeDistance = 0, EdgeType = 0, Provenance = 0, Supersession = 0, Recency = 0 }, _now));

    [Fact, Trait("Lane", "unit")]
    public void A_linked_record_overtakes_the_one_fusion_ranked_just_above_it()
    {
        var fused = Fused("a", "b");

        var ranked = FeatureRanker.Apply(fused, new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:b"] = _best }, new FeatureWeights(), _now);

        Assert.Equal(["repo:b", "repo:a"], ranked.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Equal(1d / 62 * 2, ranked[0].Score, 12);
        Assert.Equal(1d / 61 * 1.2, ranked[1].Score, 12);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_boost_can_at_most_double_a_score_so_an_item_far_below_stays_below()
    {
        var ranked = FeatureRanker.Apply(Fused("a", "b", "c", "d"), new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:d"] = _best }, new FeatureWeights(), _now);

        Assert.Equal(["repo:d", "repo:a", "repo:b", "repo:c"], ranked.Select(r => r.Key.Value), StringComparer.Ordinal);   // d: 2/64 = 0.03125 beats a: 1.2/61 = 0.01967
        Assert.True(ranked.All(r => r.Score <= 2d / 61));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_superseded_record_ranks_below_an_equal_one_that_stands()
    {
        var fused = Fused("a", "b");
        var signals = new Dictionary<string, RecordSignals>(StringComparer.Ordinal) { ["repo:a"] = _best with { Superseded = true }, ["repo:b"] = _best };

        var ranked = FeatureRanker.Apply(fused, signals, new FeatureWeights(), _now);

        Assert.Equal("repo:b", ranked[0].Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_signals_at_all_the_fusion_order_stays()
    {
        var ranked = FeatureRanker.Apply(Fused("a", "b", "c"), new Dictionary<string, RecordSignals>(StringComparer.Ordinal), new FeatureWeights(), _now);

        Assert.Equal(["repo:a", "repo:b", "repo:c"], ranked.Select(r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_items_give_no_items() =>
        Assert.Empty(FeatureRanker.Apply([], new Dictionary<string, RecordSignals>(StringComparer.Ordinal), new FeatureWeights(), _now));
}
