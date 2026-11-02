using Aw.Context.Core.Ranking;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Core;

public sealed class RankFusionTests
{
    private static SourceHit Hit(string source, string key, int rank, string chunk = "") => new(
        new SourceRef(SourceId.From(source), ExternalId.From(key + chunk), SourceVersion.From("v1"), Permalink: null, RecordKey.From("repo:" + key)),
        "t", rank, DateTimeOffset.UnixEpoch, Leaky: false);

    [Fact, Trait("Lane", "unit")]
    public void Hits_of_one_record_merge_keeping_the_best_rank_per_source()
    {
        var fused = RankFusion.Fuse([new SourceAnswer(SourceId.From("repo-docs"), [Hit("repo-docs", "a", 3, "#2"), Hit("repo-docs", "b", 2), Hit("repo-docs", "a", 1, "#1")])]);

        Assert.Equal(["repo:a", "repo:b"], fused.Select(f => f.Key.Value), StringComparer.Ordinal);
        Assert.Equal("a#1", fused[0].Hits.Single().Ref.Id.Value);
        Assert.Equal(1d / 61, fused[0].Score, 12);
        Assert.Equal(["rrf:repo-docs#1"], fused[0].Why);
    }

    [Fact, Trait("Lane", "unit")]
    public void Aliases_of_every_hit_of_a_record_merge_best_hit_first_without_repeats()
    {
        var fused = RankFusion.Fuse([
            new SourceAnswer(SourceId.From("slack"), [Hit("slack", "a", 1) with { Aliases = ["SPL-103"] }, Hit("slack", "a", 4, "#2") with { Aliases = ["SPL-9"] }]),
            new SourceAnswer(SourceId.From("linear"), [Hit("linear", "a", 2) with { Aliases = ["SPL-103", "SPL-7"] }])]);

        Assert.Equal(["SPL-103", "SPL-7", "SPL-9"], fused.Single().Aliases, StringComparer.Ordinal);   // both rank 1: linear before slack by source id
    }

    [Fact, Trait("Lane", "unit")]
    public void A_source_with_many_sub_queries_is_re_ranked_densely_before_fusion()
    {
        // 'docs' saw b only at rank 9 of its sub-queries; it is still its second-best record, so rank 2.
        var fused = RankFusion.Fuse([
            new SourceAnswer(SourceId.From("docs"), [Hit("docs", "a", 1), Hit("docs", "b", 9)]),
            new SourceAnswer(SourceId.From("slack"), [Hit("slack", "b", 1)])]);

        Assert.Equal("repo:b", fused[0].Key.Value);
        Assert.Equal((1d / 62) + (1d / 61), fused[0].Score, 12);
        Assert.Equal(["rrf:docs#2", "rrf:slack#1"], fused[0].Why);
        Assert.Equal(2, fused[0].Hits.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ties_order_by_key() =>
        Assert.Equal(["repo:a", "repo:b"], RankFusion.Fuse([
            new SourceAnswer(SourceId.From("x"), [Hit("x", "b", 1)]),
            new SourceAnswer(SourceId.From("y"), [Hit("y", "a", 1)])]).Select(f => f.Key.Value), StringComparer.Ordinal);
}
