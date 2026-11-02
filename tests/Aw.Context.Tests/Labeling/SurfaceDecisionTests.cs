using Aw.Context.Core.Surfaces;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class SurfaceDecisionTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly SurfaceGroup _group = new([Candidate("action:a.grant", "Grant"), Candidate("action:b.grant", "Give access")]);

    [Fact]
    public void A_merge_names_the_survivors_index_in_the_group_as_it_is_now()
    {
        var decision = SurfaceDecision.From(SurfaceVerdict.Merged(_group.Key, 1, "action:b.grant", "Give access"), _group, "jev-1", _at);

        var verdict = decision.ToVerdict(_group);

        Assert.Equal(SurfaceVerdict.Merged(_group.Key, 1, "action:b.grant", "Give access"), verdict);
        Assert.Equal(["action:a.grant", "action:b.grant"], decision.CandidateKeys);
    }

    [Fact]
    public void A_merge_whose_survivor_left_the_group_is_a_decided_separate()
    {
        var decision = new SurfaceDecision(_group.Key, SurfaceChoice.Merge, "action:gone", "Gone", ["action:gone"], Model: null, _at);

        Assert.Equal(SurfaceVerdict.Separate(_group.Key, decided: true), decision.ToVerdict(_group));
    }

    [Fact]
    public void A_reject_and_a_separate_come_back_decided()
    {
        var reject = SurfaceDecision.From(SurfaceVerdict.Rejected(_group.Key), _group, model: null, _at);
        var separate = SurfaceDecision.From(SurfaceVerdict.Separate(_group.Key, decided: true), _group, model: null, _at);

        Assert.Equal(SurfaceVerdict.Rejected(_group.Key), reject.ToVerdict(_group));
        Assert.Equal(SurfaceVerdict.Separate(_group.Key, decided: true), separate.ToVerdict(_group));
    }

    private static SurfaceCandidate Candidate(string key, string name) => new(key, "action", name, "action", [], []);
}
