using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class NoticeRuleTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("FYI: gate running on #12")]
    [InlineData("notice: lane api restarted")]
    [InlineData("LANE MAP v12: meta=a, api=b")]
    [InlineData("main moved to abc1234: #12 merged.")]
    [InlineData("ack")]
    [InlineData("Acked, thanks")]
    [InlineData("thank you")]
    [InlineData("got it")]
    [InlineData("roger that")]
    [InlineData("noted")]
    [InlineData("ui -> meta: ack lane map v163")]
    [InlineData("t1-ui -> t1-meta: FYI: pushed")]
    public void These_lines_are_notices(string body) => Assert.True(NoticeRule.IsNotice(body));

    [Theory, Trait("Lane", "unit")]
    [InlineData("ready #12 abc1234")]
    [InlineData("OK #12 at abc1234def")]
    [InlineData("NOT OK #12: tests red")]
    [InlineData("DIGEST READY /path/x.md")]
    [InlineData("OWNER QUESTION: pick a or b")]
    [InlineData("OWNER ANSWER q7: yes")]
    [InlineData("api lane online")]
    [InlineData("ok, merging #12")]
    [InlineData("ack, can you rerun the gate?")]
    [InlineData("acknowledgement of the plan")]
    [InlineData("LANE MAP vX")]
    [InlineData("please rebase #12")]
    [InlineData("Ack: ui champions #12. ideas is at ~200k context, please lane-recycle me.")]
    [InlineData("FYI: aw-wt/x is cleaned, please sweep it")]
    [InlineData("")]
    public void These_lines_are_not_notices(string body) => Assert.False(NoticeRule.IsNotice(body));

    [Fact, Trait("Lane", "unit")]
    public void An_ack_of_240_bytes_or_more_is_not_a_notice()
    {
        Assert.True(NoticeRule.IsNotice("ack, " + new string('x', 200)));
        Assert.False(NoticeRule.IsNotice("ack, " + new string('x', 240)));
    }
}
