using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>How a skill watch's failure text becomes the Step's code and reason: split at the first ": ", and a text with none is all reason.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillFailureTextTests
{
    [Theory]
    [InlineData("SND-SKILL-004: detail", "SND-SKILL-004", "detail")]
    [InlineData("a: b: c", "a", "b: c")]
    [InlineData("SND-SKILL-003: review said 'MAYBE'", "SND-SKILL-003", "review said 'MAYBE'")]
    public void A_failure_with_a_colon_and_space_splits_at_the_first_one(string failure, string code, string reason)
        => Assert.Equal((code, reason), SkillFailureText.Split(failure));

    [Theory]
    [InlineData("plain text")]
    [InlineData("no space:after")]
    [InlineData("")]
    public void A_failure_with_no_colon_and_space_has_no_code_and_is_all_reason(string failure)
        => Assert.Equal((null, failure), SkillFailureText.Split(failure));
}
