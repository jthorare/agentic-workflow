using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Merge;

public sealed class MergeGateTests
{
    private static GateRecord Gate(GateVerdict verdict = GateVerdict.Pass, params GateStep[] steps) =>
        new(190, "head1", "main1", ["repo"], steps, verdict, A.At(0));

    private static MergeFacts Facts(GateRecord? gate) => new()
    {
        Pr = 190,
        Gate = gate,
        CurrentHead = "head1",
        CurrentMain = "main1",
        OpenPrs = [190],
        DocsOnly = ["**/*.md", "docs/**"],
        PlannedScopes = ["repo"],
    };

    private static string[] Failed(MergeFacts facts) => [.. MergeGate.Evaluate(facts).Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public void A_passed_gate_on_unchanged_head_and_main_holds_every_check()
    {
        var checks = MergeGate.Evaluate(Facts(Gate()));

        Assert.Equal(["gate", "gate-result", "scopes", "head", "main", "order", "draft"], checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.All(checks, c => Assert.True(c.Ok, c.Detail));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "main1", true)]        // legacy gate without a tree, same main: the old rule
    [InlineData(null, "main2", false)]       // legacy gate, main moved: refused
    [InlineData("t1", "main2", false)]       // tree recorded, main moved, merge-tree not computed or different
    public void The_main_check_of_an_unstacked_gate_keeps_the_fast_path_and_the_old_rule(string? tree, string currentMain, bool ok)
    {
        var gate = Gate() with { Tree = tree };

        var check = MergeGate.Evaluate(Facts(gate) with { CurrentMain = currentMain }).Single(c => c.Name == "main");

        Assert.Equal(ok, check.Ok);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("main1", "t1", true)]
    [InlineData("main2", "t1", true)]
    [InlineData("main1", "t2", false)]
    [InlineData("main1", null, false)]
    public void A_stacked_gate_holds_only_when_the_merged_tree_equals_the_tested_tree(string currentMain, string? mergedTree, bool ok)
    {
        var gate = Gate() with { Tree = "t1", Stack = [new StackedPr(189, "a1")] };

        var check = MergeGate.Evaluate(Facts(gate) with { CurrentMain = currentMain, MergedTree = mergedTree }).Single(c => c.Name == "main");

        Assert.Equal(ok, check.Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_head_check_still_applies_to_a_stacked_gate_whose_tree_matches()
    {
        var gate = Gate() with { Tree = "t1", Stack = [new StackedPr(189, "a1")] };

        var failed = Failed(Facts(gate) with { CurrentHead = "head2", MergedTree = "t1" });

        Assert.Equal(["head"], failed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "repo" }, true)]
    [InlineData(new[] { "repo", "dotnet" }, false)]
    public void A_gate_vouches_only_for_the_scopes_it_ran(string[] planned, bool ok)
    {
        var check = MergeGate.Evaluate(Facts(Gate()) with { PlannedScopes = planned }).Single(c => c.Name == "scopes");

        Assert.Equal(ok, check.Ok);
        Assert.Equal(ok ? "ran every scope the diff needs (repo)" : "the diff needs dotnet; the gate ran repo", check.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_is_refused_when_the_scopes_the_diff_needs_cannot_be_worked_out()
    {
        var check = MergeGate.Evaluate(Facts(Gate()) with { PlannedScopes = null }).Single(c => c.Name == "scopes");

        Assert.False(check.Ok);
        Assert.Equal("could not work out the scopes the diff needs; the gate ran repo", check.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_gate_reports_only_the_missing_gate_not_unfetched_defaults()
    {
        var checks = MergeGate.Evaluate(Facts(gate: null) with { IsDraft = false });

        var only = Assert.Single(checks);
        Assert.Equal(("gate", false), (only.Name, only.Ok));
        Assert.EndsWith("run aw-meta gate-run 190", only.Detail, StringComparison.Ordinal);
    }

    private static GateStep Red(string name, params (string Test, FlakeVerdict Verdict)[] reruns) =>
        new(name, GateVerdict.Fail, "/l") { Reruns = [.. reruns.Select(r => new TestVerdict(r.Test, r.Verdict))] };

    private static MergeFacts Failing(GateRecord gate, params (string Step, string[] Tests)[] failing) =>
        Facts(gate) with { FailingTests = failing.ToDictionary(f => f.Step, f => (IReadOnlyList<string>)f.Tests, StringComparer.Ordinal) };

    [Fact, Trait("Lane", "unit")]
    public void A_failed_gate_passes_only_when_every_failing_test_of_every_red_step_is_a_flake()
    {
        var green = new GateStep("repo", GateVerdict.Pass, "/l");
        var real = Red("unit", ("A.B.T1", FlakeVerdict.Real));
        var flake = Red("integration", ("T2", FlakeVerdict.Flake));

        Assert.Equal(["gate-result"], Failed(Failing(Gate(GateVerdict.Fail, green, real, flake), ("unit", ["A.B.T1"]), ("integration", ["A.T2"]))), StringComparer.Ordinal);
        Assert.Equal(["gate-result"], Failed(Failing(Gate(GateVerdict.Fail, Red("build")), ("build", ["A.T3"]))), StringComparer.Ordinal);
        Assert.Empty(Failed(Failing(Gate(GateVerdict.Fail, green, flake), ("integration", ["A.T2"]))));
        Assert.Equal(["gate-result"], Failed(Failing(Gate(GateVerdict.Conflict))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_flake_verdict_covers_only_the_tests_it_names()
    {
        var step = Red("unit", ("T2", FlakeVerdict.Flake));

        Assert.Equal(["gate-result"], Failed(Failing(Gate(GateVerdict.Fail, step), ("unit", ["A.T2", "A.T9"]))), StringComparer.Ordinal);
        Assert.Empty(Failed(Failing(Gate(GateVerdict.Fail, step), ("unit", ["A.T2(x: 1)", "A.T2(x: 2)"]))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_red_step_with_no_readable_failing_tests_fails_whatever_was_classified()
    {
        var step = Red("unit", ("T2", FlakeVerdict.Flake));

        Assert.Equal(["gate-result"], Failed(Facts(Gate(GateVerdict.Fail, step))), StringComparer.Ordinal);
        Assert.Equal(["gate-result"], Failed(Failing(Gate(GateVerdict.Fail, step), ("unit", []))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_fails_unless_the_delta_is_docs_only_and_the_repo_gate_passed()
    {
        var moved = Facts(Gate()) with { CurrentHead = "head2" };

        Assert.Equal(["head"], Failed(moved), StringComparer.Ordinal);
        Assert.Equal(["head"], Failed(moved with { DeltaPaths = ["README.md"] }), StringComparer.Ordinal);
        Assert.Equal(["head"], Failed(moved with { DeltaPaths = ["README.md", "src/a.cs"], RepoGatePassedOnHead = true }), StringComparer.Ordinal);
        Assert.Empty(Failed(moved with { DeltaPaths = ["README.md", "docs/a/b.png"], RepoGatePassedOnHead = true }));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]     // a gate recorded without its tree
    [InlineData("t1")]     // merge-tree not computed or different
    public void Main_moved_passes_when_the_base_branch_has_a_merge_queue(string? tree)
    {
        var gate = Gate() with { Tree = tree };

        var check = MergeGate.Evaluate(Facts(gate) with { CurrentMain = "main2", BaseHasMergeQueue = true }).Single(c => c.Name == "main");

        Assert.True(check.Ok, check.Detail);
        Assert.Contains("merge queue re-tests on the merged tree", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Main_moved_still_refuses_without_a_merge_queue() =>
        Assert.Equal(["main"], Failed(Facts(Gate() with { Tree = "t1" }) with { CurrentMain = "main2", BaseHasMergeQueue = false }), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_moved_main_fails() =>
        Assert.Equal(["main"], Failed(Facts(Gate()) with { CurrentMain = "main2" }), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Merge_order_holds_a_pull_request_back_while_its_predecessor_is_open()
    {
        var rules = new[] { new MergeOrderRule(Before: 188, After: 190) };
        var facts = Facts(Gate()) with { MergeOrder = rules };

        Assert.Equal(["order"], Failed(facts with { OpenPrs = [188, 190] }), StringComparer.Ordinal);
        Assert.Empty(Failed(facts with { OpenPrs = [190] }));
        Assert.Empty(Failed(facts with { Pr = 188, OpenPrs = [188, 190] }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_fails() =>
        Assert.Equal(["draft"], Failed(Facts(Gate()) with { IsDraft = true }), StringComparer.Ordinal);
}
