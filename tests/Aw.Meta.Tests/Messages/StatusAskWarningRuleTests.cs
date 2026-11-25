using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class StatusAskWarningRuleTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_status_line_with_a_question_mark_warns() =>
        Assert.Equal(NoticeRule.StatusAskText, NoticeRule.StatusAskWarning("STATUS: want me to rerun it?"));

    [Fact, Trait("Lane", "unit")]
    public void A_status_line_without_a_question_mark_is_fine() =>
        Assert.Null(NoticeRule.StatusAskWarning("STATUS: review clean"));

    [Fact, Trait("Lane", "unit")]
    public void An_action_line_with_a_question_mark_is_fine() =>
        Assert.Null(NoticeRule.StatusAskWarning("ACTION: want me to rerun it?"));

    [Fact, Trait("Lane", "unit")]
    public void An_untagged_line_with_a_question_mark_is_fine() =>
        Assert.Null(NoticeRule.StatusAskWarning("want me to rerun it?"));

    [Fact, Trait("Lane", "unit")]
    public void A_protected_status_line_with_a_question_mark_is_fine() =>
        Assert.Null(NoticeRule.StatusAskWarning("STATUS: OK #12 abc123 why?"));
}
