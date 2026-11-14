using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflowRun;

/// <summary>Issue 3683, the run read: seconds on each node and the run's trigger kind in the list's four words.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowRunTimingTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("sum");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 7);
    private static readonly DateTimeOffset _start = new(2026, 11, 14, 8, 40, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(_start);
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly GetWorkflowRunService _service;

    public GetWorkflowRunTimingTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new GetWorkflowRunService(_runs, new InMemoryWorkflowStore(_clock), new FakeLaunchFiles(), new FakeAgentNodeControl());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_node_that_ended_says_its_seconds_and_a_running_node_has_none()
    {
        await Insert("{\"kind\":\"manual\",\"by\":\"owner\"}");
        _ = (await _runs.RecordNodeRun(Node("start", NodeRunStatus.Succeeded, _start, _start.AddMilliseconds(1500)), Token)).Value;
        _ = (await _runs.RecordNodeRun(Node("sum", NodeRunStatus.Running, _start.AddSeconds(2), ended: null), Token)).Value;
        _ = (await _runs.RecordNodeRun(Node("old", NodeRunStatus.Succeeded, started: null, ended: null), Token)).Value;   // a row written before times were kept

        var nodes = (await _service.Handle(new GetWorkflowRunRequest(_run), Token)).Value.Nodes.ToDictionary(static n => n.Node.Value, StringComparer.Ordinal);

        Assert.Equal(1.5, nodes["start"].Seconds);
        Assert.Null(nodes["sum"].Seconds);
        Assert.Null(nodes["old"].Seconds);
    }

    [Theory]
    [InlineData("{\"kind\":\"manual\",\"by\":\"owner\"}", "manual")]
    [InlineData("{\"kind\":\"manual\",\"by\":\"session:s1\"}", "agent")]
    [InlineData("{\"kind\":\"manual\",\"by\":\"cli\"}", "cli")]
    [InlineData("{\"kind\":\"schedule\"}", "schedule")]
    public async Task The_run_says_its_trigger_kind_in_the_lists_words(string stored, string served)
    {
        await Insert(stored);

        var shown = (await _service.Handle(new GetWorkflowRunRequest(_run), Token)).Value;

        Assert.Equal(served, shown.TriggerKind);
    }

    private static NodeRun Node(string id, NodeRunStatus status, DateTimeOffset? started, DateTimeOffset? ended) =>
        new(_run, NodeId.From(id), 0, status, Input: null, Output: null, Truncated: false, Error: null, Usage: null, Raw: null, started, ended);

    private async Task Insert(string trigger)
    {
        var run = new WorkflowRun(_run, _id, 1, trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, _start, EndedAt: null);
        _ = (await _runs.Insert(run, Token)).Value;
    }
}
