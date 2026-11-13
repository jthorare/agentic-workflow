using Aw.Meta.Core;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IMetaEventStore contract: the same tests run against the in-memory fake and the SQLite adapter.</summary>
public abstract class MetaEventStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty event store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IMetaEventStore> Create();

    private static MetaEvent Event(int minute, string kind = "obligation.opened", string? lane = "api", string fields = "{\"id\":\"m1\"}") =>
        new(0, A.At(minute), kind, lane, fields);

    /// <summary>Ids rise, and a read returns only the events after the cursor, oldest first.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Ids_rise_and_a_read_returns_the_events_after_the_cursor_oldest_first()
    {
        var store = await Create();

        var first = await store.Append(Event(1), _none);
        var second = await store.Append(Event(2, "obligation.closed"), _none);
        var third = await store.Append(Event(3, "lane.down", "ui", "{\"reason\":\"idle\"}"), _none);
        var all = await store.Read(after: 0, kind: null, lane: null, max: 100, cancellationToken: _none);
        var after = await store.Read(after: first.Value, kind: null, lane: null, max: 100, cancellationToken: _none);
        var none = await store.Read(after: third.Value, kind: null, lane: null, max: 100, cancellationToken: _none);

        Expect.That(first.Value < second.Value && second.Value < third.Value, "ids rise");
        Expect.That(all.Value.Events.Select(e => e.Id).SequenceEqual([first.Value, second.Value, third.Value]), "a read from 0 returns everything in id order");
        Expect.That(after.Value.Events.Select(e => e.Id).SequenceEqual([second.Value, third.Value]), "a read returns only ids above the cursor");
        Expect.That(none.Value.Events.Count == 0, "a read at the last id returns nothing");
    }

    /// <summary>An event reads back as it was appended, with its assigned id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_event_reads_back_with_its_time_kind_lane_and_fields()
    {
        var store = await Create();

        var id = await store.Append(Event(5, "usage.limited", "api", "{\"lane\":\"api\",\"provider\":\"claude\"}"), _none);
        var teamWide = await store.Append(Event(6, "box.health", lane: null, "{\"mem_available_mb\":9000}"), _none);
        var read = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(read.Value.Events[0] == new MetaEvent(id.Value, A.At(5), "usage.limited", "api", "{\"lane\":\"api\",\"provider\":\"claude\"}"), "the event round-trips");
        Expect.That(read.Value.Events[1].Id == teamWide.Value && read.Value.Events[1].Lane is null, "a team-wide event has no lane");
    }

    /// <summary>The kind and lane filters narrow a read, and max caps it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_kind_and_lane_filters_narrow_a_read_and_max_caps_it()
    {
        var store = await Create();
        _ = await store.Append(Event(1, "obligation.opened", "api"), _none);
        _ = await store.Append(Event(2, "obligation.opened", "ui"), _none);
        _ = await store.Append(Event(3, "obligation.closed", "api"), _none);
        _ = await store.Append(Event(4, "obligation.opened", "api"), _none);

        var byKind = await store.Read(after: 0, kind: "obligation.opened", lane: null, max: 100, cancellationToken: _none);
        var byLane = await store.Read(after: 0, kind: null, lane: "api", max: 100, cancellationToken: _none);
        var both = await store.Read(after: 0, kind: "obligation.opened", lane: "api", max: 100, cancellationToken: _none);
        var capped = await store.Read(after: 0, kind: null, lane: null, max: 2, cancellationToken: _none);

        Expect.That(byKind.Value.Events.Count == 3 && byKind.Value.Events.All(e => e.Kind == "obligation.opened"), "the kind filter keeps only that kind");
        Expect.That(byLane.Value.Events.Count == 3 && byLane.Value.Events.All(e => e.Lane == "api"), "the lane filter keeps only that lane");
        Expect.That(both.Value.Events.Count == 2, "both filters apply together");
        Expect.That(capped.Value.Events.Count == 2 && capped.Value.Events[0].At == A.At(1), "max keeps the oldest events after the cursor");
    }

    /// <summary>An empty store reads oldest 1 and latest 0.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_store_reads_oldest_1_and_latest_0()
    {
        var store = await Create();

        var page = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(page.Value.Events.Count == 0 && page.Value.Oldest == 1 && page.Value.Latest == 0, "an empty store has oldest 1 and latest 0");
    }

    /// <summary>After appends the bounds are the first and the last id, whatever the filters keep.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task After_appends_the_bounds_are_the_first_and_last_id_whatever_the_filters_keep()
    {
        var store = await Create();
        var first = await store.Append(Event(1, "obligation.opened", "api"), _none);
        _ = await store.Append(Event(2, "obligation.closed", "ui"), _none);
        var third = await store.Append(Event(3, "obligation.opened", "api"), _none);

        var all = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);
        var filtered = await store.Read(after: first.Value, kind: "obligation.closed", lane: null, max: 1, cancellationToken: _none);

        Expect.That(all.Value.Oldest == first.Value && all.Value.Latest == third.Value, "the bounds are the first and last id");
        Expect.That(filtered.Value.Oldest == first.Value && filtered.Value.Latest == third.Value, "a filtered, capped read reports the same bounds");
    }

    /// <summary>After a prune that drops some events, oldest moves up and latest stays.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task After_a_prune_that_drops_some_events_oldest_moves_up_and_latest_stays()
    {
        var store = await Create();
        _ = await store.Append(Event(1), _none);
        var second = await store.Append(Event(10), _none);
        var third = await store.Append(Event(20), _none);

        _ = await store.Prune(A.At(10), _none);
        var page = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(page.Value.Oldest == second.Value && page.Value.Latest == third.Value, "oldest is the first kept id and latest is unchanged");
    }

    /// <summary>After a prune that drops every event, latest stays and oldest is latest + 1.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task After_a_prune_that_drops_every_event_latest_stays_and_oldest_is_latest_plus_1()
    {
        var store = await Create();
        _ = await store.Append(Event(1), _none);
        var last = await store.Append(Event(2), _none);

        _ = await store.Prune(A.At(100), _none);
        var page = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);
        var next = await store.Append(Event(101), _none);

        Expect.That(page.Value.Events.Count == 0 && page.Value.Latest == last.Value && page.Value.Oldest == last.Value + 1, "an emptied log keeps latest and puts oldest just above it");
        Expect.That(next.Value == last.Value + 1, "the next id continues after the pruned ones");
    }

    /// <summary>A kind that is not dotted lower-case, or fields that are not a JSON object, are refused and store nothing.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("", "{}")]
    [InlineData("Obligation.opened", "{}")]
    [InlineData("opened", "{}")]
    [InlineData("a..b", "{}")]
    [InlineData("a.b ", "{}")]
    [InlineData("a.b", "[1]")]
    [InlineData("a.b", "not json")]
    [InlineData("a.b", "")]
    public async Task A_malformed_kind_or_fields_are_refused_and_store_nothing(string kind, string fields)
    {
        var store = await Create();

        var appended = await store.Append(Event(1, kind, "api", fields), _none);
        var read = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(appended.IsError && appended.Error.Code == MetaErrors.EventInvalid.Code, "the append is refused with AW-META-027");
        Expect.That(read.Value.Events.Count == 0, "nothing was stored");
    }

    /// <summary>Prune drops the events older than the cutoff and reports how many.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Prune_drops_the_events_older_than_the_cutoff()
    {
        var store = await Create();
        _ = await store.Append(Event(1), _none);
        _ = await store.Append(Event(10), _none);
        _ = await store.Append(Event(20), _none);

        var pruned = await store.Prune(A.At(10), _none);
        var left = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(pruned.Value == 1, "one event is older than the cutoff");
        Expect.That(left.Value.Events.Select(e => e.At).SequenceEqual([A.At(10), A.At(20)]), "the event at the cutoff stays");
    }

    /// <summary>An append drops the events more than the retention before its own time, and the ids it hands out keep rising.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_append_drops_events_older_than_the_retention_and_ids_keep_rising()
    {
        var store = await Create();
        var old = await store.Append(Event(1), _none);
        var edge = await store.Append(new MetaEvent(0, A.At(1) + MetaEvent.Retention, "lane.up", "api", "{}"), _none);
        var later = await store.Append(new MetaEvent(0, A.At(2) + MetaEvent.Retention, "lane.up", "api", "{}"), _none);
        var read = await store.Read(after: 0, kind: null, lane: null, max: 10, cancellationToken: _none);

        Expect.That(read.Value.Events.Select(e => e.Id).SequenceEqual([edge.Value, later.Value]), "the event more than the retention older than the newest append is gone");
        Expect.That(old.Value < edge.Value && edge.Value < later.Value, "ids are never reused");
    }
}
