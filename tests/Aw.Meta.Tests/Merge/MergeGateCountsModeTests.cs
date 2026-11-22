using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Merge;

// #3789: review.mode counts drops the reviewer's OK only. A red gate recorded at the head still refuses, and a gate.mode ci team
// still needs its CI record.
public sealed class MergeGateCountsModeTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";

    private static ReviewFacts Counts() =>
        new() { Reviewer = "t1-ideas", CountsMode = true, ReadySha = Head[..10], Counts = [new CountLine(10, 0, 0)] };

    private static GateRecord Record(GateVerdict verdict, params string[] scopes) =>
        new(7, Head, "oldmain", scopes, [new GateStep("build", verdict, "x")], verdict, A.At(1)) { Run = 3 };

    private static MergeFacts Facts(GateRecord? gate, bool ci = false) =>
        new() { Pr = 7, Gate = gate, Review = Counts(), CurrentHead = Head, CurrentMain = "newmain", CiGate = ci, LiveMergeState = "CLEAN", LiveReviewDecision = "APPROVED" };

    private static string[] Failed(IReadOnlyList<MergeCheck> checks) => [.. checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public void With_no_gate_the_counts_alone_pass()
    {
        var checks = MergeGate.Evaluate(Facts(gate: null));

        Assert.Empty(Failed(checks));
        Assert.Equal(["review", "ready", "counts", "order", "draft"], checks.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_red_gate_at_the_head_still_refuses() =>
        Assert.Equal(["gate-result"], Failed(MergeGate.Evaluate(Facts(Record(GateVerdict.Fail, "repo")))), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_ci_team_still_needs_its_CI_record()
    {
        var checks = MergeGate.Evaluate(Facts(gate: null, ci: true));

        Assert.Equal(["gate"], Failed(checks), StringComparer.Ordinal);
        Assert.True(checks.Single(c => c.Name == "review").Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ci_team_with_a_passing_CI_record_and_green_counts_passes_with_no_OK() =>
        Assert.Empty(Failed(MergeGate.Evaluate(Facts(Record(GateVerdict.Pass, "ci"), ci: true))));

    [Fact, Trait("Lane", "unit")]
    public void A_standing_HOLD_fails_the_review_check_in_a_ci_team_too()
    {
        var facts = Facts(Record(GateVerdict.Pass, "ci"), ci: true) with { Review = Counts() with { Hold = "HOLD #7" } };

        Assert.Equal(["review"], Failed(MergeGate.Evaluate(facts)), StringComparer.Ordinal);
    }
}
