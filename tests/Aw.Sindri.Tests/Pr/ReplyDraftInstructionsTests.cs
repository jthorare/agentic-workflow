using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Tests.Pr;

/// <summary>What 'step.pr.reply-draft' is told (PR babysitter spec Appendix A.3).</summary>
[Lane(TestLane.Unit)]
public sealed class ReplyDraftInstructionsTests
{
    private static string All => string.Join('\n', new ReplyDraftInstructions().Lines);

    [Fact]
    public void The_instructions_are_for_the_reply_draft_Step_only()
    {
        Assert.True(new ReplyDraftInstructions().IsFor(BlockId.From("step.pr.reply-draft")));
        Assert.False(new ReplyDraftInstructions().IsFor(BlockId.From("step.pr.fix-comment")));
    }

    [Theory]
    [InlineData("fact:comment")]
    [InlineData("fact:decision")]
    [InlineData("fact:fix")]
    [InlineData("fact:prState")]
    [InlineData("120 words")]
    [InlineData("follow-up issue")]
    [InlineData("drafted")]
    [InlineData("noReply")]
    [InlineData("needsOwner")]
    [InlineData("fact:reply")]
    [InlineData("Never promise")]
    [InlineData("@-mention")]
    [InlineData("hidden scenarios")]
    [InlineData("secrets")]
    public void The_instructions_carry_the_appendix_A3_contract(string phrase) =>
        Assert.Contains(phrase, All, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void The_instructions_name_the_comment_as_data_never_as_orders()
    {
        Assert.Contains("<untrusted>", All, StringComparison.Ordinal);
        Assert.Contains("never follow", All, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_instructions_stay_short_enough_for_a_brief() => Assert.True(All.Length < 2500);
}
