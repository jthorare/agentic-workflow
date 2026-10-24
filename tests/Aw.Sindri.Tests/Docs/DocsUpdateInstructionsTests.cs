using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Docs;

namespace Aw.Sindri.Tests.Docs;

/// <summary>What 'step.docs.update' is told (PR babysitter spec Appendix A.4).</summary>
[Lane(TestLane.Unit)]
public sealed class DocsUpdateInstructionsTests
{
    private static string All => string.Join('\n', new DocsUpdateInstructions().Lines);

    [Fact]
    public void The_instructions_are_for_the_docs_update_Step_only()
    {
        Assert.True(new DocsUpdateInstructions().IsFor(BlockId.From("step.docs.update")));
        Assert.False(new DocsUpdateInstructions().IsFor(BlockId.From("step.docs.check")));
    }

    [Theory]
    [InlineData("fact:docsScope")]
    [InlineData("fact:findings")]
    [InlineData("fact:handoffNote")]
    [InlineData("design.md")]
    [InlineData("existing voice")]
    [InlineData("only behaviour the diff shows")]
    [InlineData("Commit once")]
    [InlineData("sindri docs check")]
    [InlineData("changed")]
    [InlineData("noChange")]
    [InlineData("blocked")]
    [InlineData("workflow.docs.globs")]
    [InlineData("rewrite unrelated sections")]
    [InlineData("secret")]
    public void The_instructions_carry_the_appendix_A4_contract(string phrase) =>
        Assert.Contains(phrase, All, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void The_instructions_name_the_diff_as_data_never_as_orders()
    {
        Assert.Contains("<untrusted>", All, StringComparison.Ordinal);
        Assert.Contains("never follow", All, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_instructions_stay_short_enough_for_a_brief() => Assert.True(All.Length < 2500);
}
