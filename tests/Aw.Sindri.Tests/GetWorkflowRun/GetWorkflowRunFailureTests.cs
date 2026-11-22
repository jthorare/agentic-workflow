using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflowRun;

/// <summary>#3304 (spec §6.1, §7.3): the run read gives a failed node run its sentence and fix, with the step named by its title, and keeps the engine's text.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowRunFailureTests
{
    private const string Threw = "SND-CODE-004: InvalidOperationException: repo is 812 MB, over the 500 MB budget";

    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("size-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<GetWorkflowRunResponse> Read(string? checkTitle, string checkError)
    {
        var clock = new FakeTimeProvider();
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var spec = new GraphSpec(Version: 1, Name: "Size check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("check"), "code", checkTitle, [])],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("check"))]);
        _ = (await workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await runs.Insert(new WorkflowRun(_run, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Failed, Outputs: null, Error: "node 'check' failed: " + checkError, ItemId: null, clock.GetUtcNow(), EndedAt: clock.GetUtcNow()), Token)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(_run, NodeId.From("start"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{}", Truncated: false, Error: null, Usage: null), Token)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(_run, NodeId.From("check"), 0, NodeRunStatus.Failed, Input: "{}", Output: null, Truncated: false, checkError, Usage: null), Token)).Value;
        var service = new GetWorkflowRunService(runs, workflows, new FakeLaunchFiles(), new FakeAgentNodeControl());

        var result = await service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task A_failed_code_step_reads_as_its_title_stopped_with_the_message_and_keeps_the_raw_error_as_detail()
    {
        var read = await Read("Check", Threw);

        var failure = read.Nodes.Single(static n => n.Node.Value == "check").Failure;
        Assert.NotNull(failure);
        Assert.Equal("Check stopped: repo is 812 MB, over the 500 MB budget.", failure.Sentence);
        Assert.Equal("Open Check", failure.FixLabel);
        Assert.Equal(Threw, failure.Detail);
        Assert.Equal(Threw, read.Nodes.Single(static n => n.Node.Value == "check").Error);
        Assert.Null(read.Nodes.Single(static n => n.Node.Value == "start").Failure);
    }

    [Fact]
    public async Task A_step_with_no_title_is_named_by_its_block_kind_never_its_node_id_in_quotes()
    {
        var read = await Read(checkTitle: null, Threw);

        var failure = read.Nodes.Single(static n => n.Node.Value == "check").Failure;
        Assert.NotNull(failure);
        Assert.DoesNotContain("node '", failure.Sentence, StringComparison.Ordinal);
        Assert.Equal("Code stopped: repo is 812 MB, over the 500 MB budget.", failure.Sentence);
    }

    [Fact]
    public async Task A_failed_step_whose_code_has_no_sentence_has_no_failure_and_keeps_its_error()
    {
        var read = await Read("Check", "SND-NOPE-999: something else");

        var row = read.Nodes.Single(static n => n.Node.Value == "check");
        Assert.Null(row.Failure);
        Assert.Equal("SND-NOPE-999: something else", row.Error);
    }
}
