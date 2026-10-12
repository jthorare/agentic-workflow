using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class BriefScopeTests
{
    private static readonly string[] _fields = ["general-purpose", "Add the parser", "parses the fixtures", "npm test"];

    private const string Complete = """{"agentType":"general-purpose","goal":"Add the parser","acceptanceCriteria":"parses the fixtures","proofCommand":"npm test"}""";

    [Theory]
    [InlineData("""{"agentType":"Explore","goal":"","acceptanceCriteria":"","proofCommand":""}""", "ready")]
    [InlineData("""{"agentType":"claude-code-guide","goal":"","acceptanceCriteria":"","proofCommand":""}""", "ready")]
    [InlineData("""{"agentType":"lean-researcher","goal":"","acceptanceCriteria":"","proofCommand":""}""", "ready")]
    [InlineData("""{"agentType":"general-purpose","goal":"","acceptanceCriteria":"","proofCommand":"","skillInternal":true}""", "ready")]
    [InlineData("""{"agentType":"general-purpose","goal":"","acceptanceCriteria":"parses the fixtures","proofCommand":"npm test"}""", "missing")]
    [InlineData("""{"agentType":"general-purpose","goal":"Add the parser","acceptanceCriteria":"  \n ","proofCommand":"npm test","skillInternal":false}""", "missing")]
    public async Task Exempt_dispatches_are_ready_and_a_blank_goal_or_acceptance_is_missing_without_a_model(string input, string decision)
    {
        var cli = Cli(ContentClass.Brief).Decides("needs_design", 1);

        var outcome = await new QuestionRun().Ask(BriefScopeQuestion.Definition, input, cli);

        AssertPreRule(decision, outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task A_complete_brief_goes_to_the_model_with_every_field_in_the_prompt()
    {
        var cli = Cli(ContentClass.Brief).Decides("needs_design", 0.8);

        var outcome = await new QuestionRun().Ask(BriefScopeQuestion.Definition, Complete, cli);

        _ = AssertModel("needs_design", outcome);
        var prompt = Assert.Single(cli.Calls).Question.Prompt;
        Assert.All(_fields, s => Assert.Contains(s, prompt, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unsure_model_escalates_since_brief_scope_has_no_fallback()
    {
        var outcome = await new QuestionRun().Ask(BriefScopeQuestion.Definition, Complete, Cli(ContentClass.Brief).Decides("ready", 0.5));

        Assert.Equal(new EscalatedOutcome("below-threshold"), outcome);
    }

    [Fact]
    public async Task A_brief_without_a_proof_command_is_invalid_input()
    {
        var outcome = await new QuestionRun().Ask(
            BriefScopeQuestion.Definition, """{"agentType":"general-purpose","goal":"g","acceptanceCriteria":"a"}""", Cli(ContentClass.Brief).Decides("ready", 1));

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
    }
}
