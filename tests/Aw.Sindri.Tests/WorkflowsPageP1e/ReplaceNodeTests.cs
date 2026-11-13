using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Ops;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Tests.Graph.Blocks;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.WorkflowsPageP1e;

/// <summary>replace_node (Workflows page spec decision 5, E3, #3193): the trigger swaps in place, keeps its id and outgoing edges, and a reference the new trigger can't answer is a warning, not a refusal.</summary>
[Lane(TestLane.Unit)]
public sealed class ReplaceNodeTests
{
    private static GraphValidator RealValidator() =>
        Validator(blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types);

    // review: a PR trigger -> an agent -> a notification; step 2 reads {{trigger.title}}
    private static GraphSpec Review() => Spec(
        [
            new GraphNode(NodeId.From("pr"), "connection_event", "PR opened", new JsonObject { ["connection"] = "github:work", ["event"] = "github.pr.opened" }),
            Agent("review", "acme/api", "Review {{trigger.title}}"),
            Notify("tell", "{{review.status}}"),
        ],
        [Edge("pr", "review"), Edge("review", "tell")]);

    private static ReplaceNodeOp ToManual() => new(NodeId.From("pr"), "manual", Title: "By hand", []);

    [Fact]
    public void Swapping_the_trigger_keeps_the_node_id_and_its_outgoing_edges()
    {
        var result = GraphOps.ApplyWithGaps(Review(), baseRevision: 2, currentRevision: 2, [ToManual()], RealValidator());

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var spec = result.Value.Spec;
        var swapped = spec.Nodes.Single(n => n.Id.Value == "pr");
        Assert.Equal(("manual", "By hand"), (swapped.Type, swapped.Title));
        Assert.Equal(3, spec.Nodes.Count);
        Assert.Equal(["pr->review", "review->tell"], spec.Edges.Select(e => $"{e.From.Value}->{e.To.Value}"), StringComparer.Ordinal);
    }

    [Fact]
    public void Swapping_the_trigger_while_a_step_reads_a_field_the_new_one_lacks_saves_and_marks_the_reference_as_a_wiring_gap()
    {
        var result = GraphOps.ApplyWithGaps(Review(), baseRevision: 2, currentRevision: 2, [ToManual()], RealValidator());

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var gap = Assert.Single(result.Value.Gaps, static f => f.Code.StartsWith("GRAPH-REF-", StringComparison.Ordinal));
        Assert.True(gap.WiringGap);
        Assert.Equal(GraphSeverity.Warning, gap.Severity);
        Assert.Contains("review", gap.Nodes, StringComparer.Ordinal);
    }

    [Fact]
    public void Replacing_a_node_that_is_not_the_trigger_is_refused_with_SND_GRAPH_112()
    {
        var result = GraphOps.ApplyWithGaps(Review(), 2, 2, [new ReplaceNodeOp(NodeId.From("review"), "notify", Title: null, new JsonObject { ["title"] = "x" })], RealValidator());

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-112", result.Error.Code);
        Assert.Contains("Only the trigger can be replaced.", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorKind.Invalid, SindriErrors.GraphOnlyTriggerReplaceable.Kind);
    }

    [Fact]
    public void Replacing_the_trigger_with_a_non_trigger_block_is_refused()
    {
        var result = GraphOps.ApplyWithGaps(Review(), 2, 2, [new ReplaceNodeOp(NodeId.From("pr"), "notify", Title: null, new JsonObject { ["title"] = "x" })], RealValidator());

        Assert.True(result.IsError);
    }

    [Fact]
    public void A_replace_and_a_set_config_are_checked_as_one_edit_and_a_failing_second_op_applies_neither()
    {
        var spec = Review();

        var result = GraphOps.ApplyWithGaps(spec, 2, 2, [ToManual(), new ConnectOp(NodeId.From("review"), NodeId.From("ghost"))], RealValidator());

        Assert.True(result.IsError);
        Assert.Equal("connection_event", spec.Nodes.Single(n => n.Id.Value == "pr").Type);
    }
}
