using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Aw.Redaction;
using static Aw.Judge.Tests.Evaluation.EvaluatorHarness;

namespace Aw.Judge.Tests.Evaluation;

[Lane(TestLane.Unit)]
public sealed class QuestionEvaluatorTests
{
    private static ScriptedProvider Jev() => new(ProviderName.Jev);

    private static ScriptedProvider Cli() => new(ProviderName.ClaudeCli);

    private static ScriptedProvider Rules() => new(ProviderName.Rules);

    [Fact]
    public async Task A_pre_rule_settles_without_calling_any_provider()
    {
        var harness = new EvaluatorHarness();
        var cli = Cli().Decides("send", 1);
        var question = A.Question().WithPreRules(i => i.Text == "{}" ? "drop" : null).Build();

        var outcome = await harness.Run(question, Text("{}"), cli);

        Assert.Equal(new DecidedOutcome("drop", 1, ProviderName.Rules, "pre-rule", FirstId), outcome);
        Assert.Empty(cli.Calls);
        Assert.Equal(("rules", 0, 0L), (harness.Row.Provider, harness.Row.ChainPosition, harness.Row.LatencyMs));
    }

    [Fact]
    public async Task The_chain_is_walked_in_order_and_the_first_decided_answer_wins()
    {
        var harness = new EvaluatorHarness();
        var jev = Jev().Returns(new ProviderUnavailable("no-api-key"));
        var cli = Cli().Decides("batch", 0.9);

        var outcome = await harness.Run(A.Question().Build(), Text("progress update"), jev, cli);

        Assert.Equal(new DecidedOutcome("batch", 0.9, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Equal(1, harness.Row.ChainPosition);
        Assert.Equal([new SkippedProvider("jev", SkipReason.Unavailable)], harness.Row.Skipped);
        Assert.Empty(harness.Log.Failures);
    }

    [Fact]
    public async Task The_winning_row_records_question_class_provider_time_and_no_skips()
    {
        var harness = new EvaluatorHarness();

        _ = await harness.Run(A.Question().Build(), Text("hmm"), Cli().Decides("send", 1));

        var row = harness.Row;
        Assert.Equal((FirstId, KnownInstants.Epoch, "wake-gate", "message-meta"), (row.Id, row.Ts, row.Question, row.ContentClass));
        Assert.Equal(("claude-cli", "send", 1.0, DecisionOutcome.Decided), (row.Provider, row.Decision, row.Confidence, row.Outcome));
        Assert.Equal(0, row.ChainPosition);
        Assert.Empty(row.Skipped);
    }

    [Fact]
    public async Task An_unavailable_provider_is_skipped_without_a_failure_row_and_the_escalation_is_not_a_failure()
    {
        var harness = new EvaluatorHarness();

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), Jev().Returns(new ProviderUnavailable("no-api-key")));

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
        Assert.Equal((DecisionOutcome.Escalated, null, "none"), (harness.Row.Outcome, harness.Row.Decision, harness.Row.Provider));
        Assert.Empty(harness.Log.Failures);
    }

