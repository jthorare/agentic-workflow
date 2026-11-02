using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

// #1899: when a bare name matches two sessions, SendMessage needs "name [ref]"; that send still reaches the named peer.
public sealed class RecipientRefTests
{
    private static LaneMapState Lanes(int version) =>
        new(version, [A.Assigned("meta", "t1-meta"), A.Assigned("api", "t1-api"), A.Assigned("engine", "t1-engine")]);

    [Theory, Trait("Lane", "unit")]
    [InlineData("t1-api [286358]", false)]
    [InlineData("T1-API [fb1e0c]", false)]
    [InlineData("t1-api[286358]", true)]
    [InlineData("t1-api [", true)]
    [InlineData("t1-engine [286358]", true)]
    public void A_send_to_the_name_with_a_ref_closes_a_reply(string to, bool stillOpen)
    {
        var facts = A.Facts() with
        {
            Messages = [A.Inbox("m1", name: "t1-api", minute: 0)],
            Outbound = [A.Sent(to, "ack", 5)],
        };

        Assert.Equal(stillOpen, ObligationEvaluator.Evaluate(facts).Any(o => o.Kind == ObligationKind.Reply));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_map_and_a_main_moved_sent_with_refs_reach_every_lane()
    {
        var facts = A.Facts() with
        {
            Lanes = Lanes(2),
            LatestMerge = new MergeRecord(190, "abcdef1234567890", A.At(10)),
            Outbound =
            [
                A.Sent("t1-api [286358]", "LANE MAP v2: api=t1-api", 11),
                A.Sent("t1-engine [fb1e0c]", "LANE MAP v2: api=t1-api", 11),
                A.Sent("t1-api [286358]", "main moved to abcdef1: #190", 11),
                A.Sent("t1-engine [fb1e0c]", "main moved to abcdef1: #190", 11),
            ],
        };

        Assert.Empty(ObligationEvaluator.Evaluate(facts));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_owner_answer_relayed_with_a_ref_closes_it()
    {
        var answer = new OwnerAnswer(3, "t1-api", "use postgres", A.At(20));

        var open = ObligationEvaluator.Evaluate(A.Facts() with
        {
            Answers = [answer],
            Outbound = [A.Sent("t1-api [286358]", "OWNER ANSWER q3: use postgres", 21)],
        });

        Assert.Empty(open);
    }
}
