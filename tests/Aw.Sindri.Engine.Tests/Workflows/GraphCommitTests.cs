using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A block's after-success hook (agent-first PR 9, for 'only_new'): it runs once the run succeeded, and a failure in it fails the run.</summary>
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
    public async Task A_commit_that_fails_fails_the_run()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("a", "commit_probe", config: new System.Text.Json.Nodes.JsonObject { ["failCommit"] = true })], [GraphRig.Edge("start", "a")]));

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("commit failed", run.Error, StringComparison.Ordinal);
    }

    private const string TwoIssues = """{"kind":"manual","issues":[{"id":1},{"id":2}]}""";

    private static System.Text.Json.Nodes.JsonObject FreshConfig() => new() { ["items"] = "{{trigger.issues}}", ["key"] = "id" };

    [Fact]
    public async Task A_run_that_fails_after_only_new_gives_its_items_back_to_the_next_run()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("fresh", "only_new", config: FreshConfig()), GraphRig.Node("boom", "fail")],
            [GraphRig.Edge("start", "fresh"), GraphRig.Edge("fresh", "boom")]));

        var first = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoIssues)).RunId);
        var second = await rig.Final((await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Unlimited, TwoIssues)).RunId);

        Assert.Equal(WorkflowRunStatus.Failed, first.Status);
        Assert.Equal(0, rig.Seen.Count);
        Assert.Equal(2, (await rig.NodeOutput(second.Id, "fresh"))!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_run_that_succeeds_after_only_new_remembers_its_items_so_the_next_run_passes_none()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("fresh", "only_new", config: FreshConfig()), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "fresh"), GraphRig.Edge("fresh", "after")]));

        var first = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoIssues)).RunId);
        var second = await rig.Final((await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Unlimited, TwoIssues)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, first.Status);
        Assert.Equal(2, rig.Seen.Count);
        Assert.Equal(0, (await rig.NodeOutput(second.Id, "fresh"))!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_run_that_succeeds_after_a_failed_node_that_continues_still_commits_the_nodes_that_worked()
    {
        await using var rig = GraphRig.Start(server.Environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("fresh", "only_new", config: FreshConfig()), GraphRig.Node("boom", "fail", onFailure: "continue")],
            [GraphRig.Edge("start", "fresh"), GraphRig.Edge("fresh", "boom")]));

        var run = await rig.Final((await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, TwoIssues)).RunId);

        Assert.Equal(WorkflowRunStatus.Succeeded, run.Status);
        Assert.Equal(2, rig.Seen.Count);
    }
}
