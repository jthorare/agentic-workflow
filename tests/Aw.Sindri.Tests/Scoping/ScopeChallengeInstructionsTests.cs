using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>What 'step.scope.challenge' is told about its output (#1102): one round, and challenge.json is the log of earlier rounds.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeChallengeInstructionsTests
{
    private static string All => string.Join('\n', new ScopeChallengeInstructions().Lines);

    [Fact]
    public void The_instructions_are_for_the_challenge_Step_only()
    {
        Assert.True(new ScopeChallengeInstructions().IsFor(BlockId.From("step.scope.challenge")));
        Assert.False(new ScopeChallengeInstructions().IsFor(BlockId.From("step.scope.challenge-log")));
    }

    [Theory]
    [InlineData("this round only")]
    [InlineData("challenge-round.json")]
    [InlineData("challenge.json is the log of the earlier rounds")]
    [InlineData("never put a \"rounds\" array")]
    public void The_lines_name_the_one_round_shape_and_that_the_log_is_not_the_output(string text) =>
        Assert.Contains(text, All, StringComparison.Ordinal);
}
