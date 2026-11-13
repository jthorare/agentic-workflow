using Aw.Meta.Core.Events;
using Aw.Meta.Features.ReadEvents;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.ReadEvents;

public sealed class ReadEventsTests
{
    private readonly MetaWorld _world = new();

    private static ReadEventsRequest Req(long after, string? kind, string? lane, int max) => new(After: after, Kind: kind, Lane: lane, Max: max);

    private ReadEventsService Service() => new(_world.Events);

    private async Task Seed()
    {
        _ = await _world.Events.Append(new MetaEvent(0, A.At(1), "obligation.opened", "api", "{\"id\":\"m1\",\"from\":\"api-1\",\"lane\":\"api\"}"), CancellationToken.None);
        _ = await _world.Events.Append(new MetaEvent(0, A.At(2), "lane.down", "ui", "{\"lane\":\"ui\",\"reason\":\"idle\"}"), CancellationToken.None);
        _ = await _world.Events.Append(new MetaEvent(0, A.At(3), "box.health", Lane: null, "{\"mem_available_mb\":9000,\"disk_free_mb\":50000}"), CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_next_cursor_is_the_last_id_returned_and_passing_it_back_reads_on()
    {
        await Seed();

        var first = await Service().Handle(Req(after: 0, kind: null, lane: null, max: 2), CancellationToken.None);
        var second = await Service().Handle(Req(after: first.Value.Next, kind: null, lane: null, max: 2), CancellationToken.None);

        Assert.Equal(["obligation.opened", "lane.down"], first.Value.Events.Select(e => e.Kind), StringComparer.Ordinal);
        Assert.Equal(first.Value.Events[1].Id, first.Value.Next);
        Assert.Equal(["box.health"], second.Value.Events.Select(e => e.Kind), StringComparer.Ordinal);
        Assert.Equal(second.Value.Events[0].Id, second.Value.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_nothing_new_the_next_cursor_is_the_one_given()
    {
        await Seed();

        var response = await Service().Handle(Req(after: 41, kind: null, lane: null, max: 10), CancellationToken.None);

        Assert.Empty(response.Value.Events);
        Assert.Equal(41, response.Value.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_kind_and_lane_filters_apply_and_fields_come_back_as_an_object()
    {
        await Seed();

        var byKind = await Service().Handle(Req(after: 0, kind: "lane.down", lane: null, max: 10), CancellationToken.None);
        var byLane = await Service().Handle(Req(after: 0, kind: null, lane: "api", max: 10), CancellationToken.None);

        var down = Assert.Single(byKind.Value.Events);
        Assert.Equal("idle", down.Fields.GetProperty("reason").GetString());
        Assert.Equal("ui", down.Lane);
        Assert.Equal(["obligation.opened"], byLane.Value.Events.Select(e => e.Kind), StringComparer.Ordinal);
        Assert.Equal(byLane.Value.Events[0].Id, byLane.Value.Next);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(9, false)]
    public async Task Gap_is_true_only_when_the_cursor_is_below_the_oldest_kept_id_minus_one(long after, bool gap)
    {
        await Seed();
        _ = await _world.Events.Prune(A.At(2), CancellationToken.None);   // drops event 1; oldest is 2, latest 3

        var response = await Service().Handle(Req(after: after, kind: null, lane: null, max: 10), CancellationToken.None);

        Assert.Equal(gap, response.Value.Gap);
        Assert.Equal(2, response.Value.Oldest);
        Assert.Equal(3, response.Value.Latest);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_page_still_carries_oldest_latest_and_gap()
    {
        var empty = await Service().Handle(Req(after: 0, kind: null, lane: null, max: 10), CancellationToken.None);
        await Seed();
        _ = await _world.Events.Prune(A.At(100), CancellationToken.None);   // drops all three
        var emptied = await Service().Handle(Req(after: 1, kind: null, lane: null, max: 10), CancellationToken.None);

        Assert.Equal((1L, 0L, false), (empty.Value.Oldest, empty.Value.Latest, empty.Value.Gap));
        Assert.Equal((4L, 3L, true), (emptied.Value.Oldest, emptied.Value.Latest, emptied.Value.Gap));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Lane.Down", 0, 10, "AW-META-027")]
    [InlineData("down", 0, 10, "AW-META-027")]
    [InlineData(null, -1, 10, "AW-META-001")]
    [InlineData(null, 0, 0, "AW-META-001")]
    public void A_malformed_kind_a_negative_cursor_or_a_zero_max_is_refused(string? kind, long after, int max, string code) =>
        Assert.Equal(code, new ReadEventsValidator().Validate(Req(after: after, kind: kind, lane: null, max: max)).Error.Code, StringComparer.Ordinal);
}
