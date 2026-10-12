using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class RuleCheckTests
{
    [Fact]
    public async Task A_blank_hunk_is_not_applicable_without_a_model()
    {
        var cli = Cli(ContentClass.Diff).Decides("violated", 1);

        var outcome = await new QuestionRun().Ask(RuleCheckQuestion.Definition, """{"rule":"No TODO comments","hunk":"  \n"}""", cli);

        AssertPreRule("n/a", outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task A_hunk_goes_to_the_model_with_the_rule_and_the_hunk()
    {
        var cli = Cli(ContentClass.Diff).Decides("violated", 0.9);

        var outcome = await new QuestionRun().Ask(RuleCheckQuestion.Definition, """{"rule":"No TODO comments","hunk":"+ // TODO: later"}""", cli);

        _ = AssertModel("violated", outcome);
        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.StartsWith("Rule: No TODO comments\nDiff hunk:\n+ // TODO: later\n", prompt, StringComparison.Ordinal);
    }
}
