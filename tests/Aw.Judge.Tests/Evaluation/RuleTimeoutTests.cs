using System.Text.RegularExpressions;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Evaluation.EvaluatorHarness;

namespace Aw.Judge.Tests.Evaluation;

/// <summary>
/// The question rules are regexes with a match timeout: a timeout is an expected failure (a huge input), recorded and
/// answered safely; any other throw from a rule is a bug in the rule and escapes.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RuleTimeoutTests
{
    private static string? TimesOut(TextInput _) => throw new RegexMatchTimeoutException("x", "y", TimeSpan.FromSeconds(1));

    [Fact]
    public async Task A_pre_rule_that_times_out_escalates_as_a_recorded_failure_without_asking_a_model()
    {
        var harness = new EvaluatorHarness();
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1);

        var outcome = await harness.Run(A.Question().WithPreRules(TimesOut).Build(), Text("hi"), cli);

        Assert.Equal(new EscalatedOutcome("rule-timeout"), outcome);
        Assert.Equal(DecisionOutcome.Failed, harness.Row.Outcome);
        Assert.Equal([("wake-gate", "rules", "rule-timeout")], harness.Log.Failures.Select(f => (f.Question, f.Provider, f.ReasonCode)));
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task An_input_check_that_times_out_escalates_the_same_way()
    {
        var harness = new EvaluatorHarness();

        var outcome = await harness.Run(A.Question().WithInputProblem(TimesOut).Build(), Text("hi"), new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1));

        Assert.Equal(new EscalatedOutcome("rule-timeout"), outcome);
        Assert.Equal([("rules", "rule-timeout")], harness.Log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task A_fallback_that_times_out_is_no_opinion_with_a_failure_row_and_the_chain_continues()
    {
        var harness = new EvaluatorHarness();
        var jev = new ScriptedProvider(ProviderName.Jev).Decides("batch", 0.5);
        var cli = new ScriptedProvider(ProviderName.ClaudeCli).Decides("drop", 0.9);
        var context = new EvaluationContext { Providers = [jev, cli], Chain = Chain(ProviderName.Jev, ProviderName.ClaudeCli) };

        var outcome = await harness.Run(A.Question().WithThreshold(0.8).WithFallbackRules(TimesOut).Build(), Text("hi"), context);

        Assert.Equal(new DecidedOutcome("drop", 0.9, ProviderName.ClaudeCli, "model", FirstId), outcome);
        Assert.Contains(("rules", "rule-timeout"), harness.Log.Failures.Select(f => (f.Provider, f.ReasonCode)));
    }

    [Fact]
    public async Task A_rule_that_throws_anything_else_is_a_bug_and_escapes()
    {
        var harness = new EvaluatorHarness();
        var jev = new ScriptedProvider(ProviderName.Jev).Decides("batch", 0.5);
        var question = A.Question().WithThreshold(0.8).WithFallbackRules(_ => throw new InvalidOperationException("boom")).Build();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await harness.Run(question, Text("hi"), jev));
    }
}
