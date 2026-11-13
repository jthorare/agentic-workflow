using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflowRun;

/// <summary>P3e (#3196, spec §7.1, E4): the run read gives each node run its Raw text, so the run page can show the Raw answer tab.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowRunRawTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("prompt-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_prompt_node_run_shows_its_raw_text_and_a_succeeded_one_shows_none()
    {
        var clock = new FakeTimeProvider();
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var spec = new GraphSpec(Version: 1, Name: "Prompt check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("ask"), "prompt", "Ask", [])],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("ask"))]);
        _ = (await workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await runs.Insert(new WorkflowRun(_run, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Failed, Outputs: null, Error: "x", ItemId: null, clock.GetUtcNow(), EndedAt: clock.GetUtcNow()), Token)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(_run, NodeId.From("start"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{}", Truncated: false, Error: null, Usage: null), Token)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(_run, NodeId.From("ask"), 0, NodeRunStatus.Failed, Input: "{}", Output: null, Truncated: false, Error: "SND-GRAPH-103: x", Usage: null, Raw: "Usage limit reached."), Token)).Value;
        var service = new GetWorkflowRunService(runs, workflows, new FakeLaunchFiles(), new FakeAgentNodeControl());

        var result = await service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Usage limit reached.", result.Value.Nodes.Single(static n => n.Node.Value == "ask").Raw);
        Assert.Null(result.Value.Nodes.Single(static n => n.Node.Value == "start").Raw);
    }
}
