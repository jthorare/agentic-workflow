using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.WorkflowSchedule;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.WorkflowSchedule;

/// <summary>The 'workflow-schedule' service: one run for the latest tick of an active workflow's schedule node, never twice, never before the revision existed.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowScheduleServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly RecordingStarter _starter;
    private readonly WorkflowScheduleService _service;

    public WorkflowScheduleServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _starter = new RecordingStarter(_runs, _clock);
        _service = new WorkflowScheduleService(_workflows, _runs, _starter, _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Three_missed_days_fire_one_run_for_the_latest_tick()
    {
        // Wed 4 Nov 09:00 the workflow goes live; the box sleeps; Mon 9 Nov 08:00 it wakes. Missed: Thu 5, Fri 6, Mon 9 at 07:00.
        _clock.SetUtcNow(At("2026-11-04T09:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T08:00:00Z"));

        var ran = await Tick();

        Assert.True(ran.IsSuccess);
        var start = Assert.Single(_starter.Requests);
        Assert.Equal(_slug, start.Slug);
        Assert.Equal(1, start.Revision);
        Assert.Equal(Minutes("2026-11-09T07:00:00Z"), start.Occurrence);
        Assert.Equal(At("2026-11-09T07:00:00Z"), ScheduledFor(start));
    }

    [Fact]
    public async Task A_tick_already_fired_starts_nothing()
    {
        _clock.SetUtcNow(At("2026-11-09T06:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T07:00:30Z"));
        _ = await Tick();

        _clock.Advance(TimeSpan.FromMinutes(1));
        var again = await Tick();

        Assert.True(again.IsSuccess);
        _ = Assert.Single(_starter.Requests);
    }

    [Fact]
    public async Task A_tick_before_the_revision_was_activated_starts_nothing()
    {
        // saved and activated at 09:00 on Mon 9 Nov, after that day's 07:00 tick
        _clock.SetUtcNow(At("2026-11-09T09:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T09:30:00Z"));

        var first = await Tick();

        Assert.True(first.IsSuccess);
        Assert.Empty(_starter.Requests);

        // the next 07:00 is after activation, so it fires
        _clock.SetUtcNow(At("2026-11-10T07:00:30Z"));
        _ = await Tick();

        var start = Assert.Single(_starter.Requests);
        Assert.Equal(At("2026-11-10T07:00:00Z"), ScheduledFor(start));
    }

    [Fact]
    public async Task A_workflow_without_a_schedule_node_is_ignored()
    {
        _clock.SetUtcNow(At("2026-11-04T09:00:00Z"));
        await GoLive(Spec("manual", []));
        _clock.SetUtcNow(At("2026-11-09T08:00:00Z"));

        var ran = await Tick();

        Assert.True(ran.IsSuccess);
        Assert.Empty(_starter.Requests);
    }

    private async Task GoLive(GraphSpec spec)
    {
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
    }

    private ValueTask<Result<ScheduledResult>> Tick()
    {
        var holder = new LeaseHolder(HostId.From("test-host"), 1, _clock.GetUtcNow(), BootId.From("test-boot"));
        var grant = new LeaseGrant(holder, 1, _clock.GetUtcNow().AddMinutes(1));
        return _service.Run(new ScheduledRun(grant, _clock.GetUtcNow()), Token);
    }

    private static GraphSpec Spec(string type, JsonObject config) =>
        new(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), type, Title: null, config)], Edges: []);

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static int Minutes(string iso) => (int)(At(iso).ToUnixTimeSeconds() / 60);

    private static DateTimeOffset ScheduledFor(GraphStartRequest request) =>
        JsonNode.Parse(request.Trigger)!["scheduledFor"]!.GetValue<DateTimeOffset>();

    /// <summary>Records each request and, like the real starter, stores the run under its id so a tick that fired once is on record.</summary>
    private sealed class RecordingStarter(InMemoryWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            var inserted = await runs.Insert(new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null), cancellationToken);
            if (inserted.IsError)
            {
                return inserted.Error;
            }

            Requests.Add(request);
            return new GraphStart(GraphStartKind.Started, id);
        }
    }
}
