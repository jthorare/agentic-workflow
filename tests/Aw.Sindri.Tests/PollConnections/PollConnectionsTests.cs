using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>The 'workflow-poll' service: one provider call per (connection, event kind), per-trigger filters applied locally, the cursor saved only after the runs started, and the backoff after a rate limit.</summary>
[Lane(TestLane.Unit)]
public sealed class PollConnectionsTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(PollSettings.DefaultIntervalSeconds);

    private readonly PollWorld _world = new();

    private static PollCursor Cursor(string? value) =>
        new(PollWorld.Name, PollWorld.Kind, value, UpdatedAt: null, LastPollAt: null, BackoffUntil: null, Failures: 0);

    private async Task Seeded(string? value)
    {
        await _world.Connect();
        _ = await _world.Cursors.Save(Cursor(value), PollWorld.Token);
        _ = _world.Wake.TryTake();   // a regular poll, not the catch-up of a start
    }

    [Fact]
    public async Task Three_triggers_on_one_connection_and_event_kind_make_one_provider_call()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        _ = await _world.GoLive("fix-only", filter: "trigger.title == \"Fix login\"");
        _ = await _world.GoLive("other-only", filter: "trigger.title == \"Other\"");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1", ("title", "Fix login")), PollWorld.Event("linear:h-2", ("title", "Docs"))));

        var ran = await _world.Tick(poller);

        Assert.True(ran.IsSuccess);
        Assert.Equal(1, poller.Calls);
        Assert.Equal(2, _world.Starter.Requests.Count(r => r.Slug.Value == "all-labels"));
        Assert.Equal(1, _world.Starter.Requests.Count(r => r.Slug.Value == "fix-only"));
        Assert.DoesNotContain(_world.Starter.Requests, static r => r.Slug.Value == "other-only");
        Assert.Equal("c1", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);
    }

    [Fact]
    public async Task The_cursor_advances_only_after_the_runs_started()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1")));
        _world.Starter.Unreachable = true;

        _ = await _world.Tick(poller);

        Assert.Equal("c0", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);   // the start failed: the cursor is where it was
        Assert.Empty(_world.Starter.Requests);

        _world.Starter.Unreachable = false;
        _world.Connections.Clock.Advance(_interval);
        _ = await _world.Tick(poller);

        Assert.Equal("c1", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);
        _ = Assert.Single(_world.Starter.Requests);
    }

    [Fact]
    public async Task A_failed_start_saves_the_backoff_so_the_next_tick_leaves_the_provider_alone()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1")));
        var clock = _world.Connections.Clock;
        _world.Starter.Unreachable = true;

        _ = await _world.Tick(poller);
        var saved = (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!;

        Assert.Equal("c0", saved.Value);
        Assert.Equal(1, saved.Failures);
        Assert.Equal(clock.GetUtcNow() + PollBackoff.BaseDelay, saved.BackoffUntil);

        clock.Advance(TimeSpan.FromSeconds(30));   // the service's next tick
        _ = await _world.Tick(poller);

        Assert.Equal(1, poller.Calls);
    }

    [Fact]
    public async Task A_delivery_whose_launch_failed_is_a_duplicate_on_the_next_poll_so_the_group_moves_on()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1"), PollWorld.Event("linear:h-2")));
        _world.Starter.FailNextLaunch = true;

        _ = await _world.Tick(poller);

        Assert.Equal("c0", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);

        _world.Connections.Clock.Advance(_interval);
        _ = await _world.Tick(poller);

        Assert.Equal("c1", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);
        _ = Assert.Single(_world.Starter.Requests);   // h-2; h-1 keeps its failed run and is skipped as a duplicate
        Assert.Contains(EventTypes.WorkflowTriggerSkipped, _world.Recorded(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_trigger_that_watches_a_group_again_starts_nothing_for_the_events_in_between()
    {
        await Seeded("c0");
        _ = await _world.GoLive("first");
        var poller = new ScriptedEventPoller("linear", (cursor, _) => cursor is null ? PollWorld.Page("c9") : PollWorld.Page("c9", PollWorld.Event("linear:h-7")));
        var clock = _world.Connections.Clock;

        _ = await _world.Tick(poller);
        await _world.Archive("first");
        clock.Advance(_interval);
        _ = await _world.Tick(poller);   // nothing watches the group: its cursor loses its value
        _ = await _world.GoLive("second");
        clock.Advance(_interval);
        _ = await _world.Tick(poller);

        Assert.DoesNotContain(_world.Starter.Requests, static r => r.Slug.Value == "second");
        Assert.Null(poller.Cursors[^1]);   // the second trigger's first poll is a baseline
        Assert.Equal("c9", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);
    }

    [Fact]
    public async Task A_catch_up_that_fails_is_asked_for_again()
    {
        await _world.Connect();   // the wake request starts pending: this first run is the catch-up of a start
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1")));
        _world.Starter.Unreachable = true;

        _ = await _world.Tick(poller);

        Assert.True(_world.Wake.TryTake());
    }

    [Fact]
    public async Task A_429_with_Retry_After_delays_the_next_poll_and_a_second_failure_doubles_it()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => new PollResult([], "c0", RateLimited: true, TimeSpan.FromSeconds(90), Reason: "the provider is rate limiting this connection"));
        var clock = _world.Connections.Clock;

        _ = await _world.Tick(poller);
        var first = (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!;

        Assert.Equal(clock.GetUtcNow().AddSeconds(90), first.BackoffUntil);
        Assert.Equal(1, first.Failures);
        Assert.Equal("c0", first.Value);

        clock.Advance(TimeSpan.FromSeconds(60));
        _ = await _world.Tick(poller);
        Assert.Equal(1, poller.Calls);   // inside the wait nothing is asked

        clock.Advance(_interval);
        _ = await _world.Tick(poller);
        var second = (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!;

        Assert.Equal(2, poller.Calls);
        Assert.Equal(2, second.Failures);
        Assert.Equal(clock.GetUtcNow().AddSeconds(180), second.BackoffUntil);   // 90 s asked again, doubled
        Assert.Empty(_world.Starter.Requests);
    }

    [Fact]
    public async Task A_trigger_skipped_for_a_run_in_flight_is_recorded_and_the_cursor_still_moves()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        _world.Starter.Skip = true;
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1")));

        _ = await _world.Tick(poller);

        Assert.Equal("c1", (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value!.Value);   // a skip is a handled start, not a failure
        Assert.Contains(EventTypes.WorkflowTriggerSkipped, _world.Recorded(), StringComparer.Ordinal);
        Assert.Contains(EventTypes.ConnectionPolled, _world.Recorded(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_poll_keeps_its_events_in_the_recent_store_even_when_no_trigger_takes_them()
    {
        await Seeded("c0");
        _ = await _world.GoLive("other-only", filter: "trigger.title == \"Other\"");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1", ("title", "Fix login")), PollWorld.Event("linear:h-2", ("title", "Docs"))));

        _ = await _world.Tick(poller);

        Assert.Empty(_world.Starter.Requests);
        Assert.Equal(["linear:h-1", "linear:h-2"], _world.Recent.Newest(PollWorld.Kind, PollWorld.Name, max: 10).Select(static r => r.Event.DeliveryKey).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_rate_limited_poll_keeps_nothing_in_the_recent_store()
    {
        await Seeded("c0");
        _ = await _world.GoLive("all-labels");
        var poller = new ScriptedEventPoller("linear", (_, _) => new PollResult([], "c0", RateLimited: true, TimeSpan.FromSeconds(90)));

        _ = await _world.Tick(poller);

        Assert.Empty(_world.Recent.Newest(PollWorld.Kind, connection: null, max: 10));
    }

    [Fact]
    public async Task A_workflow_that_watches_nothing_makes_no_provider_call()
    {
        await Seeded("c0");
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1"));

        var ran = await _world.Tick(poller);

        Assert.True(ran.Value.Idle);
        Assert.Equal(0, poller.Calls);
    }

    [Fact]
    public void A_delivery_id_always_gives_the_same_occurrence_inside_the_run_id_range()
    {
        var a = DeliveryOccurrence.Of("github:1001");

        Assert.Equal(a, DeliveryOccurrence.Of("github:1001"));
        Assert.NotEqual(a, DeliveryOccurrence.Of("github:1002"));
        Assert.InRange(a, 100_000_000, 999_999_998);
    }
}
