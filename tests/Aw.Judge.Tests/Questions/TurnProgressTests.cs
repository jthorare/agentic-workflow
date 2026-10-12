using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class TurnProgressTests
{
    [Fact]
    public async Task A_turn_with_no_diff_is_stalled_without_a_model()
    {
        var cli = Cli(ContentClass.Diff).Decides("progressing", 1);

        var outcome = await new QuestionRun().Ask(TurnProgressQuestion.Definition, """{"problem":"parser drops comments","turnDiff":"  "}""", cli);

        AssertPreRule("stalled", outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task A_blank_problem_is_invalid_input()
    {
        var outcome = await new QuestionRun().Ask(TurnProgressQuestion.Definition, """{"problem":"","turnDiff":"+x"}""", Cli(ContentClass.Diff).Decides("stalled", 1));

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
    }

    [Fact]
    public async Task The_context_fields_are_optional_and_only_the_given_ones_reach_the_prompt()
    {
        var cli = Cli(ContentClass.Diff).Decides("off-target", 0.9);

        var outcome = await new QuestionRun().Ask(
            TurnProgressQuestion.Definition, """{"problem":"parser drops comments","turnDiff":"+keep comments","signals":"2 tests\nfail"}""", cli);

        _ = AssertModel("off-target", outcome);
        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.Contains("Signals: 2 tests fail", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Acceptance criteria:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Change before this turn:", prompt, StringComparison.Ordinal);
    }
}
