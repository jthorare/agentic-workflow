using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflowRun;

/// <summary>'sindri wf run-show': a run with its node runs; an unknown run is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowRunServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 2, 7);

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly GetWorkflowRunService _service;

    public GetWorkflowRunServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new GetWorkflowRunService(_runs);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_running_run_shows_its_trigger_and_the_node_runs_so_far()
    {
        _ = (await _runs.Insert(Run(WorkflowRunStatus.Running, outputs: null, error: null), Token)).Value;
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From("start"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{\"at\":\"now\"}", Truncated: false, Error: null, Usage: null), Token)).Value;
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From("agent"), 0, NodeRunStatus.Running, Input: "{\"prompt\":\"check\"}", Output: null, Truncated: false, Error: null, Usage: null), Token)).Value;

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.True(result.IsSuccess);
        var shown = result.Value;
        Assert.Equal((_run, _slug, 2, "running"), (shown.Id, shown.Slug, shown.Revision, shown.Status));
        Assert.Equal("manual", shown.Trigger!["kind"]!.GetValue<string>());
        Assert.Null(shown.EndedAt);
        Assert.Equal(["agent", "start"], shown.Nodes.Select(static n => n.Node.Value), StringComparer.Ordinal);
        Assert.Equal(("running", "check"), (shown.Nodes[0].Status, shown.Nodes[0].Input!["prompt"]!.GetValue<string>()));
        Assert.Equal("now", shown.Nodes[1].Output!["at"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_finished_run_shows_its_outputs_and_error_and_end_time()
    {
        _ = (await _runs.Insert(Run(WorkflowRunStatus.Running, outputs: null, error: null), Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(2));
        _ = (await _runs.SetStatus(_run, WorkflowRunStatus.Failed, "{\"count\":3}", "node 'agent' failed", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(("failed", "node 'agent' failed", 3), (result.Value.Status, result.Value.Error, result.Value.Outputs!["count"]!.GetValue<int>()));
        Assert.Equal(_clock.GetUtcNow(), result.Value.EndedAt);
    }

    [Fact]
    public async Task A_node_payload_the_store_cut_short_comes_back_as_text_with_the_truncated_flag()
    {
        _ = (await _runs.Insert(Run(WorkflowRunStatus.Running, outputs: null, error: null), Token)).Value;
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From("agent"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{\"text\":\"cut off mid", Truncated: true, Error: null, Usage: null), Token)).Value;

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        var node = Assert.Single(result.Value.Nodes);
        Assert.True(node.Truncated);
        Assert.Equal("{\"text\":\"cut off mid", node.Output!.GetValue<string>());
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }

    private WorkflowRun Run(WorkflowRunStatus status, string? outputs, string? error) =>
        new(_run, _id, 2, "{\"kind\":\"manual\",\"by\":\"owner\"}", status, outputs, error, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
}
