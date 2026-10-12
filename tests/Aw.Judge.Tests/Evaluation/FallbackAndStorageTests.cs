using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Evaluation.EvaluatorHarness;

namespace Aw.Judge.Tests.Evaluation;

[Lane(TestLane.Unit)]
public sealed class FallbackAndStorageTests
{
    private static readonly Dictionary<string, double> _below = new(StringComparer.Ordinal) { ["send"] = 0.3, ["batch"] = 0.55, ["drop"] = 0.15 };

    private static ScriptedProvider BelowJev() => new ScriptedProvider(ProviderName.Jev).Decides("batch", 0.55, _below);

    private static EvaluationContext JevThenCli(ScriptedProvider jev, ScriptedProvider cli) =>
        new() { Providers = [jev, cli], Chain = Chain(ProviderName.Jev, ProviderName.ClaudeCli) };

    private static async Task<DecisionDetails?> Agreement(Func<TextInput, string?>? fallback, string answer)
    {
        var harness = new EvaluatorHarness();
        var builder = A.Question();
        var question = fallback is null ? builder.Build() : builder.WithFallbackRules(fallback).Build();
        _ = await harness.Run(question, Text("hi"), new ScriptedProvider(ProviderName.ClaudeCli).Decides(answer, 0.9));
        return harness.Row.Details;
    }

    [Fact]
    public async Task A_fallback_rule_settles_a_below_threshold_answer_without_calling_the_next_provider()
    {
        var harness = new EvaluatorHarness();
        var slow = new ScriptedProvider(ProviderName.ClaudeCli).Decides("drop", 1);

        var outcome = await harness.Run(A.Question().WithThreshold(0.8).WithFallbackRules(_ => "send").Build(), Text("hi"), JevThenCli(BelowJev(), slow));

        Assert.Equal(new DecidedOutcome("send", 1, ProviderName.Rules, "fallback-after-undecided", FirstId), outcome);
        Assert.Empty(slow.Calls);
        Assert.Equal(("rules", 0), (harness.Row.Provider, harness.Row.ChainPosition));
        Assert.Equal([new SkippedProvider("jev", SkipReason.BelowThreshold)], harness.Row.Skipped);
        Assert.Equal(Core.Decisions.Agreement.Undecided, harness.Row.Details?.Agreement);
        Assert.Equal(_below, harness.Row.Details?.Probabilities);
        Assert.Empty(harness.Log.Failures);
    }

    [Fact]
    public async Task A_null_fallback_continues_the_chain_and_keeps_the_undecided_probabilities()
    {
        var harness = new EvaluatorHarness();
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Decides("drop", 0.9);

        var outcome = await harness.Run(A.Question().WithThreshold(0.8).WithFallbackRules(_ => null).Build(), Text("hi"), JevThenCli(BelowJev(), cli));

        Assert.Equal(new DecidedOutcome("drop", 0.9, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Equal(_below, harness.Row.Details?.Probabilities);
    }

    [Fact]
    public async Task The_settling_provider_probabilities_win_over_an_earlier_undecided_answer()
    {
        var harness = new EvaluatorHarness();
        var own = new Dictionary<string, double>(StringComparer.Ordinal) { ["drop"] = 0.9 };
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Decides("drop", 0.9, own);

        _ = await harness.Run(A.Question().WithThreshold(0.8).Build(), Text("hi"), JevThenCli(BelowJev(), cli));

        Assert.Equal(own, harness.Row.Details?.Probabilities);
    }

    [Fact]
    public async Task An_undecided_answer_without_probabilities_keeps_the_earlier_ones_through_the_escalation()
    {
        var harness = new EvaluatorHarness();
        var bare = new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 0.1);

        var outcome = await harness.Run(A.Question().WithThreshold(0.8).Build(), Text("hi"), JevThenCli(BelowJev(), bare));

        Assert.Equal(new EscalatedOutcome("below-threshold"), outcome);
        Assert.Equal(_below, harness.Row.Details?.Probabilities);
        Assert.Empty(harness.Log.Failures);
    }

    [Fact]
    public async Task A_model_that_disagrees_with_the_rules_overrode_them()
    {
        var details = await Agreement(_ => "send", "batch");

        Assert.Equal(("send", Core.Decisions.Agreement.Overrode), (details?.RulesOpinion, details?.Agreement));
    }

    [Fact]
    public async Task A_model_that_matches_the_rules_agreed()
    {
        var details = await Agreement(_ => "send", "send");

        Assert.Equal(("send", Core.Decisions.Agreement.Agreed), (details?.RulesOpinion, details?.Agreement));
    }

    [Fact]
    public async Task No_fallback_a_null_one_or_one_that_times_out_records_no_opinion()
    {
        foreach (var fallback in new Func<TextInput, string?>?[] { null, _ => null, _ => throw new System.Text.RegularExpressions.RegexMatchTimeoutException() })
        {
            var details = await Agreement(fallback, "send");

            Assert.Equal((null, null), (details?.RulesOpinion, details?.Agreement));
        }
    }

    [Fact]
    public async Task A_failed_decision_write_still_returns_the_decision_and_records_decision_write_failed()
    {
        var log = new InMemoryDecisionLog();
        _ = await log.Record(A.Decision().WithId(FirstId.Value).Build(), CancellationToken.None);
        var harness = new EvaluatorHarness(log);

        var outcome = await harness.Run(A.Question().Build(), Text("hmm"), new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));

        Assert.Equal(new DecidedOutcome("send", 1, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Equal([("claude-cli", "decision-write-failed")], log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task An_unusable_store_never_changes_the_outcome()
    {
        var harness = new EvaluatorHarness(new InMemoryDecisionLog { Unavailable = true });

        var decided = await harness.Run(A.Question().Build(), Text("hmm"), new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));
        var escalated = await harness.Run(A.Question().Build(), Text("hmm"), new ScriptedProvider(ProviderName.ClaudeCli).Returns(new ProviderErrored("exit-1")));

        Assert.Equal("send", Assert.IsType<DecidedOutcome>(decided).Decision);
        Assert.Equal(new EscalatedOutcome("no-provider-decided"), escalated);
    }

    [Fact]
    public async Task The_stored_input_is_redacted_while_the_digest_covers_the_unredacted_input()
    {
        var harness = new EvaluatorHarness();
        var input = Text($"token {SampleSecrets.AnthropicKey} here");

        _ = await harness.Run(A.Question().Build(), input, new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));

        Assert.Equal("""{"text":"token [REDACTED] here"}""", harness.Row.Details?.InputJson);
        Assert.Equal(InputJson.Digest(input, JsonOptions), harness.Row.InputDigest);
    }

    [Fact]
    public async Task The_session_id_is_stored_with_the_details()
    {
        var harness = new EvaluatorHarness();
        var context = new EvaluationContext { Providers = [new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1)], SessionId = SessionId.From("s1") };

        _ = await harness.Run(A.Question().Build(), Text("hi"), context);

        Assert.Equal(SessionId.From("s1"), harness.Row.Details?.SessionId);
    }
}
