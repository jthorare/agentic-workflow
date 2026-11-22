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
    public async Task Schedule_fire_gives_local_date_and_weekday_in_its_zone()
    {
        // Tokyo is UTC+9: Fri 13 Nov 07:00 there is Thu 12 Nov 22:00 UTC, so the UTC instant names the wrong day.
        _clock.SetUtcNow(At("2026-11-12T12:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "Asia/Tokyo" }));
        _clock.SetUtcNow(At("2026-11-12T22:00:30Z"));

        var ran = await Tick();

        Assert.True(ran.IsSuccess);
        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!;
        Assert.Equal("2026-11-13", trigger["localDate"]?.GetValue<string>());
        Assert.Equal("Friday", trigger["weekday"]?.GetValue<string>());
        var scheduledFor = trigger["scheduledFor"]!.GetValue<string>();
        Assert.EndsWith("Z", scheduledFor, StringComparison.Ordinal);
        Assert.Equal(At("2026-11-12T22:00:00Z"), DateTimeOffset.Parse(scheduledFor, CultureInfo.InvariantCulture));
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

    [Fact]
    public async Task A_skipped_tick_is_not_counted_and_the_next_tick_fires_its_own_latest_tick()
    {
        _clock.SetUtcNow(At("2026-11-09T06:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T07:00:30Z"));
        _starter.Skip = true;   // one-per-workflow with a run in flight

        var skipped = await Tick();

        Assert.True(skipped.Value.Idle);
        Assert.Empty(_starter.Requests);

        // the run in flight has ended; Tuesday's tick fires, Monday's skipped one is not retried
        _starter.Skip = false;
        _clock.SetUtcNow(At("2026-11-10T07:00:30Z"));
        _ = await Tick();

        var start = Assert.Single(_starter.Requests);
        Assert.Equal(At("2026-11-10T07:00:00Z"), ScheduledFor(start));
    }

    [Fact]
    public async Task A_run_in_flight_over_a_tick_that_ends_before_the_next_tick_fires_nothing_until_the_next_tick()
    {
        // daily 07:00; the Monday tick is skipped because a run is in flight; the run ends at 07:05, well before Tuesday
        _clock.SetUtcNow(At("2026-11-09T06:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T07:00:30Z"));
        _starter.Skip = true;
        _ = await Tick();

        _starter.Skip = false;
        _clock.SetUtcNow(At("2026-11-09T07:05:00Z"));
        var later = await Tick();
        _clock.SetUtcNow(At("2026-11-09T07:30:00Z"));
        _ = await Tick();

        Assert.True(later.IsSuccess);
        Assert.Empty(_starter.Requests);   // the skipped tick is done, not late-fired
        _clock.SetUtcNow(At("2026-11-10T07:00:30Z"));
        _ = await Tick();
        Assert.Equal(At("2026-11-10T07:00:00Z"), ScheduledFor(Assert.Single(_starter.Requests)));
    }

    [Fact]
    public async Task A_failed_launch_does_not_use_up_its_tick_so_the_next_pass_tries_again()
    {
        _clock.SetUtcNow(At("2026-11-09T06:00:00Z"));
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T07:00:30Z"));
        _starter.FailSlug = _slug;

        var failed = await Tick();
        _starter.FailSlug = null;
        _clock.Advance(TimeSpan.FromMinutes(1));
        var retried = await Tick();

        Assert.True(failed.IsError);
        Assert.True(retried.IsSuccess);
        Assert.Equal(At("2026-11-09T07:00:00Z"), ScheduledFor(Assert.Single(_starter.Requests)));
    }

    [Fact]
    public async Task One_workflows_error_does_not_stop_the_others_and_the_first_error_is_returned()
    {
        _clock.SetUtcNow(At("2026-11-09T06:00:00Z"));
        var broken = WorkflowSlug.From("a-broken");
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }), WorkflowId.From("WF-00000000000000000000000000000002"), broken);
        await GoLive(Spec("schedule", new JsonObject { ["cron"] = "0 7 * * 1-5" }));
        _clock.SetUtcNow(At("2026-11-09T07:00:30Z"));
        _starter.FailSlug = broken;

        var ran = await Tick();

        Assert.True(ran.IsError);
        Assert.Equal(_slug, Assert.Single(_starter.Requests).Slug);   // 'morning-check' still started
    }

    private async Task GoLive(GraphSpec spec, WorkflowId? id = null, WorkflowSlug? slug = null)
    {
        var workflow = id ?? _id;
        _ = (await _workflows.Create(workflow, slug ?? _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(workflow, 1, Token)).Value;
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

        public bool Skip { get; set; }

        public WorkflowSlug? FailSlug { get; set; }

        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            if (FailSlug is { } failing && failing == request.Slug)
            {
                return Core.SindriErrors.EngineUnavailable.With("the engine is down");
            }

            if (Skip)
            {
                return new GraphStart(GraphStartKind.Skipped, id);
            }

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
