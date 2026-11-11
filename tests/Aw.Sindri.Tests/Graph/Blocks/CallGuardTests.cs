using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Guards;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class CallGuardTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void A_call_one_level_deeper_is_allowed_up_to_the_maximum(int callerDepth, int expected)
    {
        var next = CallDepth.Next(new CallOrigin("a@1:1", callerDepth, ParentRun: null), "b", CallDepth.DefaultMax);

        Assert.True(next.IsSuccess);
        Assert.Equal(expected, next.Value);
    }

    [Fact]
    public void A_call_past_the_maximum_is_SND_GRAPH_120()
    {
        var next = CallDepth.Next(new CallOrigin("a@1:1", 3, ParentRun: null), "b", CallDepth.DefaultMax);

        Assert.Equal(SindriErrors.GraphCallDepth.Code, next.Error.Code);
        Assert.Contains("depth 4", next.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_may_not_start_its_own_workflow_or_the_workflow_that_called_it()
    {
        var own = CallDepth.Next(new CallOrigin("a@1:1", 0, ParentRun: null), "a", CallDepth.DefaultMax);
        var caller = CallDepth.Next(new CallOrigin("b@1:100000001", 1, "a@1:1"), "a", CallDepth.DefaultMax);
        var other = CallDepth.Next(new CallOrigin("b@1:100000001", 1, "a@1:1"), "c", CallDepth.DefaultMax);

        Assert.Equal(SindriErrors.GraphSelfStart.Code, own.Error.Code);
        Assert.Equal(SindriErrors.GraphSelfStart.Code, caller.Error.Code);
        Assert.True(other.IsSuccess);
    }

    [Fact]
    public void A_call_chain_that_loops_back_to_an_ancestor_is_refused_before_the_depth_cap()
    {
        // a -> b -> c, and c tries to start a: depth 2 of 10, so only the ancestor check can stop it.
        var c = new CallOrigin("c@1:3", 2, "b@1:2") { Ancestors = ["a", "b"] };

        var loop = CallDepth.Next(c, "a", max: 10);
        var fresh = CallDepth.Next(c, "d", max: 10);

        Assert.Equal(SindriErrors.GraphSelfStart.Code, loop.Error.Code);
        Assert.True(fresh.IsSuccess);
        Assert.Equal(3, fresh.Value);
    }

    [Fact]
    public void The_launch_lineage_seam_refuses_a_workflow_the_calling_session_descends_from()
    {
        var refused = CallDepth.Next(new CallOrigin("b@1:1", 0, ParentRun: null), "a", CallDepth.DefaultMax, launchLineage: ["a"]);

        Assert.Equal(SindriErrors.GraphSelfStart.Code, refused.Error.Code);
    }

    [Fact]
    public void The_slug_of_a_run_id_is_the_part_before_the_at_sign() => Assert.Equal("contract-drift", CallDepth.SlugOf("contract-drift@3:12"));

    [Fact]
    public void A_call_occurrence_is_stable_per_caller_node_and_index_and_above_the_numbers_a_trigger_counts_to()
    {
        var one = CallOccurrence.Of("a@1:1", "call", 0);

        Assert.Equal(one, CallOccurrence.Of("a@1:1", "call", 0));
        Assert.NotEqual(one, CallOccurrence.Of("a@1:1", "call", 1));
        Assert.NotEqual(one, CallOccurrence.Of("a@1:2", "call", 0));
        Assert.NotEqual(one, CallOccurrence.Of("a@1:1", "other", 0));
        Assert.InRange(one, 100_000_000, 999_999_999);
    }
}
