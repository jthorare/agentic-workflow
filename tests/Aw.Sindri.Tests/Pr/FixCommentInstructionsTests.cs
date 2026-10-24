using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Tests.Pr;

/// <summary>What 'step.pr.fix-comment' is told (PR babysitter spec Appendix A.2).</summary>
[Lane(TestLane.Unit)]
public sealed class FixCommentInstructionsTests
{
    private static string All => string.Join('\n', new FixCommentInstructions().Lines);

    [Fact]
    public void The_instructions_are_for_the_fix_comment_Step_only()
    {
        Assert.True(new FixCommentInstructions().IsFor(BlockId.From("step.pr.fix-comment")));
        Assert.False(new FixCommentInstructions().IsFor(BlockId.From("step.pr.ci-fix")));
    }

    [Theory]
    [InlineData("fact:comment")]
    [InlineData("fact:decision")]
    [InlineData("fact:ownerNote")]
    [InlineData("nothing else")]
    [InlineData("test that would fail without it")]
    [InlineData("effort: cheap")]
    [InlineData("effort: bot")]
    [InlineData("Sindri-Comment")]
    [InlineData("workflow.build.commands")]
    [InlineData("fixed")]
    [InlineData("declined")]
    [InlineData("cannotFix")]
    [InlineData("fact:fix")]
    [InlineData("fact:handoffNote")]
    [InlineData("allowedFiles")]
    [InlineData("Never reply, push or resolve")]
    public void The_instructions_carry_the_appendix_A2_contract(string phrase) =>
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
