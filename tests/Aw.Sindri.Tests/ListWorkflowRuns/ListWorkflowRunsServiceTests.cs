using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>'sindri wf runs': a workflow's newest runs, newest first, with the status word and the trigger's kind.</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowRunsServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ListWorkflowRunsService _service;

    public ListWorkflowRunsServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new ListWorkflowRunsService(_workflows, _runs);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_workflow_that_never_ran_has_an_empty_list()
    {
        await Create();

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(_slug, result.Value.Slug);
        Assert.Empty(result.Value.Runs);
    }

    [Fact]
    public async Task Runs_come_newest_first_with_their_status_trigger_and_error()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Succeeded, "manual", error: null);
        _ = await Insert(2, WorkflowRunStatus.Failed, "schedule", error: "node 'agent' failed");
        _ = await Insert(3, WorkflowRunStatus.Running, "manual", error: null);

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal([WorkflowRunId.Of(_slug, 1, 3), WorkflowRunId.Of(_slug, 1, 2), WorkflowRunId.Of(_slug, 1, 1)], result.Value.Runs.Select(static r => r.Id));
        Assert.Equal(["running", "failed", "succeeded"], result.Value.Runs.Select(static r => r.Status), StringComparer.Ordinal);
        Assert.Equal(["manual", "schedule", "manual"], result.Value.Runs.Select(static r => r.Trigger), StringComparer.Ordinal);
        Assert.Equal("node 'agent' failed", result.Value.Runs[1].Error);
    }

    [Fact]
    public async Task The_list_keeps_the_newest_fifty_runs()
    {
        await Create();
        for (var n = 1; n <= ListWorkflowRunsService.Newest + 2; n++)
        {
            _ = await Insert(n, WorkflowRunStatus.Succeeded, "manual", error: null);
        }

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug), Token);

        Assert.Equal(ListWorkflowRunsService.Newest, result.Value.Runs.Count);
        Assert.Equal(WorkflowRunId.Of(_slug, 1, ListWorkflowRunsService.Newest + 2), result.Value.Runs[0].Id);
    }

    [Fact]
    public async Task An_offset_past_fifty_still_returns_rows_and_more_says_whether_older_runs_follow()
    {
        await Create();
        for (var n = 1; n <= 120; n++)
        {
            _ = await Insert(n, WorkflowRunStatus.Succeeded, "manual", error: null);
        }

        var middle = await _service.Handle(new ListWorkflowRunsRequest(_slug, Offset: 100, Limit: 10), Token);
        var last = await _service.Handle(new ListWorkflowRunsRequest(_slug, Offset: 110, Limit: 10), Token);

        Assert.Equal(10, middle.Value.Runs.Count);
        Assert.Equal(WorkflowRunId.Of(_slug, 1, 20), middle.Value.Runs[0].Id);
        Assert.True(middle.Value.More);
        Assert.Equal(10, last.Value.Runs.Count);
        Assert.False(last.Value.More);
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        var result = await _service.Handle(new ListWorkflowRunsRequest(WorkflowSlug.From("nope")), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
    }

    private async Task Create()
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
    }

    private async Task<WorkflowRun> Insert(int occurrence, WorkflowRunStatus status, string kind, string? error)
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _id, 1, $"{{\"kind\":\"{kind}\"}}", status, Outputs: null, error, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        return (await _runs.Insert(run, Token)).Value;
    }
}
