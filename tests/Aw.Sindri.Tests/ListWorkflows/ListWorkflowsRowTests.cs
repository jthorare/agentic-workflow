using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflows;

/// <summary>Issue 3683, the list rows: trigger in words, state, last run, next run, the 10-run strip, "Needs a look" and the header counts. The clock reads Sat 14 Nov 2026 10:42 UTC, as the design frames do.</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowsRowTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 42, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(_now);
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ListWorkflowsService _service;

    public ListWorkflowsRowTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new ListWorkflowsService(_workflows, _runs, _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("manual", null, "By hand")]
    [InlineData("schedule", "0 7 * * 1-5", "Every weekday 7:00")]
    [InlineData("schedule", "*/20 * * * *", "Every 20 min")]
    public async Task A_row_says_its_trigger_in_words(string type, string? cron, string words)
    {
        _ = await Create(1, "w", type, cron);

        var row = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single();

        Assert.Equal(words, row.Trigger);
    }

    [Fact]
    public async Task A_workflow_that_never_ran_has_no_last_run_and_an_empty_strip()
    {
        _ = await Create(1, "w", "manual", cron: null);

        var row = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single();

        Assert.Null(row.LastRun);
        Assert.Empty(row.RecentRuns);
        Assert.False(row.NeedsLook);
    }

    [Fact]
    public async Task The_last_run_is_the_newest_runs_status_and_start_time()
    {
        var id = await Create(1, "w", "manual", cron: null);
        _ = await Insert(id, "w", 1, WorkflowRunStatus.Succeeded, _now.AddMinutes(-30));
        _ = await Insert(id, "w", 2, WorkflowRunStatus.Failed, _now.AddMinutes(-3));

        var row = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single();

        Assert.Equal(new WorkflowLastRun("failed", _now.AddMinutes(-3)), row.LastRun);
        Assert.True(row.NeedsLook);
    }

    [Fact]
    public async Task The_strip_holds_the_newest_ten_runs_newest_first()
    {
        var id = await Create(1, "w", "manual", cron: null);
        for (var n = 1; n <= 12; n++)
        {
            _ = await Insert(id, "w", n, n == 12 ? WorkflowRunStatus.Failed : WorkflowRunStatus.Succeeded, _now.AddMinutes(-60 + n));
        }

        var row = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single();

        Assert.Equal(10, row.RecentRuns.Count);
        Assert.Equal("failed", row.RecentRuns[0]);
        Assert.All(row.RecentRuns.Skip(1), static s => Assert.Equal("succeeded", s));
    }

    [Fact]
    public async Task An_active_schedule_says_its_next_run_and_a_by_hand_workflow_has_none()
    {
        var sum = await Create(1, "sum", "schedule", "*/20 * * * *");
        var daily = await Create(2, "weekday-check", "schedule", "0 7 * * 1-5");
        var hand = await Create(3, "by-hand", "manual", cron: null);
        foreach (var id in new[] { sum, daily, hand })
        {
            _ = (await _workflows.Activate(id, 1, Token)).Value;
        }

        var rows = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.ToDictionary(static w => w.Slug.Value, StringComparer.Ordinal);

        Assert.Equal(new DateTimeOffset(2026, 11, 14, 11, 0, 0, TimeSpan.Zero), rows["sum"].NextRun);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 7, 0, 0, TimeSpan.Zero), rows["weekday-check"].NextRun);   // Sat -> Mon
        Assert.Null(rows["by-hand"].NextRun);
    }

    [Fact]
    public async Task A_paused_schedule_has_no_next_run_and_needs_a_look_but_an_off_one_does_not()
    {
        var nightly = await Create(1, "nightly", "schedule", "0 2 * * *");
        _ = await Create(2, "digest", "schedule", "0 8 * * *");   // never turned on: off, not paused
        _ = (await _workflows.Activate(nightly, 1, Token)).Value;
        _ = (await _workflows.Pause(nightly, Token)).Value;

        var rows = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.ToDictionary(static w => w.Slug.Value, StringComparer.Ordinal);

        Assert.Equal(("paused", true, null), (rows["nightly"].State, rows["nightly"].NeedsLook, rows["nightly"].NextRun));
        Assert.Equal(("off", false, null), (rows["digest"].State, rows["digest"].NeedsLook, rows["digest"].NextRun));
    }

    [Fact]
    public async Task A_paused_by_hand_workflow_does_not_need_a_look_because_it_has_no_schedule()
    {
        var id = await Create(1, "hand", "manual", cron: null);
        _ = (await _workflows.Activate(id, 1, Token)).Value;
        _ = (await _workflows.Pause(id, Token)).Value;

        var row = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single();

        Assert.False(row.NeedsLook);
    }

    [Fact]
    public async Task The_header_counts_split_total_into_active_paused_and_off()
    {
        var a = await Create(1, "a", "manual", cron: null);
        var b = await Create(2, "b", "manual", cron: null);
        var c = await Create(3, "c", "schedule", "0 2 * * *");
        _ = await Create(4, "d", "manual", cron: null);
        var gone = await Create(5, "gone", "manual", cron: null);
        _ = (await _workflows.Activate(a, 1, Token)).Value;
        _ = (await _workflows.Activate(b, 1, Token)).Value;
        _ = (await _workflows.Activate(c, 1, Token)).Value;
        _ = (await _workflows.Pause(c, Token)).Value;
        _ = (await _workflows.Archive(gone, Token)).Value;

        var counts = (await _service.Handle(new ListWorkflowsRequest(), Token)).Value.Counts;

        Assert.Equal(new WorkflowCounts(Total: 4, Active: 2, Paused: 1, Off: 1), counts);
    }

    private static GraphSpec Spec(string name, string type, string? cron) =>
        new(Version: 1, name, Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), type, Title: null, cron is null ? [] : new JsonObject { ["cron"] = cron, ["timezone"] = "UTC" })], Edges: []);

    private async Task<WorkflowId> Create(int number, string slug, string type, string? cron)
    {
        var id = WorkflowId.From("WF-" + number.ToString("D32", System.Globalization.CultureInfo.InvariantCulture));
        _ = (await _workflows.Create(id, WorkflowSlug.From(slug), Spec(slug, type, cron), "owner", Token)).Value;
        return id;
    }

    private async Task<WorkflowRun> Insert(WorkflowId id, string slug, int occurrence, WorkflowRunStatus status, DateTimeOffset startedAt)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(WorkflowSlug.From(slug), 1, occurrence), id, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, startedAt, EndedAt: startedAt.AddSeconds(5));
        return (await _runs.Insert(run, Token)).Value;
    }
}
