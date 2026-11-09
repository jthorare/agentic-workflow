using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Launch;
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
    private readonly InMemoryWorkflowStore _workflows;
    private readonly FakeLaunchFiles _files = new();
    private readonly FakeAgentNodeControl _control = new();
    private readonly GetWorkflowRunService _service;

    public GetWorkflowRunServiceTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _workflows = new InMemoryWorkflowStore(_clock);
        _service = new GetWorkflowRunService(_runs, _workflows, _files, _control);
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

    [Fact]
    public async Task A_run_agent_node_the_run_waits_on_shows_waiting_and_every_other_row_shows_none()
    {
        await InsertAgentRun(WorkflowRunStatus.Running, NodeRunStatus.Running);
        _control.Wait(NodeId.From("agent"));

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.Equal([("agent", "waiting"), ("start", null)], result.Value.Nodes.Select(static n => (n.Node.Value, n.Agent)));
    }

    [Fact]
    public async Task A_run_agent_node_a_person_holds_shows_taken_over()
    {
        await InsertAgentRun(WorkflowRunStatus.Running, NodeRunStatus.Running);
        _control.Wait(NodeId.From("agent"), takenOver: true);

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.Equal("taken_over", result.Value.Nodes.Single(static n => n.Node.Value == "agent").Agent);
    }

    [Fact]
    public async Task A_read_right_after_a_takeover_says_taken_over_before_the_launch_marker_is_written()
    {
        await InsertAgentRun(WorkflowRunStatus.Running, NodeRunStatus.Running);
        _control.Wait(NodeId.From("agent"));
        _ = (await _control.Takeover(_run, NodeId.From("agent"), 0, Token)).Value;   // the workflow's update; its next round writes the launch marker, which is not there yet

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.Equal("taken_over", result.Value.Nodes.Single(static n => n.Node.Value == "agent").Agent);
    }

    [Fact]
    public async Task A_row_the_run_does_not_wait_on_shows_none_even_with_a_stale_held_marker()
    {
        await InsertAgentRun(WorkflowRunStatus.Running, NodeRunStatus.Running);   // the run waits on no agent node (the query answers null)
        _files.MarkTakenOver(NodeRunAddress.Of(_run, NodeId.From("agent")).Launch(), taken: true);

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.All(result.Value.Nodes, static n => Assert.Null(n.Agent));
    }

    [Theory]
    [InlineData(false, "waiting")]
    [InlineData(true, "taken_over")]
    public async Task When_the_run_cannot_be_asked_the_launch_marker_answers(bool marked, string expected)
    {
        await InsertAgentRun(WorkflowRunStatus.Running, NodeRunStatus.Running);
        _control.Fail = SindriErrors.EngineUnavailable.With("temporal is down");
        _files.MarkTakenOver(NodeRunAddress.Of(_run, NodeId.From("agent")).Launch(), marked);

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.Equal(expected, result.Value.Nodes.Single(static n => n.Node.Value == "agent").Agent);
    }

    [Theory]
    [InlineData(NodeRunStatus.Succeeded)]
    [InlineData(NodeRunStatus.Failed)]
    [InlineData(NodeRunStatus.Cancelled)]
    public async Task A_run_agent_node_that_ended_shows_none_even_with_a_stale_held_marker(NodeRunStatus status)
    {
        await InsertAgentRun(WorkflowRunStatus.Running, status);
        _control.Wait(NodeId.From("agent"), takenOver: true);
        _files.MarkTakenOver(NodeRunAddress.Of(_run, NodeId.From("agent")).Launch(), taken: true);

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.All(result.Value.Nodes, static n => Assert.Null(n.Agent));
    }

    [Fact]
    public async Task A_run_that_ended_shows_none_on_a_row_still_marked_running()
    {
        await InsertAgentRun(WorkflowRunStatus.Cancelled, NodeRunStatus.Running);
        _control.Wait(NodeId.From("agent"));

        var result = await _service.Handle(new GetWorkflowRunRequest(_run), Token);

        Assert.All(result.Value.Nodes, static n => Assert.Null(n.Agent));
    }

    private async Task InsertAgentRun(WorkflowRunStatus status, NodeRunStatus agent)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("agent"), "run_agent", "Check", new JsonObject { ["prompt"] = "go" })],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("agent"))]);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _runs.Insert(Run(status, outputs: null, error: null) with { Revision = 1 }, Token)).Value;
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From("start"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{}", Truncated: false, Error: null, Usage: null), Token)).Value;
        _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From("agent"), 0, agent, Input: "{\"prompt\":\"go\"}", Output: null, Truncated: false, Error: null, Usage: null), Token)).Value;
    }

    private WorkflowRun Run(WorkflowRunStatus status, string? outputs, string? error) =>
        new(_run, _id, 2, "{\"kind\":\"manual\",\"by\":\"owner\"}", status, outputs, error, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
}
