using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Merge;

public sealed class MergeGateCiTests
{
    private static GateRecord Record(string head, GateVerdict verdict, params string[] scopes) =>
        new(7, head, "oldmain", scopes, [new GateStep("build", verdict, "x")], verdict, A.At(1)) { Run = 3 };

    private static MergeFacts Facts(GateRecord? gate, string head = "h1") =>
        new() { Pr = 7, Gate = gate, CurrentHead = head, CurrentMain = "newmain", CiGate = true, LiveMergeState = "CLEAN", LiveReviewDecision = "APPROVED" };

    private static string[] Failed(IReadOnlyList<MergeCheck> checks) => [.. checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public void A_passing_ci_record_at_the_head_passes_with_no_scope_main_or_test_log_check()
    {
        var checks = MergeGate.Evaluate(Facts(Record("h1", GateVerdict.Pass, "ci")));

        Assert.Empty(Failed(checks));
        Assert.Equal(["gate", "gate-result", "head", "merge-state", "review-decision", "order", "draft"], checks.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_a_red_record_or_a_local_record_fails()
    {
        Assert.Equal(["head"], Failed(MergeGate.Evaluate(Facts(Record("old", GateVerdict.Pass, "ci")))), StringComparer.Ordinal);
        Assert.Equal(["gate-result"], Failed(MergeGate.Evaluate(Facts(Record("h1", GateVerdict.Fail, "ci")))), StringComparer.Ordinal);
        Assert.Contains("gate", Failed(MergeGate.Evaluate(Facts(Record("h1", GateVerdict.Pass, "repo")))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_record_the_gate_check_fails() =>
        Assert.Equal(["gate"], Failed(MergeGate.Evaluate(Facts(gate: null))), StringComparer.Ordinal);
}
