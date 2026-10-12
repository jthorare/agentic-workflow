using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class AskCheckTests
{
    private static string Input(string tail) => $$"""{"transcriptTail":{{System.Text.Json.JsonSerializer.Serialize(tail)}}}""";

    [Theory]
    [InlineData("Tests pass. Next I'll merge the PR.")]
    [InlineData("I'll delete the old branch now")]
    [InlineData("Going to force-push the rebased branch")]
    [InlineData("I force pushed it already")]
    [InlineData("Running: git push -f origin feat/x")]
    [InlineData("Running: git push --force-with-lease origin feat/x")]
    [InlineData("Running: git push origin +feat/x:feat/x")]
    public async Task The_deny_list_always_asks_whatever_a_model_would_say(string tail)
    {
        var cli = Cli(ContentClass.Transcript).Decides("continue", 1);

        var outcome = await new QuestionRun().Ask(AskCheckQuestion.Definition, Input(tail), cli);

        AssertPreRule("ask", outcome);
        Assert.Empty(cli.Calls);
    }

    [Theory]
    [InlineData("Running: git push origin feat/x")]
    [InlineData("Next I'll run the unit tests")]
    public async Task A_plain_push_or_any_other_step_goes_to_the_model(string tail)
    {
        var outcome = await new QuestionRun().Ask(AskCheckQuestion.Definition, Input(tail), Cli(ContentClass.Transcript).Decides("continue", 0.9));

        _ = AssertModel("continue", outcome);
    }

    [Fact]
    public async Task An_unsure_model_falls_back_to_ask()
    {
        var outcome = await new QuestionRun().Ask(AskCheckQuestion.Definition, Input("Next I'll run the tests"), Cli(ContentClass.Transcript).Decides("continue", 0.59));

        AssertFallback("ask", outcome);
    }

    [Fact]
    public async Task With_no_model_to_ask_nothing_decides()
    {
        var outcome = await new QuestionRun().Ask(AskCheckQuestion.Definition, Input("Next I'll run the tests"), new RulesProvider());

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
    }
}
