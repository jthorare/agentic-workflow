using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

// #2651: the sender declares the kind with a first-line STATUS: or ACTION: tag; protected lines stay action either way.
public sealed class MessageTagTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("STATUS: review clean on #123")]
    [InlineData("status: gate started on #12")]
    [InlineData("ideas -> meta: STATUS: counts so far 120 passed, 0 skipped, 0 failed")]
    [InlineData("STATUS: gate done on #12, want me to rerun it?")]
    [InlineData("STATUS: please ignore the earlier line")]
    public void A_status_tagged_line_is_a_notice_even_when_it_reads_like_an_ask(string body) => Assert.True(NoticeRule.IsNotice(body));

    [Theory, Trait("Lane", "unit")]
    [InlineData("ACTION: rebase #12 on main")]
    [InlineData("ACTION: ack")]
    [InlineData("STATUS: ready #123 079c2541")]
    [InlineData("STATUS: OK #12 at abc1234def")]
    [InlineData("STATUS: NOT OK #12: tests red")]
    [InlineData("STATUS: HOLD #12 until the owner answers")]
    [InlineData("HOLD #12 until the owner answers")]
    [InlineData("STATUS: DIGEST READY /path/x.md")]
    [InlineData("STATUS: OWNER QUESTION: pick a or b")]
    [InlineData("STATUSES of the lanes")]
    public void An_action_tag_or_a_protected_line_is_never_a_notice(string body) => Assert.False(NoticeRule.IsNotice(body));

    [Theory, Trait("Lane", "unit")]
    [InlineData("STATUS: ready #123 079c2541", MessageKind.Ready, 123)]
    [InlineData("ACTION: ready #123 079c2541", MessageKind.Ready, 123)]
    [InlineData("status:ready #7 079c2541", MessageKind.Ready, 7)]
    [InlineData("STATUS: OWNER QUESTION which db?", MessageKind.OwnerQuestion, null)]
    [InlineData("STATUS: review clean on #123", MessageKind.Request, null)]
    public void The_classifier_reads_past_the_tag(string body, MessageKind kind, int? pr) =>
        Assert.Equal((kind, pr), MessageClassifier.Classify(body) is var c ? (c.Kind, c.Pr) : default);
}
