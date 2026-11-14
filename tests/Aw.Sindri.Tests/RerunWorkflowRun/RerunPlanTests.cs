using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>The pure part of 'Re-run from here' (spec §7.3): which steps are after the chosen one, which come before it, and which branches a restored step took.</summary>
[Lane(TestLane.Unit)]
public sealed class RerunPlanTests
{
    private static NodeId N(string id) => NodeId.From(id);

    // t -> a -(yes)-> b -> c ; a -(no)-> d
    private static GraphSpec Spec() => new(Version: 1, Name: "x", Description: null, Concurrency: null, Resources: null,
        Nodes: [.. new[] { "t", "a", "b", "c", "d" }.Select(id => new GraphNode(N(id), "http", Title: null, []))],
        Edges: [new GraphEdge(N("t"), Output: null, N("a")), new GraphEdge(N("a"), "yes", N("b")), new GraphEdge(N("b"), Output: null, N("c")), new GraphEdge(N("a"), "no", N("d"))]);

    [Fact]
    public void FromHere_is_the_node_and_everything_it_reaches_in_spec_order()
    {
        Assert.Equal(["b", "c"], RerunPlan.FromHere(Spec(), N("b")).Select(static i => i.Value));
        Assert.Equal(["a", "b", "c", "d"], RerunPlan.FromHere(Spec(), N("a")).Select(static i => i.Value));
    }

    [Fact]
    public void Before_is_what_the_node_reads_from_and_not_the_node_or_a_side_branch()
    {
        Assert.Equal(["t", "a"], RerunPlan.Before(Spec(), N("b")).Select(static i => i.Value));
        Assert.Empty(RerunPlan.Before(Spec(), N("t")));
    }

    [Fact]
    public void A_restored_step_took_the_branches_into_steps_the_source_did_not_skip()
    {
        var taken = RerunPlan.Taken(Spec(), N("a"), new HashSet<NodeId> { N("d") });

        Assert.Equal(["yes"], taken);
    }

    [Fact]
    public void An_edge_with_no_named_output_is_the_out_branch()
    {
        Assert.Equal(["out"], RerunPlan.Taken(Spec(), N("t"), new HashSet<NodeId>()));
    }
}
