using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>Issue 3683, the runs read: seconds, a one-line result and the trigger words. (The status and date filters are the client's, per ui.)</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowRunsReadTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("sum");
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 42, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(_now);
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ListWorkflowRunsService _service;

    public ListWorkflowRunsReadTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new ListWorkflowRunsService(_workflows, _runs);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_that_ended_says_its_duration_and_a_running_one_has_none()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Succeeded, _now.AddMinutes(-10), ended: _now.AddMinutes(-10).AddSeconds(16), outputs: null, error: null);
        _ = await Insert(2, WorkflowRunStatus.Running, _now.AddMinutes(-1), ended: null, outputs: null, error: null);

        var runs = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs;

        Assert.Null(runs[0].Seconds);
        Assert.Equal(16.0, runs[1].Seconds);
    }

    [Fact]
    public async Task A_failed_run_s_result_is_the_first_line_of_its_error()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Failed, _now.AddMinutes(-5), ended: _now.AddMinutes(-5).AddSeconds(2), outputs: null, error: "sum failed: code runner not found (SND-CODE-005)\nat node 'sum'");

        var run = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs.Single();

        Assert.Equal("sum failed: code runner not found (SND-CODE-005)", run.Result);
    }

    [Fact]
    public async Task A_succeeded_run_s_result_is_its_outputs_as_compact_json_cut_at_120_characters()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Succeeded, _now.AddMinutes(-9), ended: _now.AddMinutes(-9).AddSeconds(1), outputs: "{ \"sum\": { \"total\": 3 } }", error: null);
        _ = await Insert(2, WorkflowRunStatus.Succeeded, _now.AddMinutes(-8), ended: _now.AddMinutes(-8).AddSeconds(1), outputs: "{\"big\":\"" + new string('x', 300) + "\"}", error: null);
        _ = await Insert(3, WorkflowRunStatus.Succeeded, _now.AddMinutes(-7), ended: _now.AddMinutes(-7).AddSeconds(1), outputs: null, error: null);

        var runs = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs;

        Assert.Null(runs[0].Result);
        Assert.Equal(121, runs[1].Result!.Length);
        Assert.EndsWith("…", runs[1].Result, StringComparison.Ordinal);
        Assert.Equal("{\"sum\":{\"total\":3}}", runs[2].Result);
    }

    [Theory]
    [InlineData("{\"kind\":\"manual\",\"by\":\"owner\"}", "manual")]
    [InlineData("{\"kind\":\"manual\",\"by\":\"session:s1\"}", "agent")]
    [InlineData("{\"kind\":\"manual\",\"by\":\"cli\"}", "cli")]
    [InlineData("{\"kind\":\"schedule\",\"scheduledFor\":\"2026-11-14T10:40:00Z\"}", "schedule")]
    public async Task The_trigger_is_one_of_manual_schedule_cli_or_agent(string stored, string served)
    {
        await Create();
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, 1), _id, 1, stored, WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, _now.AddMinutes(-5), _now.AddMinutes(-4));
        _ = (await _runs.Insert(run, Token)).Value;

        var row = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs.Single();

        Assert.Equal(served, row.Trigger);
    }

    private async Task Create()
    {
        var spec = new GraphSpec(Version: 1, Name: "Sum numbers", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
    }

    private async Task<WorkflowRun> Insert(int occurrence, WorkflowRunStatus status, DateTimeOffset startedAt, DateTimeOffset? ended, string? outputs, string? error)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _id, 1, "{\"kind\":\"manual\"}", status, outputs, error, ItemId: null, startedAt, ended);
        return (await _runs.Insert(run, Token)).Value;
    }
}
