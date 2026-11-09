using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

public sealed class ObligationEvaluatorTests
{
    private static LaneMapState Lanes(int version) =>
        new(version, [A.Assigned("meta", "t1-meta"), A.Assigned("api", "t1-api"), A.Assigned("engine", "t1-engine")]);

    [Fact, Trait("Lane", "unit")]
    public void Nothing_recorded_owes_nothing() =>
        Assert.Empty(ObligationEvaluator.Evaluate(A.Facts()));

    [Fact, Trait("Lane", "unit")]
    public void An_unanswered_message_opens_a_reply_with_the_next_command()
    {
        var facts = A.Facts() with { Messages = [A.Inbox("m1", name: "t1-api", body: "ready #190 079c2541")] };

        var open = Assert.Single(ObligationEvaluator.Evaluate(facts), o => o.Kind == ObligationKind.Reply);

        Assert.Equal((ObligationKind.Reply, "m1"), (open.Kind, open.Key));
        Assert.Contains("inbox-dismiss m1", open.NextCommand, StringComparison.Ordinal);
        Assert.Contains("t1-api", open.Summary, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("t1-api", 5, false)]
    [InlineData("T1-API", 5, false)]
    [InlineData("uds:/run/t1-api.sock", 5, false)]
    [InlineData("t1-api", -5, true)]
    [InlineData("t1-engine", 5, true)]
    public void A_later_send_to_the_same_peer_closes_a_reply(string to, int minute, bool stillOpen)
    {
        var facts = A.Facts() with
        {
            Messages = [A.Inbox("m1", name: "t1-api", minute: 0)],
            Outbound = [A.Sent(to, "ack", minute)],
        };

        Assert.Equal(stillOpen, ObligationEvaluator.Evaluate(facts).Any(o => o.Kind == ObligationKind.Reply));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dismissed_message_is_closed_and_a_deferred_one_reopens_when_due()
    {
        var dismissed = A.Inbox("m1") with { Resolution = new MessageResolution(MessageResolutionKind.Dismissed, "fyi") };
        var deferred = A.Inbox("m2") with { Resolution = new MessageResolution(MessageResolutionKind.Deferred, "gate running", A.At(90)) };
        var due = A.Inbox("m3") with { Resolution = new MessageResolution(MessageResolutionKind.Deferred, "gate running", A.At(30)) };

        var open = ObligationEvaluator.Evaluate(A.Facts() with { Messages = [dismissed, deferred, due] });

        Assert.Equal(["m3", "watch"], open.Select(o => o.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Replies_are_listed_in_arrival_order()
    {
        var facts = A.Facts() with { Messages = [A.Inbox("late", name: "x", minute: 9), A.Inbox("early", name: "y", minute: 2)] };

        Assert.Equal(["early", "late", "watch"], ObligationEvaluator.Evaluate(facts).Select(o => o.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lane_map_must_reach_every_lane_but_the_meta()
    {
        var facts = A.Facts() with { Lanes = Lanes(2), Outbound = [A.Sent("t1-api", "LANE MAP v2: api=t1-api", 3)] };

        var open = Assert.Single(ObligationEvaluator.Evaluate(facts));

        Assert.Equal((ObligationKind.BroadcastLanes, "v2"), (open.Kind, open.Key));
        Assert.Contains("engine", open.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("api", open.Summary.Replace("LANE MAP", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Broadcast_and_announce_name_each_lane_by_its_address()
    {
        var facts = A.Facts() with { Lanes = Lanes(1), LatestMerge = new MergeRecord(190, "abcdef1234567890", A.At(10)) };

        var open = ObligationEvaluator.Evaluate(facts);

        Assert.All(open, o => Assert.EndsWith("to: t1-api, t1-engine", o.NextCommand, StringComparison.Ordinal));
        Assert.All(open, o => Assert.Contains("api (t1-api), engine (t1-engine)", o.Summary, StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("LANE MAP v2: x", true)]
    [InlineData("LANE MAP v1: x", false)]
    public void The_lane_map_is_closed_once_every_lane_has_this_version(string text, bool closed)
    {
        var facts = A.Facts() with
        {
            Lanes = Lanes(2),
            Outbound = [A.Sent("t1-api", text, 3), A.Sent("t1-engine", text, 3)],
        };

        Assert.Equal(closed, ObligationEvaluator.Evaluate(facts).All(o => o.Kind != ObligationKind.BroadcastLanes));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merge_must_be_announced_to_every_lane_after_it_happened()
    {
        var merge = new MergeRecord(190, "abcdef1234567890", A.At(10));
        var facts = A.Facts() with
        {
            Lanes = Lanes(0),
            LatestMerge = merge,
            Outbound = [A.Sent("t1-api", "main moved to abcdef1: #190", 11), A.Sent("t1-engine", "main moved to abcdef1", 2)],
        };

        var open = Assert.Single(ObligationEvaluator.Evaluate(facts));

        Assert.Equal((ObligationKind.AnnounceMain, "abcdef1"), (open.Kind, open.Key));
        Assert.Contains("engine", open.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_announcement_giving_the_full_sha_closes_it()
    {
        var merge = new MergeRecord(190, "abcdef1234567890", A.At(10));
        var facts = A.Facts() with
        {
            Lanes = Lanes(0),
            LatestMerge = merge,
            Outbound = [A.Sent("t1-api", "main is now abcdef1234567890", 11), A.Sent("t1-engine", "main moved to abcdef1: #190 merged.", 11)],
        };

        Assert.Empty(ObligationEvaluator.Evaluate(facts));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_announcement_of_an_unknown_sha_merge_names_the_pull_request_as_a_whole_number()
    {
        var merge = new MergeRecord(22, MergeRecord.Unknown, A.At(10));
        var facts = A.Facts() with
        {
            Lanes = Lanes(0),
            LatestMerge = merge,
            Outbound = [A.Sent("t1-api", "main moved (#221 merged).", 11), A.Sent("t1-engine", "main moved (#22 merged).", 11)],
        };

        var open = Assert.Single(ObligationEvaluator.Evaluate(facts));

        Assert.Contains("api", open.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("engine", open.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_owner_answer_must_go_back_to_the_asking_lane()
    {
        var answer = new OwnerAnswer(3, "t1-api", "use postgres", A.At(20));
        var open = ObligationEvaluator.Evaluate(A.Facts() with { Answers = [answer] });
        var relayed = ObligationEvaluator.Evaluate(A.Facts() with
        {
            Answers = [answer],
            Outbound = [A.Sent("t1-api", "OWNER ANSWER q3: use postgres", 21)],
        });
        var wrong = ObligationEvaluator.Evaluate(A.Facts() with
        {
            Answers = [answer],
            Outbound = [A.Sent("t1-api", "something else", 21)],
        });

        Assert.Equal((ObligationKind.RelayOwner, "q3"), (open[0].Kind, open[0].Key));
        Assert.Empty(relayed);
        _ = Assert.Single(wrong);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, false)]
    [InlineData(58, false)]
    [InlineData(20, true)]
    public void A_stale_watch_must_be_rearmed(int? heartbeatMinute, bool open)
    {
        var facts = A.Facts() with { Heartbeat = heartbeatMinute is { } m ? A.At(m) : null, WatchInterval = TimeSpan.FromMinutes(2) };

        Assert.Equal(open, ObligationEvaluator.Evaluate(facts).Any(o => o.Kind == ObligationKind.RearmWatch));
    }
}
