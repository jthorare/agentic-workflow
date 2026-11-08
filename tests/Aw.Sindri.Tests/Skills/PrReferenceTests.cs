using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The 'pr' input of review and address-review (skill flows spec §3.1): a number or a github.com pull request link, or SND-SKILL-006; and where the host fetches the head to.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReferenceTests
{
    [Theory]
    [InlineData("7", 7)]
    [InlineData("#42", 42)]
    [InlineData(" 42 ", 42)]
    [InlineData("https://github.com/acme/api/pull/1631", 1631)]
    [InlineData("https://github.com/acme/api/pull/1631/files", 1631)]
    [InlineData("https://github.com/acme/api/pull/1631#issuecomment-9", 1631)]
    public void A_number_or_a_github_pull_link_gives_the_number(string input, int expected) => Assert.Equal(expected, PrReference.Number(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("seven")]
    [InlineData("42 and 43")]
    [InlineData("https://gitlab.com/acme/api/pull/7")]
    [InlineData("https://github.com/acme/api/issues/7")]
    [InlineData("https://github.com.evil.example/acme/api/pull/7")]
    public void Anything_else_is_SND_SKILL_006(string input)
    {
        var parsed = PrReference.Number(input);

        Assert.True(parsed.IsError);
        Assert.Equal(SindriErrors.SkillPrInputInvalid.Code, parsed.Error.Code);
    }

    [Fact]
    public void The_head_goes_to_a_branch_of_the_runs_own_namespace()
    {
        var branch = PrReference.BranchOf(WorkflowInstanceId.Of(InstanceAddress.From("ITM-00000000000000000000000000000001/review/1")), runNumber: 1);

        Assert.StartsWith("sindri/", branch, StringComparison.Ordinal);
        Assert.EndsWith("/1/pr", branch, StringComparison.Ordinal);
        Assert.Equal($"+refs/pull/7/head:refs/heads/{branch}", PrReference.Refspec(7, branch));
    }
}
