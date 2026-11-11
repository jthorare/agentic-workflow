using Aw.Meta.Core.Obligations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Obligations;

public sealed class NoticeObligationTests
{
    private static List<Obligation> Open(string body) =>
        [.. ObligationEvaluator.Evaluate(A.Facts() with { Messages = [A.Inbox("m1", name: "t1-api", body: body)] })];

    [Theory, Trait("Lane", "unit")]
    [InlineData("ack, thanks")]
    [InlineData("thanks, on it")]
    [InlineData("noted")]
    [InlineData("ui -> meta: ack lane map v163")]
    [InlineData("LANE MAP v12: meta=a, api=b")]
    [InlineData("main moved to abc1234: #12 merged.")]
    [InlineData("FYI: gate running on #12")]
    public void A_notice_opens_no_reply(string body) =>
        Assert.DoesNotContain(Open(body), o => o.Kind == ObligationKind.Reply);

    [Theory, Trait("Lane", "unit")]
    [InlineData("ready #12 abc1234")]
    [InlineData("OK #12 at abc1234def")]
    [InlineData("NOT OK #12: tests red")]
    [InlineData("DIGEST READY /path/x.md")]
    [InlineData("ok, merging #12")]
    [InlineData("ack, can you rerun the gate?")]
    [InlineData("OWNER QUESTION: pick a or b")]
    [InlineData("please rebase #12")]
    public void A_message_that_asks_something_still_opens_one_reply(string body)
    {
        var reply = Assert.Single(Open(body), o => o.Kind == ObligationKind.Reply);

        Assert.Equal("m1", reply.Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ack_over_240_bytes_still_opens_a_reply()
    {
        var body = "ack, " + new string('x', 260);

        _ = Assert.Single(Open(body), o => o.Kind == ObligationKind.Reply);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rearm_does_not_count_a_notice_as_a_waiting_message() =>
        Assert.DoesNotContain(Open("ack, thanks"), o => o.Kind == ObligationKind.RearmWatch);
}
