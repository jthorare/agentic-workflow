using Aw.Context.Core.Graph;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Ranker;

public sealed class RankFeaturesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 0d)]
    [InlineData(0, 1d)]
    [InlineData(1, 0.5)]
    [InlineData(3, 0.25)]
    public void Cascade_distance_scores_one_over_one_plus_the_steps(int? distance, double expected) =>
        Assert.Equal(expected, RankFeatures.Cascade(distance), 12);

    [Fact, Trait("Lane", "unit")]
    public void Edge_type_ranks_a_deciding_record_over_a_quoting_one_over_a_mentioning_one()
    =>
        Assert.Equal([1d, 0.7, 0.4, 0.2, 0d], [RankFeatures.Edge(EdgeType.Decides), RankFeatures.Edge(EdgeType.Quotes), RankFeatures.Edge(EdgeType.Mentions), RankFeatures.Edge(EdgeType.Implements), RankFeatures.Edge(edge: null)]);

    [Fact, Trait("Lane", "unit")]
    public void Provenance_ranks_human_over_deterministic_over_model()
    =>
        Assert.Equal([1d, 0.7, 0.4, 0d], [RankFeatures.Source(Provenance.Human), RankFeatures.Source(Provenance.Deterministic), RankFeatures.Source(Provenance.Model), RankFeatures.Source(provenance: null)]);

    [Fact, Trait("Lane", "unit")]
    public void A_superseded_record_scores_zero_and_one_that_stands_scores_one() =>
        Assert.Equal([1d, 0d], [RankFeatures.Standing(superseded: false), RankFeatures.Standing(superseded: true)]);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 1d)]
    [InlineData(90, 0.5)]
    [InlineData(180, 0.25)]
    [InlineData(-5, 1d)]
    public void Recency_halves_every_90_days_and_a_future_date_counts_as_now(int daysAgo, double expected) =>
        Assert.Equal(expected, RankFeatures.Recency(_now.AddDays(-daysAgo), _now), 12);

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_link_date_scores_zero_recency() => Assert.Equal(0d, RankFeatures.Recency(linked: null, _now));
}
