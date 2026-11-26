using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>
/// #4334 (and the server half of #3305): the runs read filters by status and start time in the store's query, so a filter reaches past the newest 50,
/// and pages past the cap with 'before' and the answer's 'next'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowRunsFilterTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("busy");
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowRunStatus[] _cycle = [WorkflowRunStatus.Succeeded, WorkflowRunStatus.Succeeded, WorkflowRunStatus.Failed, WorkflowRunStatus.Cancelled, WorkflowRunStatus.Running];

    private readonly FakeTimeProvider _clock = new(_start);
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ListWorkflowRunsService _service;

    public ListWorkflowRunsFilterTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new ListWorkflowRunsService(_workflows, _runs);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowRunId Id(int occurrence) => WorkflowRunId.Of(_slug, 1, occurrence);

    // Every ten minutes from the start, a run; statuses cycle succeeded, succeeded, failed, cancelled, running.
    private static DateTimeOffset At(int occurrence) => _start.AddMinutes(10 * occurrence);

    [Fact]
    public async Task A_status_filter_keeps_only_runs_in_that_status()
    {
        await Seed(10);

        var failed = await Read(RunsFilter.None with { Statuses = [WorkflowRunStatus.Failed] });
        var cancelled = await Read(RunsFilter.None with { Statuses = [WorkflowRunStatus.Cancelled] });

        Assert.Equal([Id(8), Id(3)], failed.Runs.Select(static r => r.Id));
        Assert.Equal([Id(9), Id(4)], cancelled.Runs.Select(static r => r.Id));
        Assert.All(failed.Runs, static r => Assert.Equal("failed", r.Status));
    }

    [Fact]
    public async Task Since_and_until_keep_runs_that_started_in_the_window()
    {
        await Seed(10);

        var since = await Read(RunsFilter.None with { Since = At(8) });
        var window = await Read(RunsFilter.None with { Since = At(2), Until = At(5) });

        Assert.Equal([Id(10), Id(9), Id(8)], since.Runs.Select(static r => r.Id));
        Assert.Equal([Id(4), Id(3), Id(2)], window.Runs.Select(static r => r.Id));
    }

    [Fact]
    public async Task Yesterdays_failed_run_of_a_workflow_that_runs_every_ten_minutes_is_found_past_the_newest_fifty()
    {
        await Seed(200);   // 200 runs over 33 hours: run 3 is far past the newest 50

        var newest = await Read(RunsFilter.None);
        var found = await Read(new RunsFilter([WorkflowRunStatus.Failed], Since: At(1), Until: At(6), Before: null));

        Assert.DoesNotContain(newest.Runs, static r => r.Id.Equals(Id(3)));
        Assert.Equal([Id(3)], found.Runs.Select(static r => r.Id));
        Assert.False(found.More);
        Assert.Null(found.Next);
    }

    [Fact]
    public async Task Before_pages_past_the_newest_fifty_and_next_is_the_last_run_of_the_page()
    {
        await Seed(120);

        var first = await Read(RunsFilter.None);
        var second = await Read(RunsFilter.None with { Before = first.Next });
        var third = await Read(RunsFilter.None with { Before = second.Next });

        Assert.Equal((50, true, (WorkflowRunId?)Id(71)), (first.Runs.Count, first.More, first.Next));
        Assert.Equal((Id(70), 50, true, (WorkflowRunId?)Id(21)), (second.Runs[0].Id, second.Runs.Count, second.More, second.Next));
        Assert.Equal((Id(20), Id(1), 20, false), (third.Runs[0].Id, third.Runs[^1].Id, third.Runs.Count, third.More));
        Assert.Null(third.Next);
    }

    [Fact]
    public async Task Before_and_a_status_filter_page_together()
    {
        await Seed(300);   // 60 failed runs: 3, 8, ..., 298

        var first = await Read(RunsFilter.None with { Statuses = [WorkflowRunStatus.Failed] });
        var second = await Read(RunsFilter.None with { Statuses = [WorkflowRunStatus.Failed], Before = first.Next });

        Assert.Equal((50, true, Id(298), (WorkflowRunId?)Id(53)), (first.Runs.Count, first.More, first.Runs[0].Id, first.Next));
        Assert.Equal((10, false, Id(48), Id(3)), (second.Runs.Count, second.More, second.Runs[0].Id, second.Runs[^1].Id));
    }

    [Fact]
    public async Task A_before_run_of_another_workflow_is_SND_GRAPH_134()
    {
        await Seed(3);
        var other = WorkflowId.From("WF-00000000000000000000000000000002");
        var stray = WorkflowRunId.Of(WorkflowSlug.From("quiet"), 1, 1);
        _ = (await _runs.Insert(new WorkflowRun(stray, other, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, At(1), EndedAt: null), Token)).Value;

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { Filter = RunsFilter.None with { Before = stray } }, Token);

        Assert.Equal(SindriErrors.GraphRunsFilterInvalid.Code, result.Error.Code);
        Assert.Contains("'quiet@1:1' is not a run of busy", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_before_run_that_does_not_exist_is_SND_GRAPH_101()
    {
        await Seed(3);

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { Filter = RunsFilter.None with { Before = Id(99) } }, Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }

    private async Task<ListWorkflowRunsResponse> Read(RunsFilter filter)
    {
        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { Filter = filter }, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task Seed(int count)
    {
        var spec = new GraphSpec(Version: 1, Name: "Busy", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "schedule", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        for (var n = 1; n <= count; n++)
        {
            var run = new WorkflowRun(Id(n), _id, 1, "{\"kind\":\"schedule\"}", _cycle[(n - 1) % _cycle.Length], Outputs: null, Error: null, ItemId: null, At(n), EndedAt: null);
            _ = (await _runs.Insert(run, Token)).Value;
        }
    }
}
