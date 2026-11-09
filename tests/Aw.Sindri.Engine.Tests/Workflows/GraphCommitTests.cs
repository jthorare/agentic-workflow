using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A block's after-success hook (agent-first PR 9, for 'only_new'): it runs once the node succeeded, and a failure in it fails the node.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCommitTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    [Fact]
    public async Task A_node_that_succeeded_is_committed_after_it_ran()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("a", "commit_probe")], [GraphRig.Edge("start", "a")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal([$"exec {rig.Slug}@1:1/a", $"commit {rig.Slug}@1:1/a"], rig.Blocks.Log);
    }

    [Fact]
    public async Task A_node_that_failed_is_not_committed()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "commit_probe", config: new System.Text.Json.Nodes.JsonObject { ["failExecute"] = true })], [GraphRig.Edge("start", "a")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal([$"exec {rig.Slug}@1:1/a"], rig.Blocks.Log);
    }

    [Fact]
    public async Task A_commit_that_fails_fails_the_node_and_the_run()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "commit_probe", config: new System.Text.Json.Nodes.JsonObject { ["failCommit"] = true })], [GraphRig.Edge("start", "a")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("commit failed", run.Error, StringComparison.Ordinal);
        Assert.Equal(NodeRunStatus.Failed, (await rig.NodeStatuses(started.RunId))["a"]);
    }
}
