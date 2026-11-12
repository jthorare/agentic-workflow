using Aw.Meta.Core.Events;

namespace Aw.Meta.Tests.Events;

public sealed class MetaEventsTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void Every_kind_is_well_formed_and_carries_the_fields_the_docs_list()
    {
        var all = new[]
        {
            (MetaEvents.ObligationOpened(_at, "m1", "t1-api", "api"), "obligation.opened", "api", """{"id":"m1","from":"t1-api","lane":"api"}"""),
            (MetaEvents.ObligationClosed(_at, "m1", "t1-api", "api"), "obligation.closed", "api", """{"id":"m1","from":"t1-api","lane":"api"}"""),
            (MetaEvents.ContextHigh(_at, "api", 487_000), "context.high", "api", """{"lane":"api","tokens_k":487,"recycle":true}"""),
            (MetaEvents.ContextOk(_at, "api", 9_000), "context.ok", "api", """{"lane":"api","tokens_k":9,"recycle":false}"""),
            (MetaEvents.LaneDown(_at, "ui", "no live session"), "lane.down", "ui", """{"lane":"ui","reason":"no live session"}"""),
            (MetaEvents.LaneUp(_at, "ui", "live session"), "lane.up", "ui", """{"lane":"ui","reason":"live session"}"""),
            (MetaEvents.UsageLimited(_at, "api", "claude", _at.AddMinutes(30)), "usage.limited", "api", """{"lane":"api","provider":"claude","until":"2026-11-12T09:30:00Z"}"""),
            (MetaEvents.UsageOk(_at, "api", "claude", until: null), "usage.ok", "api", """{"lane":"api","provider":"claude"}"""),
            (MetaEvents.MergeRecorded(_at, 190, "abc1234", "api"), "merge.recorded", "api", """{"pr":190,"sha":"abc1234","lane":"api"}"""),
        };

        foreach (var (made, kind, lane, fields) in all)
        {
            Assert.Equal((kind, lane, fields, _at), (made.Kind, made.Lane, made.Fields, made.At));
            Assert.True(MetaEventRules.Check(made).IsSuccess);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merge_with_no_known_lane_has_no_event_lane()
    {
        var made = MetaEvents.MergeRecorded(_at, 190, "unknown", lane: string.Empty);

        Assert.Null(made.Lane);
        Assert.Equal("""{"pr":190,"sha":"unknown","lane":""}""", made.Fields);
    }
}
