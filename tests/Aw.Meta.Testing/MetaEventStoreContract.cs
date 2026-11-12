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
        Expect.That(all.Value.Select(e => e.Id).SequenceEqual([first.Value, second.Value, third.Value]), "a read from 0 returns everything in id order");
        Expect.That(after.Value.Select(e => e.Id).SequenceEqual([second.Value, third.Value]), "a read returns only ids above the cursor");
        Expect.That(none.Value.Count == 0, "a read at the last id returns nothing");
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

        Expect.That(read.Value[0] == new MetaEvent(id.Value, A.At(5), "usage.limited", "api", "{\"lane\":\"api\",\"provider\":\"claude\"}"), "the event round-trips");
        Expect.That(read.Value[1].Id == teamWide.Value && read.Value[1].Lane is null, "a team-wide event has no lane");
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

        Expect.That(byKind.Value.Count == 3 && byKind.Value.All(e => e.Kind == "obligation.opened"), "the kind filter keeps only that kind");
        Expect.That(byLane.Value.Count == 3 && byLane.Value.All(e => e.Lane == "api"), "the lane filter keeps only that lane");
        Expect.That(both.Value.Count == 2, "both filters apply together");
        Expect.That(capped.Value.Count == 2 && capped.Value[0].At == A.At(1), "max keeps the oldest events after the cursor");
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
        Expect.That(read.Value.Count == 0, "nothing was stored");
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
        Expect.That(left.Value.Select(e => e.At).SequenceEqual([A.At(10), A.At(20)]), "the event at the cutoff stays");
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

        Expect.That(read.Value.Select(e => e.Id).SequenceEqual([edge.Value, later.Value]), "the event more than the retention older than the newest append is gone");
        Expect.That(old.Value < edge.Value && edge.Value < later.Value, "ids are never reused");
    }
}
