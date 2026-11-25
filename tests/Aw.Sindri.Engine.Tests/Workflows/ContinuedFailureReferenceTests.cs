using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A node that reads a field of a node that failed and continued: the reader fails with SND-GRAPH-131 instead of reading nothing (#3235).</summary>
[Lane(TestLane.Integration)]
public sealed class ContinuedFailureReferenceTests
{
    private static JsonObject Reads(string reference) => new() { ["text"] = $"got {{{{{reference}}}}}" };

    [Fact]
    public async Task A_reader_of_a_node_that_failed_and_continued_fails_with_a_named_error()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "fail", onFailure: "continue"), GraphRig.Node("b", "echo", config: Reads("a.x"))],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);
        var nodes = await rig.NodeStatuses(run.Id);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, nodes["b"]);
        Assert.Contains(SindriErrors.GraphReferenceToContinuedFailure.Code, run.Error, StringComparison.Ordinal);
        Assert.Contains("'a' failed and continued, so '{{a.x}}' has no value", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain($"{rig.Slug}@1:1/b", rig.Blocks.Log, StringComparer.Ordinal);   // why: the block never ran on a missing value
    }

    [Fact]
    public async Task A_reader_of_a_node_that_succeeded_gets_its_value()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo"), GraphRig.Node("b", "echo", config: Reads("a.node"))],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal("got a", (await rig.NodeInput(started.RunId, "b"))!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_node_that_does_not_read_the_continued_failure_runs_fine()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "fail", onFailure: "continue"), GraphRig.Node("b", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "b")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);
        var nodes = await rig.NodeStatuses(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, nodes["b"]);
    }

    [Fact]
    public async Task A_condition_that_reads_a_node_that_failed_and_continued_fails_with_the_same_error()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "fail", onFailure: "continue"), GraphRig.Node("c", "condition", config: new JsonObject { ["when"] = "a.x" }), GraphRig.Node("b", "echo")],
            [GraphRig.Edge("start", "a"), GraphRig.Edge("a", "c"), GraphRig.Edge("c", "b", "true")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited)).RunId);
        var nodes = await rig.NodeStatuses(run.Id);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, nodes["c"]);
        Assert.Contains(SindriErrors.GraphReferenceToContinuedFailure.Code, run.Error, StringComparison.Ordinal);
        Assert.Contains("'a' failed and continued", run.Error, StringComparison.Ordinal);
        Assert.False(nodes.TryGetValue("b", out var b) && b == NodeRunStatus.Succeeded);   // why: the branch behind the failed condition never ran
    }

    [Fact]
    public async Task A_for_each_body_node_that_reads_a_body_node_that_failed_and_continued_fails_with_the_same_error()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }), GraphRig.Node("a", "fail", onFailure: "continue"),
                GraphRig.Node("b", "echo", config: Reads("a.x")), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "gather")]));
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString();

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger);
        var run = await rig.Final(started.RunId);
        var bRuns = await rig.ItemRuns(started.RunId, "b");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, bRuns[1].Status);
        Assert.Contains(SindriErrors.GraphReferenceToContinuedFailure.Code, run.Error, StringComparison.Ordinal);
        Assert.Contains("'a' failed and continued", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_for_each_reader_fails_only_for_the_item_whose_node_failed_and_continued()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("each", "for_each", config: new JsonObject { ["items"] = "{{trigger.items}}" }),
                GraphRig.Node("a", "fail_on_two", onFailure: "continue", config: new JsonObject { ["value"] = "{{item}}" }),
                GraphRig.Node("b", "echo", config: Reads("a.node")), GraphRig.Node("gather", "collect", config: new JsonObject { ["value"] = "{{item}}" })],
            [GraphRig.Edge("start", "each"), GraphRig.Edge("each", "a"), GraphRig.Edge("a", "b"), GraphRig.Edge("b", "gather")]));
        var trigger = new JsonObject { ["kind"] = "manual", ["items"] = new JsonArray(1, 2) }.ToJsonString();

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger);
        var run = await rig.Final(started.RunId);
        var bRuns = await rig.ItemRuns(started.RunId, "b");

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Succeeded, bRuns[1].Status);   // why: item 1's 'a' succeeded, so its value reads fine
        Assert.Equal(NodeRunStatus.Failed, bRuns[2].Status);
        Assert.Contains(SindriErrors.GraphReferenceToContinuedFailure.Code, run.Error, StringComparison.Ordinal);
        Assert.Contains("'a' failed and continued", run.Error, StringComparison.Ordinal);
    }
}
