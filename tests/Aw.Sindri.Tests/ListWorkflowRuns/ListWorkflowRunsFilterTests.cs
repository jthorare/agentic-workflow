using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>Issue 3683, the runs read: duration, a one-line result, and the status and date filters with their counts ("23 of 500").</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowRunsFilterTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("sum");
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 42, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(_now);
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

    [Fact]
    public async Task A_run_that_ended_says_its_duration_and_a_running_one_has_none()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Succeeded, _now.AddMinutes(-10), ended: _now.AddMinutes(-10).AddSeconds(16), outputs: null, error: null);
        _ = await Insert(2, WorkflowRunStatus.Running, _now.AddMinutes(-1), ended: null, outputs: null, error: null);

        var runs = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs;

        Assert.Null(runs[0].DurationMs);
        Assert.Equal(16_000, runs[1].DurationMs);
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

    [Fact]
    public async Task The_status_filter_keeps_that_status_and_counts_matching_against_total()
    {
        await Create();
        for (var n = 1; n <= 10; n++)
        {
            _ = await Insert(n, n % 4 == 0 ? WorkflowRunStatus.Failed : WorkflowRunStatus.Succeeded, _now.AddMinutes(-100 + n), ended: null, outputs: null, error: null);
        }

        var result = (await _service.Handle(new ListWorkflowRunsRequest(_slug) { Status = "failed" }, Token)).Value;

        Assert.Equal(2, result.Runs.Count);
        Assert.All(result.Runs, static r => Assert.Equal("failed", r.Status));
        Assert.Equal((2, 10), (result.Matching, result.Total));
    }

    [Fact]
    public async Task The_date_range_is_inclusive_at_from_and_exclusive_at_to()
    {
        await Create();
        for (var n = 1; n <= 6; n++)
        {
            _ = await Insert(n, WorkflowRunStatus.Succeeded, _now.AddHours(-n), ended: null, outputs: null, error: null);
        }

        var result = (await _service.Handle(new ListWorkflowRunsRequest(_slug) { From = _now.AddHours(-4), To = _now.AddHours(-1) }, Token)).Value;

        Assert.Equal([2, 3, 4], result.Runs.Select(static r => r.Id.Occurrence).Order());   // -2h, -3h, -4h; -1h is excluded by To
        Assert.Equal((3, 6), (result.Matching, result.Total));
    }

    [Fact]
    public async Task Filters_apply_before_paging_so_an_offset_walks_the_matching_runs()
    {
        await Create();
        for (var n = 1; n <= 120; n++)
        {
            _ = await Insert(n, n % 2 == 0 ? WorkflowRunStatus.Failed : WorkflowRunStatus.Succeeded, _now.AddMinutes(-200 + n), ended: null, outputs: null, error: null);
        }

        var page = (await _service.Handle(new ListWorkflowRunsRequest(_slug, Offset: 50, Limit: 10) { Status = "failed" }, Token)).Value;

        Assert.Equal(10, page.Runs.Count);
        Assert.Equal(20, page.Runs[0].Id.Occurrence);   // 60 failed runs, newest 120; offset 50 starts at the 51st: occurrence 20
        Assert.False(page.More);
        Assert.Equal((60, 120), (page.Matching, page.Total));
    }

    [Fact]
    public async Task A_filter_with_no_match_is_an_empty_page_not_an_error()
    {
        await Create();
        _ = await Insert(1, WorkflowRunStatus.Succeeded, _now.AddMinutes(-5), ended: null, outputs: null, error: null);

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { Status = "failed" }, Token);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Runs);
        Assert.Equal((0, 1), (result.Value.Matching, result.Value.Total));
    }

    [Fact]
    public async Task A_status_that_is_not_a_status_word_is_invalid_input()
    {
        await Create();

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { Status = "exploded" }, Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_range_that_ends_before_it_starts_is_invalid_input()
    {
        await Create();

        var result = await _service.Handle(new ListWorkflowRunsRequest(_slug) { From = _now, To = _now.AddDays(-1) }, Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
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
