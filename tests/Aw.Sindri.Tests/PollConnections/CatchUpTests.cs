using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>Catch-up (on control-plane start and after a wake): every cursor is read forward, each event starts one run, and a read that stops at its cap says so once.</summary>
[Lane(TestLane.Unit)]
public sealed class CatchUpTests
{
    private readonly PollWorld _world = new();

    private static PollGroup Group() =>
        new(PollWorld.Name, PollWorld.Kind, [new PollTrigger(WorkflowId.From("WF-00000000000000000000000000000001"), WorkflowSlug.From("all-labels"), 1, GraphConcurrency.OnePerWorkflow, Filter: null)]);

    private async Task<ReadOutcome> Read(ScriptedEventPoller poller, int pages, string? cursor)
    {
        if (_world.Connections.Held.Current is null)
        {
            await _world.Connect();
        }

        var run = await _world.Connections.Tick();
        var connection = (await _world.Connections.Store.Get(PollWorld.Name, PollWorld.Token)).Value;
        var saved = cursor is null ? null : new PollCursor(PollWorld.Name, PollWorld.Kind, cursor, null, null, null, 0);
        var read = await _world.CatchUp().Read(new ReadRequest(Group(), connection, poller, saved, pages, Forward: true), run.Grant, PollWorld.Token);
        return read.Value;
    }

    private async Task<string?> SavedCursor() => (await _world.Cursors.Get(PollWorld.Name, PollWorld.Kind, PollWorld.Token)).Value?.Value;

    [Fact]
    public async Task A_catch_up_that_overlaps_a_regular_poll_starts_each_event_once()
    {
        await _world.Connect();
        _ = await _world.GoLive("all-labels");
        _ = await _world.Cursors.Save(new PollCursor(PollWorld.Name, PollWorld.Kind, "c0", null, null, null, 0), PollWorld.Token);
        _ = _world.Wake.TryTake();
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1"), PollWorld.Event("linear:h-2")));
        _ = await _world.Tick(poller);   // the regular poll starts both runs

        _world.Wake.Request();   // a wake: catch-up reads the same events again before the interval is over
        var again = await _world.Tick(poller);

        Assert.True(again.IsSuccess);
        Assert.Equal(2, _world.Starter.Requests.Count);   // still two runs
        Assert.Equal(2, _world.Starter.Requests.Select(static r => r.Occurrence).Distinct().Count());
        Assert.Equal(2, _world.Recorded().Count(static t => t == EventTypes.WorkflowTriggerSkipped));   // the repeats were refused as duplicates, once each
    }

    [Fact]
    public async Task A_capped_catch_up_records_one_event_and_leaves_the_cursor_where_it_stopped()
    {
        var poller = new ScriptedEventPoller("linear", (_, call) => PollWorld.Page($"c{call}", PollWorld.Event($"linear:h-{call}")));

        var read = await Read(poller, pages: 3, cursor: "c0");

        Assert.True(read.Capped);
        Assert.Equal(3, poller.Calls);
        Assert.Equal(["c0", "c1", "c2"], poller.Cursors);   // each call goes on from the cursor the one before saved
        Assert.Equal("c3", await SavedCursor());
        Assert.Equal(3, _world.Starter.Requests.Count);
        Assert.Equal(1, _world.Recorded().Count(static t => t == EventTypes.CatchUpCapped));
    }

    [Fact]
    public async Task A_catch_up_that_reaches_the_end_before_the_cap_records_no_cap()
    {
        var poller = new ScriptedEventPoller("linear", (_, call) => call == 1 ? PollWorld.Page("c1", PollWorld.Event("linear:h-1")) : PollWorld.Page("c1"));

        var read = await Read(poller, pages: 20, cursor: "c0");

        Assert.False(read.Capped);
        Assert.Equal(2, poller.Calls);   // the second call found nothing new
        Assert.Equal("c1", await SavedCursor());
        Assert.DoesNotContain(EventTypes.CatchUpCapped, _world.Recorded());
    }

    [Fact]
    public async Task A_poller_that_reports_a_gap_gets_one_cap_event_and_the_cursor_moves_to_the_newest_event()
    {
        var poller = new ScriptedEventPoller("linear", (_, call) => call == 1
            ? new PollResult([PollWorld.Event("linear:h-9")], "c9", RateLimited: false, RetryAfter: null, Gap: true, Reason: "history order")
            : PollWorld.Page("c9"));

        var read = await Read(poller, pages: 20, cursor: "c0");

        Assert.True(read.Capped);
        Assert.Equal("c9", await SavedCursor());
        Assert.Equal(1, _world.Recorded().Count(static t => t == EventTypes.CatchUpCapped));
    }

    [Fact]
    public async Task Two_different_deliveries_on_the_same_occurrence_both_start_a_run_and_a_repeat_is_still_a_duplicate()
    {
        _world.Occurrence = static _ => 123_456_789;   // force a hash collision
        var poller = new ScriptedEventPoller("linear", (_, _) => PollWorld.Page("c1", PollWorld.Event("linear:h-1"), PollWorld.Event("linear:h-2")));

        _ = await Read(poller, pages: 1, cursor: "c0");

        Assert.Equal(2, _world.Starter.Requests.Count);
        Assert.Equal([123_456_789, 123_456_790], _world.Starter.Requests.Select(static r => r.Occurrence));
        Assert.DoesNotContain(EventTypes.WorkflowTriggerSkipped, _world.Recorded());

        // the same two events again: each finds its own run, so neither starts a second one
        _ = await Read(poller, pages: 1, cursor: "c0");

        Assert.Equal(2, _world.Starter.Requests.Count);
        Assert.Equal(2, _world.Recorded().Count(static t => t == EventTypes.WorkflowTriggerSkipped));
    }
}