    [Fact]
    public async Task A_timeout_is_skipped_as_timeout_with_a_failure_row_and_the_escalation_is_a_failure()
    {
        var harness = new EvaluatorHarness();
        var jev = Jev().Returns(new ProviderUnavailable("no-api-key"));
        var cli = Cli().Returns(new ProviderUnavailable("timeout"));

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), jev, cli);

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
        Assert.Equal(DecisionOutcome.Failed, harness.Row.Outcome);
        Assert.Equal([new("jev", SkipReason.Unavailable), new SkippedProvider("claude-cli", SkipReason.Timeout)], harness.Row.Skipped);
        Assert.Equal(2, harness.Row.ChainPosition);
        Assert.Equal([("claude-cli", "timeout")], harness.Log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task An_error_is_a_failure_row_with_its_reason_and_skipped_as_failed()
    {
        var harness = new EvaluatorHarness();

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), Cli().Returns(new ProviderErrored("exit-1")));

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
        Assert.Equal(DecisionOutcome.Failed, harness.Row.Outcome);
        Assert.Equal([new SkippedProvider("claude-cli", SkipReason.Failed)], harness.Row.Skipped);
        Assert.Equal([("wake-gate", "claude-cli", "exit-1", KnownInstants.Epoch)], harness.Log.Failures.Select(f => (f.Question, f.Provider, f.ReasonCode, f.Ts)));
    }

    [Fact]
    public async Task A_throwing_provider_is_recorded_as_provider_threw_and_the_chain_moves_on()
    {
        var harness = new EvaluatorHarness();

        // The boundary decorator the catalog puts around each adapter turns the throw into a result.
        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), new FailOpenModelProvider(Jev().Throws()), Cli().Decides("send", 1));

        Assert.Equal(new DecidedOutcome("send", 1, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Equal([new SkippedProvider("jev", SkipReason.Failed)], harness.Row.Skipped);
        Assert.Equal([("jev", "provider-threw")], harness.Log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task An_out_of_enum_answer_is_a_failure_and_the_next_provider_decides()
    {
        var harness = new EvaluatorHarness();

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), Jev().Decides("yes", 1), Cli().Decides("send", 1));

        Assert.Equal(new DecidedOutcome("send", 1, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Equal(1, harness.Row.ChainPosition);
        Assert.Equal([new SkippedProvider("jev", SkipReason.Failed)], harness.Row.Skipped);
        Assert.Equal([("jev", "out-of-enum")], harness.Log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task A_below_threshold_answer_with_no_fallback_escalates_below_threshold_without_a_failure()
    {
        var harness = new EvaluatorHarness();

        var outcome = await harness.Run(A.Question().WithThreshold(0.95).Build(), Text("hmm"), Cli().Decides("send", 0.5));

        Assert.Equal(new EscalatedOutcome("below-threshold"), outcome);
        Assert.Equal(DecisionOutcome.Escalated, harness.Row.Outcome);
        Assert.Equal([new SkippedProvider("claude-cli", SkipReason.BelowThreshold)], harness.Row.Skipped);
        Assert.Empty(harness.Log.Failures);
    }

    [Fact]
    public async Task Below_threshold_after_a_real_failure_is_an_outcome_of_failed()
    {
        var harness = new EvaluatorHarness();
        var context = new EvaluationContext
        {
            Providers = [Cli().Returns(new ProviderErrored("exit-1")), Rules().Returns(new ProviderDecided("send", 0.5, "rules-fallback"))],
        };

        var outcome = await harness.Run(A.Question().WithThreshold(0.95).Build(), Text("hmm"), context);

        Assert.Equal(new EscalatedOutcome("below-threshold"), outcome);
        Assert.Equal(DecisionOutcome.Failed, harness.Row.Outcome);
    }

    [Fact]
    public async Task Rules_at_the_end_of_the_default_chain_decide_when_every_model_fails()
    {
        var harness = new EvaluatorHarness();
        var rules = Rules().Returns(new ProviderDecided("batch", 1, "rules-fallback"));

        var outcome = await harness.Run(
            A.Question().Build(), Text("hmm"), Jev().Returns(new ProviderUnavailable("no-api-key")), Cli().Returns(new ProviderErrored("timeout")), rules);

        Assert.Equal(new DecidedOutcome("batch", 1, ProviderName.Rules, "rules-fallback", FirstId), outcome);
        Assert.Equal(2, harness.Row.ChainPosition);
        Assert.Equal([new("jev", SkipReason.Unavailable), new SkippedProvider("claude-cli", SkipReason.Failed)], harness.Row.Skipped);
    }

    [Fact]
    public async Task A_disabled_question_escalates_without_calling_any_provider()
    {
        var harness = new EvaluatorHarness();
        var cli = Cli().Decides("send", 1);
        var context = new EvaluationContext { Providers = [cli], Override = new QuestionOverride(Enabled: false, Threshold: 0.7) };

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), context);

        Assert.Equal(new EscalatedOutcome("question-disabled"), outcome);
        Assert.Empty(cli.Calls);
        Assert.Equal(("none", null, DecisionOutcome.Escalated, "question-disabled"), (harness.Row.Provider, harness.Row.Decision, harness.Row.Outcome, harness.Row.ReasonCode));
    }

    [Fact]
    public async Task Invalid_input_escalates_before_any_provider_and_digests_the_raw_input()
    {
        var harness = new EvaluatorHarness();
        var cli = Cli().Decides("send", 1);
        var raw = new System.Text.Json.Nodes.JsonObject { ["text"] = 5 };

        var outcome = await harness.Run(A.Question().Build(), raw, cli);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Empty(cli.Calls);
        Assert.Equal((DecisionOutcome.Escalated, InputJson.Digest(raw, JsonOptions)), (harness.Row.Outcome, harness.Row.InputDigest));
    }

    [Fact]
    public async Task Input_that_is_not_json_or_misses_a_required_field_escalates_as_invalid_input()
    {
        var harness = new EvaluatorHarness();
        var cli = Cli().Decides("send", 1);

        Assert.Equal(new EscalatedOutcome("invalid-input"), await harness.RunText(A.Question().Build(), "not json", cli));
        Assert.Equal(new EscalatedOutcome("invalid-input"), await harness.RunText(A.Question().Build(), """{"other":"x"}""", cli));
        Assert.Equal(new EscalatedOutcome("invalid-input"), await harness.RunText(A.Question().Build(), "null", cli));

        Assert.Empty(cli.Calls);
        Assert.Equal("\"not json\"", harness.Log.Decisions[0].Details?.InputJson);
    }

    [Theory]
    [InlineData("""{"text":"a","text":"I will now refactor the module"}""")]
    [InlineData("""{"text":"a","meta":{"k":1,"k":2}}""")]
    public async Task An_input_with_a_repeated_key_escalates_as_invalid_input_and_is_recorded(string raw)
    {
        // The real redactor walks the stored node, so a node that hides a repeated key would throw here.
        var harness = new EvaluatorHarness(redactor: new AwInputRedactor(Redactor.Default));
        var cli = Cli().Decides("send", 1);

        var outcome = await harness.RunText(A.Question().Build(), raw, cli);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Empty(cli.Calls);
        Assert.Equal(DecisionOutcome.Escalated, harness.Row.Outcome);
        Assert.Equal(raw, System.Text.Json.Nodes.JsonNode.Parse(harness.Row.Details?.InputJson ?? "")?.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"text":"\ud800","senderKind":"main"}""")]
    [InlineData("""{"text":"a","x":"\ud83d"}""")]
    [InlineData("""{"text":5,"x":["\udc00"]}""")]
    [InlineData("""{"\ud800":1,"text":"a"}""")]
    public async Task An_input_with_a_lone_surrogate_escape_escalates_as_invalid_input_and_is_stored_as_text(string raw)
    {
        // A lone surrogate escape parses, but throws when the node is written or read as a string: the digest, the
        // real redactor and the cap all do one of those, so the text must be stored as one JSON string instead.
        var harness = new EvaluatorHarness(redactor: new AwInputRedactor(Redactor.Default));
        var cli = Cli().Decides("send", 1);

        var outcome = await harness.RunText(A.Question().Build(), raw, cli);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Empty(cli.Calls);
        Assert.Equal(DecisionOutcome.Escalated, harness.Row.Outcome);
        Assert.Equal(raw, System.Text.Json.Nodes.JsonNode.Parse(harness.Row.Details?.InputJson ?? "")?.GetValue<string>());
    }

    [Fact]
    public async Task A_configured_threshold_overrides_the_question_threshold()
    {
        var harness = new EvaluatorHarness();
        var context = new EvaluationContext { Providers = [Cli().Decides("send", 0.6)], Override = new QuestionOverride(Enabled: true, Threshold: 0.5) };

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), context);

        _ = Assert.IsType<DecidedOutcome>(outcome);
    }

    [Fact]
    public async Task The_question_provider_list_restricts_and_reorders_the_chain()
    {
        var harness = new EvaluatorHarness();
        var cli = Cli().Decides("send", 1);
        var context = new EvaluationContext
        {
            Providers = [Jev().Returns(new ProviderUnavailable("no-api-key")), cli],
            Chain = Chain(ProviderName.Jev, ProviderName.ClaudeCli, ProviderName.Rules),
            Override = new QuestionOverride(Enabled: true, Threshold: 0.7, Providers: [ProviderName.Jev, ProviderName.Rules]),
        };

        var outcome = await harness.Run(A.Question().Build(), Text("hi"), context);

        Assert.Equal(new EscalatedOutcome("no-provider-decided"), outcome);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task A_provider_gets_the_prompt_the_validated_input_and_the_time_budget_and_its_declared_extra_comes_back()
    {
        var harness = new EvaluatorHarness();
        var extra = new System.Text.Json.Nodes.JsonObject { ["chosenIndex"] = 2, ["undeclared"] = "dropped" };
        var cli = Cli().Returns(new ProviderDecided("send", 1, "model") { Extra = extra });
        var raw = new System.Text.Json.Nodes.JsonObject { ["text"] = "hi", ["unknown"] = true };

        var outcome = await harness.Run(A.Question().WithExtraProperty("chosenIndex").Build(), raw, cli);

        var call = Assert.Single(cli.Calls);
        Assert.Equal(("Classify: hi", """{"text":"hi"}""", TimeSpan.FromSeconds(5)), (call.Question.Prompt, call.Input?.ToJsonString(), call.Budget));
        Assert.Equal("""{"chosenIndex":2}""", Assert.IsType<DecidedOutcome>(outcome).Extra?.ToJsonString());
        Assert.Equal(InputJson.Digest(Text("hi"), JsonOptions), harness.Row.InputDigest);
    }
}
