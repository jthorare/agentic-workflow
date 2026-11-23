using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Rerun;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>The pure part of 'Re-run from here' (spec §7.3): which steps are after the chosen one, which come before it, and which branches a restored step took.</summary>
[Lane(TestLane.Unit)]
public sealed class RerunPlanTests
{
    private static readonly string[] _ids = ["t", "a", "b", "c", "d"];

    private static NodeId N(string id) => NodeId.From(id);

    private static List<NodeRun> Rows(params (string Node, NodeRunStatus Status)[] rows) =>
        [.. rows.Select(static r => new NodeRun(WorkflowRunId.Of(WorkflowSlug.From("plan"), 1, 1), N(r.Node), 0, r.Status, Input: null, Output: null, Truncated: false, Error: null, Usage: null))];

    // t -> a -(yes)-> b -> c ; a -(no)-> d
    private static GraphSpec Spec() => new(Version: 1, Name: "x", Description: null, Concurrency: null, Resources: null,
        Nodes: [.. _ids.Select(id => new GraphNode(N(id), "http", Title: null, []))],
        Edges: [new GraphEdge(N("t"), Output: null, N("a")), new GraphEdge(N("a"), "yes", N("b")), new GraphEdge(N("b"), Output: null, N("c")), new GraphEdge(N("a"), "no", N("d"))]);

    [Fact]
    public void FromHere_is_the_node_and_everything_it_reaches_in_spec_order()
    {
        Assert.Equal(["b", "c"], RerunPlan.FromHere(Spec(), N("b")).Select(static i => i.Value), StringComparer.Ordinal);
        Assert.Equal(["a", "b", "c", "d"], RerunPlan.FromHere(Spec(), N("a")).Select(static i => i.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Before_is_what_the_node_reads_from_and_not_the_node_or_a_side_branch()
    {
        Assert.Equal(["t", "a"], RerunPlan.Before(Spec(), N("b")).Select(static i => i.Value), StringComparer.Ordinal);
        Assert.Empty(RerunPlan.Before(Spec(), N("t")));
    }

    [Fact]
    public void A_restored_step_took_the_branches_into_steps_the_source_did_not_skip()
    {
        var taken = RerunPlan.Taken(Spec(), N("a"), new HashSet<NodeId> { N("d") });

        Assert.Equal(["yes"], taken, StringComparer.Ordinal);
    }

    [Fact]
    public void A_step_skipped_on_the_branch_a_ran_step_did_not_take_is_restored()
    {
        // b failed on 'yes', so 'no' was never taken and d is dead; c was skipped because the run halted, but it is after b anyway
        var rows = Rows(("t", NodeRunStatus.Succeeded), ("a", NodeRunStatus.Succeeded), ("b", NodeRunStatus.Failed), ("c", NodeRunStatus.Skipped), ("d", NodeRunStatus.Skipped));

        Assert.Equal(["t", "a", "d"], RerunPlan.Restored(Spec(), N("b"), rows).Select(static i => i.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_branching_step_none_of_whose_steps_ran_counts_every_branch_as_taken()
    {
        // the run halted before b or d ran, so a's branch can't be read: d runs again
        var rows = Rows(("t", NodeRunStatus.Succeeded), ("a", NodeRunStatus.Succeeded), ("b", NodeRunStatus.Skipped), ("c", NodeRunStatus.Skipped), ("d", NodeRunStatus.Skipped));

        Assert.Equal(["t", "a"], RerunPlan.Restored(Spec(), N("b"), rows).Select(static i => i.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_step_after_a_step_that_runs_again_runs_again_too()
    {
        var rows = Rows(("t", NodeRunStatus.Succeeded), ("a", NodeRunStatus.Failed), ("b", NodeRunStatus.Succeeded), ("c", NodeRunStatus.Succeeded), ("d", NodeRunStatus.Skipped));

        Assert.Equal(["t"], RerunPlan.Restored(Spec(), N("c"), rows).Select(static i => i.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void An_edge_with_no_named_output_is_the_out_branch()
        => Assert.Equal(["out"], RerunPlan.Taken(Spec(), N("t"), new HashSet<NodeId>()), StringComparer.Ordinal);
}
