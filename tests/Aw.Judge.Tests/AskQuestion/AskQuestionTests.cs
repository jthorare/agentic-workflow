using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.AskQuestion;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.AskQuestion.AskQuestionHarness;

namespace Aw.Judge.Tests.AskQuestion;

[Lane(TestLane.Unit)]
public sealed class AskQuestionTests
{
    private const string Progress = """{"text":"progress on the parser, half done","senderKind":"teammate"}""";

    private readonly AskQuestionHarness _harness = new();

    private void Configure(string question, QuestionConfig entry) =>
        _harness.Config.Stored = JudgeConfig.Default with
        {
            Questions = new Dictionary<string, QuestionConfig>(JudgeConfig.Default.Questions, StringComparer.Ordinal) { [question] = entry },
        };

    [Fact]
    public async Task AskQuestion_asks_the_named_question_with_the_built_providers()
    {
        var cli = Cli().Decides("batch", 0.9);

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), cli);

        var decided = Assert.IsType<DecidedOutcome>(result.Value.Outcome);
        Assert.Equal(("batch", ProviderName.ClaudeCli), (decided.Decision, decided.Model));
        Assert.Null(result.Value.ConfigWarning);
    }

    [Fact]
    public async Task AskQuestion_escalates_a_question_the_config_disables_without_asking_anyone()
    {
        Configure("wake-gate", new QuestionConfig(Enabled: false, Threshold: 0.7));
        var cli = Cli().Decides("batch", 1);

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), cli);

        Assert.Equal(new EscalatedOutcome("question-disabled"), result.Value.Outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task AskQuestion_uses_the_config_s_threshold()
    {
        Configure("wake-gate", new QuestionConfig(Enabled: true, Threshold: 0.95));

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), Cli().Decides("batch", 0.9));

        var decided = Assert.IsType<DecidedOutcome>(result.Value.Outcome);
        Assert.Equal(("send", "fallback-after-undecided"), (decided.Decision, decided.ReasonCode));
    }

    [Fact]
    public async Task AskQuestion_limits_a_question_to_the_config_s_providers()
    {
        Configure("ask-check", new QuestionConfig(Enabled: true, Threshold: 0.6) { Providers = ["rules"] });
        var cli = Cli(ContentClass.Transcript).Decides("continue", 1);

        var result = await _harness.Ask(new AskQuestionRequest("ask-check", """{"transcriptTail":"run the tests"}"""), cli);

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), result.Value.Outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task AskQuestion_takes_the_agent_clis_from_the_config_s_providers_block()
    {
        _harness.Config.Stored = JudgeConfig.Default with { Providers = new ProvidersConfig(AgentClis: ["cursor-cli"]) };
        var claude = Cli().Decides("batch", 0.9);
        var cursor = new ScriptedProvider(ProviderName.CursorCli).Decides("send", 0.9);

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), claude, cursor);

        Assert.Equal(("send", ProviderName.CursorCli), Decided(result.Value.Outcome));
        Assert.Empty(claude.Calls);
    }

    [Fact]
    public async Task AskQuestion_leaves_jev_out_when_the_config_turns_it_off()
    {
        _harness.Config.Stored = JudgeConfig.Default with { Providers = new ProvidersConfig(Jev: false) };
        var jev = new ScriptedProvider(ProviderName.Jev).Decides("drop", 0.9);

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), jev, Cli().Decides("batch", 0.9));

        Assert.Equal(("batch", ProviderName.ClaudeCli), Decided(result.Value.Outcome));
        Assert.Empty(jev.Calls);
    }

    private static (string, ProviderName) Decided(EvaluationOutcome outcome)
    {
        var decided = Assert.IsType<DecidedOutcome>(outcome);
        return (decided.Decision, decided.Model);
    }

    [Fact]
    public async Task AskQuestion_limits_the_chain_to_the_allowlist()
    {
        var cli = Cli().Decides("batch", 1);

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress) { AllowedProviders = [ProviderName.Jev] }, cli);

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), result.Value.Outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task AskQuestion_runs_on_the_defaults_when_the_config_is_invalid_and_says_why()
    {
        _harness.Config.InvalidReason = "threshold out of range";
        Configure("wake-gate", new QuestionConfig(Enabled: false, Threshold: 0.7));

        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", Progress), Cli().Decides("batch", 0.9));

        Assert.Equal("batch", Assert.IsType<DecidedOutcome>(result.Value.Outcome).Decision);
        Assert.Contains("threshold out of range", result.Value.ConfigWarning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskQuestion_stores_the_calling_session_with_the_decision()
    {
        var result = await _harness.Ask(new AskQuestionRequest("wake-gate", """{"text":"ok","senderKind":"main"}""") { SessionId = SessionId.From("s-1") });

        _ = Assert.IsType<DecidedOutcome>(result.Value.Outcome);
        Assert.Equal(SessionId.From("s-1"), Assert.Single(_harness.Log.Decisions).Details?.SessionId);
    }

    [Fact]
    public async Task AskQuestion_refuses_a_name_that_isnt_a_question()
    {
        var result = await _harness.Ask(new AskQuestionRequest("nope", "{}"));

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("unknown question: nope", result.Error.Message, StringComparison.Ordinal);
    }
}
