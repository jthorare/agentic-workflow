using Aw.Kernel.Testing;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Features.HomeLanes;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The three triggers of Home spec §3.2 as pure rules, and the id each row gets.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneTriggerTests
{
    private static LaneSnapshot Snapshot(string status, string context, string usage) => LaneParser.Parse(status, context, usage).Value;

    [Fact]
    public void A_lane_with_the_verdict_Recycle_is_a_lane_recycle_row_and_a_lane_under_the_mark_is_not()
    {
        var rows = LaneTriggers.Rows(Snapshot(LaneFixtures.StatusEmpty, LaneFixtures.ContextRecycle, LaneFixtures.UsageOk));

        var row = Assert.Single(rows);
        Assert.Equal((NeedsYouKind.LaneRecycle, "api"), (row.Kind, row.Key));
        Assert.Contains("212004", row.Reason, StringComparison.Ordinal);
        Assert.Contains("200000", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_meta_lane_is_a_lane_like_any_other()
    {
        var rows = LaneTriggers.Rows(Snapshot(LaneFixtures.StatusEmpty, LaneFixtures.ContextMetaRecycle, LaneFixtures.UsageOk));

        Assert.Equal([(NeedsYouKind.LaneRecycle, "meta")], rows.Select(static r => (r.Kind, r.Key)));
    }

    [Fact]
    public void A_limited_subject_is_an_agent_limited_row_naming_the_limit_and_the_reset_and_ok_or_unknown_subjects_are_not()
    {
        var rows = LaneTriggers.Rows(Snapshot(LaneFixtures.StatusEmpty, LaneFixtures.ContextOk, LaneFixtures.UsageLimited));

        var row = Assert.Single(rows);
        Assert.Equal((NeedsYouKind.AgentLimited, "engine"), (row.Kind, row.Key));
        Assert.Contains("Session", row.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("23:00", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_queued_pr_is_one_pr_ready_merge_row_named_by_its_number_with_the_lane_that_sent_it()
    {
        var rows = LaneTriggers.Rows(Snapshot(LaneFixtures.Status, LaneFixtures.ContextOk, LaneFixtures.UsageOk));

        Assert.Equal([(NeedsYouKind.PrReadyMerge, "2897"), (NeedsYouKind.PrReadyMerge, "2950"), (NeedsYouKind.PrReadyMerge, "3214")], rows.Select(static r => (r.Kind, r.Key)));
        Assert.Contains("sindri-v3-api", rows[2].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void When_nothing_holds_there_are_no_rows()
        => Assert.Empty(LaneTriggers.Rows(Snapshot(LaneFixtures.StatusEmpty, LaneFixtures.ContextOk, LaneFixtures.UsageOk)));

    [Theory]
    [InlineData(NeedsYouKind.LaneRecycle, "api", "lane-recycle:api")]
    [InlineData(NeedsYouKind.AgentLimited, "engine", "agent-limited:engine")]
    [InlineData(NeedsYouKind.PrReadyMerge, "3214", "pr-ready-merge:3214")]
    public void The_id_is_the_wire_name_of_the_kind_then_the_key(NeedsYouKind kind, string key, string expected)
        => Assert.Equal(expected, LaneTriggers.Id(kind, key).Value);

    [Fact]
    public void The_three_kinds_are_on_the_wire_by_their_spec_names()
        => Assert.Equal("\"lane-recycle\" \"agent-limited\" \"pr-ready-merge\"", string.Join(' ', new[] { NeedsYouKind.LaneRecycle, NeedsYouKind.AgentLimited, NeedsYouKind.PrReadyMerge }.Select(static k => System.Text.Json.JsonSerializer.Serialize(k))));
}
