using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class WakeGateTests
{
    private static string Input(string text, string sender = "teammate") =>
        $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"senderKind":"{{sender}}"}""";

    [Theory]
    [InlineData("""{"type":"ack","from":"worker-1"}""", "drop")]
    [InlineData("""  {"type": "idle_notification"}""", "drop")]
    [InlineData("The build failed on step 3, please look at it", "send")]
    [InlineData("I have a QUESTION about the migration plan", "send")]
    [InlineData("Blocked on the schema review for now", "send")]
    [InlineData("ok thanks", "drop")]
    [InlineData("   abcdefghijkl   ", "drop")]
    public async Task Pure_acks_and_very_short_text_drop_and_urgent_words_send_without_a_model(string text, string decision)
    {
        var cli = Cli().Decides("batch", 1);

        var outcome = await new QuestionRun().Ask(WakeGateQuestion.Definition, Input(text), cli);

        AssertPreRule(decision, outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task Text_no_rule_settles_goes_to_the_model_with_the_message_in_the_prompt()
    {
        var cli = Cli().Decides("batch", 0.9);

        var outcome = await new QuestionRun().Ask(WakeGateQuestion.Definition, Input("abcdefghijklm", "subagent"), cli);

        _ = AssertModel("batch", outcome);
        var call = Assert.Single(cli.Calls);
        Assert.Contains("\"abcdefghijklm\"", call.Question.Prompt, StringComparison.Ordinal);
        Assert.Equal(["send", "batch", "drop"], call.Question.Outputs);
        Assert.Equal(TimeSpan.FromSeconds(10), call.Budget);
    }

    [Fact]
    public async Task An_unsure_model_falls_back_to_send()
    {
        var outcome = await new QuestionRun().Ask(WakeGateQuestion.Definition, Input("progress on the parser, half done"), Cli().Decides("batch", 0.69));

        AssertFallback("send", outcome);
    }

    [Theory]
    [InlineData("""{"text":"progress on the parser","senderKind":"robot"}""")]
    [InlineData("""{"senderKind":"teammate"}""")]
    [InlineData("""{"text":"progress on the parser"}""")]
    [InlineData("not json")]
    public async Task Input_outside_the_hook_shape_escalates_as_invalid_input(string input)
    {
        var outcome = await new QuestionRun().Ask(WakeGateQuestion.Definition, input, Cli().Decides("send", 1));

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
    }
}
