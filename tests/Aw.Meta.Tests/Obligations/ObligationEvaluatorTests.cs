using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

public sealed class ObligationEvaluatorTests
{
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
