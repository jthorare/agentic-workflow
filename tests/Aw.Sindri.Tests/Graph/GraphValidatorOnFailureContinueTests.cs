using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>'onFailure: continue' needs an 'out' path to continue on, so a node with named branches (condition, ask_owner) can't have it (#3003).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphValidatorOnFailureContinueTests
{
    private static GraphNode Continuing(GraphNode node) => node with { OnFailure = "continue" };

    [Fact]
    public void A_condition_with_onFailure_continue_is_refused_in_product_words()
    {
        var spec = Spec([Trigger("daily"), Continuing(Condition("check", "workflow.name")), Notify("yes", "a"), Notify("no", "b")],
            [Edge("daily", "check"), Edge("check", "yes", "true"), Edge("check", "no", "false")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NODE-004");
        Assert.Equal(["check"], finding.Nodes);
        Assert.Equal("onFailure", finding.Field);
        Assert.Contains("'check' is a condition, so it has no single path to continue on when it fails", finding.ToString(), StringComparison.Ordinal);
        Assert.Contains("remove onFailure continue, or add a step after both branches", finding.Fix, StringComparison.Ordinal);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_notify_with_onFailure_continue_still_passes()
    {
        var spec = Spec([Trigger("daily"), Continuing(Notify("tell", "hi"))], [Edge("daily", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-NODE-004");
    }

    [Fact]
    public void A_condition_that_stops_on_failure_is_not_refused()
    {
        var spec = Spec([Trigger("daily"), Condition("check", "workflow.name"), Notify("yes", "a")], [Edge("daily", "check"), Edge("check", "yes", "true")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-NODE-004");
    }

    [Fact]
    public void The_run_rule_says_a_node_with_only_named_edges_leaves_nowhere_and_a_node_with_an_out_edge_does_not()
    {
        var branching = Spec([Trigger("daily"), Condition("check", "x"), Notify("yes", "a")], [Edge("daily", "check"), Edge("check", "yes", "true")]);
        var plain = Spec([Trigger("daily"), Notify("tell", "a"), Notify("after", "b")], [Edge("daily", "tell"), Edge("tell", "after")]);
        var last = Spec([Trigger("daily"), Notify("tell", "a")], [Edge("daily", "tell")]);

        Assert.True(GraphContinueRule.LeavesNowhere(branching, branching.Nodes.Single(n => n.Id.Value == "check")));
        Assert.False(GraphContinueRule.LeavesNowhere(plain, plain.Nodes.Single(n => n.Id.Value == "tell")));
        Assert.False(GraphContinueRule.LeavesNowhere(last, last.Nodes.Single(n => n.Id.Value == "tell")));   // why: nothing after it, so nothing is skipped
    }
}
