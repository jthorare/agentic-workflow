using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;

namespace Aw.Sindri.Tests.Workflow.Judge;

/// <summary>'step.judge.choose' (DT spec 3.1): the judge's envelope becomes the outcome, 'fact:decision' is filled on every outcome, and what goes to the judge is redacted, capped and limited to the providers the runner resolved.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeChooseStepTests
{
    private static readonly string[] _choices = ["ui: what a user sees changed", "api: an HTTP contract changed"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] _defaultFacts = ["fact:built"];
    private static readonly string[] _twoFacts = ["fact:built", "fact:phasePacket"];
    private static readonly string[] _codexAndRules = ["codex-cli", "rules"];
    private static readonly string[] _badChoices = ["ui", "api: x"];

    private sealed class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("hunter2", "[redacted]", StringComparison.Ordinal);
    }

    internal static StepContext Context(Dictionary<string, JsonElement>? arguments = null, params (string Key, string Json)[] facts)
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["prompt"] = JsonSerializer.SerializeToElement("Which evidence proves this change works?"),
            ["choices"] = JsonSerializer.SerializeToElement(_choices),
            ["facts"] = JsonSerializer.SerializeToElement(_defaultFacts),
        };
        foreach (var (name, value) in arguments ?? [])
        {
            args[name] = value;
        }

        var held = facts.Length == 0 ? [("fact:built", "{\"head\":\"abc\"}")] : facts;
        return new StepContext(
            BlockId.From("step.judge.choose"),
            args,
            held.ToDictionary(static f => FactKey.From(f.Key), static f => JsonSerializer.SerializeToElement(f.Json)),
            InjectionSections.Empty);
    }

    private static Dictionary<string, JsonElement> With(string name, object value) => new(StringComparer.Ordinal) { [name] = JsonSerializer.SerializeToElement(value) };

    private static JsonObject Decision(BlockStepResult result) =>
        (JsonObject)JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:decision")].GetRawText())!;

    private static Task<BlockStepResult> Run(FakeJudgeChoice judge, StepContext context, ITextScrubber? scrubber = null) =>
        new JudgeChooseStep(judge, scrubber ?? new PassThroughScrubber()).Run(context, Token).AsTask();

    [Fact]
    public async Task A_decided_choice_is_the_outcome_with_the_decision_as_a_fact()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "api", "model", 0.82, "claude-cli", "d-1", new Dictionary<string, double>(StringComparer.Ordinal) { ["ui"] = 0.1, ["api"] = 0.82 }));

        var result = await Run(judge, Context());

        Assert.Equal("api", result.Result!.Outcome.Value);
        var decision = Decision(result);
        Assert.Equal("api", (string?)decision["choice"]);
        Assert.Equal("model", (string?)decision["reason"]);
        Assert.Equal(0.82, (double?)decision["confidence"]);
        Assert.Equal("self-reported", (string?)decision["confidenceKind"]);
        Assert.Equal("claude-cli", (string?)decision["provider"]);
        Assert.Equal("d-1", (string?)decision["decisionId"]);
        Assert.Equal(0.82, (double?)decision["probabilities"]!["api"]);
    }

    [Fact]
    public async Task A_jev_answer_is_a_probability()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "ui", "model", 0.9, "jev", "d-2"));

        var result = await Run(judge, Context());

        Assert.Equal("probability", (string?)Decision(result)["confidenceKind"]);
    }

    [Theory]
    [InlineData("below-threshold")]
    [InlineData("no-provider-decided")]
    [InlineData("rule-timeout")]
    public async Task An_escalation_is_uncertain_and_the_decision_names_the_reason_and_the_probabilities(string reason)
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, reason, DecisionRef: "d-3", Probabilities: new Dictionary<string, double>(StringComparer.Ordinal) { ["ui"] = 0.4, ["api"] = 0.35 }));

        var result = await Run(judge, Context());

        Assert.Equal("uncertain", result.Result!.Outcome.Value);
        var decision = Decision(result);
        Assert.Equal("uncertain", (string?)decision["choice"]);
        Assert.Equal(reason, (string?)decision["reason"]);
        Assert.Null(decision["confidence"]);
        Assert.Null(decision["provider"]);
        Assert.Equal("d-3", (string?)decision["decisionId"]);
        Assert.Equal(0.4, (double?)decision["probabilities"]!["ui"]);
    }

    [Theory]
    [InlineData("invalid-input")]
    [InlineData("question-disabled")]
    [InlineData("judge choose exited 1: boom")]
    public async Task A_failed_answer_fails_the_Step_with_its_reason(string reason)
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Failed, Choice: null, reason));

        var result = await Run(judge, Context());

        Assert.Null(result.Result);
        Assert.Equal("step.judge.choose: " + reason, result.Failure);
    }

    [Fact]
    public async Task A_choice_outside_the_recipes_choices_fails_the_Step()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Decided, "db", "model", 0.9, "claude-cli", "d-4"));

        var result = await Run(judge, Context());

        Assert.Null(result.Result);
        Assert.Contains("'db', which is not one of the choices", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fact_the_instance_does_not_hold_fails_the_Step_and_the_judge_is_never_asked()
    {
        var judge = new FakeJudgeChoice();
        var context = Context(With("facts", _twoFacts));

        var result = await Run(judge, context);

        Assert.Equal("step.judge.choose needs fact:phasePacket, which the instance does not hold", result.Failure);
        Assert.Empty(judge.Requests);
    }

    [Fact]
    public async Task The_request_carries_the_prompt_the_options_and_the_default_threshold()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold"));

        _ = await Run(judge, Context());

        var request = Assert.Single(judge.Requests);
        Assert.Equal("Which evidence proves this change works?", request.Prompt);
        Assert.Equal<ChooseOption>([new ChooseOption("ui", "what a user sees changed"), new ChooseOption("api", "an HTTP contract changed")], request.Choices);
        Assert.Equal(0.6, request.Threshold);
        Assert.Equal("{\"head\":\"abc\"}", request.Facts["fact:built"]);
    }

    [Fact]
    public async Task The_recipes_threshold_is_passed_on()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold"));

        _ = await Run(judge, Context(With("threshold", 0.8)));

        Assert.Equal(0.8, Assert.Single(judge.Requests).Threshold);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public async Task A_threshold_out_of_range_fails_the_Step(double threshold)
    {
        var judge = new FakeJudgeChoice();

        var result = await Run(judge, Context(With("threshold", threshold)));

        Assert.Contains("must be above 0 and at most 1", result.Failure, StringComparison.Ordinal);
        Assert.Empty(judge.Requests);
    }

    [Fact]
    public async Task The_providers_are_the_runners_resolved_list_as_it_came()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "no-provider-decided"));

        _ = await Run(judge, Context(With("providers", _codexAndRules)));

        Assert.Equal(["codex-cli", "rules"], Assert.Single(judge.Requests).Providers, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Without_a_resolved_list_the_providers_are_the_defaults_without_jev_and_rules_is_last()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "no-provider-decided"));

        _ = await Run(judge, Context());

        var providers = Assert.Single(judge.Requests).Providers;
        Assert.DoesNotContain(JudgeProviders.Jev, providers, StringComparer.Ordinal);
        Assert.Equal(JudgeProviders.Rules, providers[^1]);
    }

    [Fact]
    public async Task Fact_text_is_redacted_and_capped_before_it_leaves()
    {
        var judge = new FakeJudgeChoice().Answers(new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold"));
        var context = Context(arguments: null, ("fact:built", "password is hunter2 " + new string('x', 20_000)));

        _ = await Run(judge, context, new MaskingScrubber());

        var text = Assert.Single(judge.Requests).Facts["fact:built"];
        Assert.StartsWith("password is [redacted] ", text, StringComparison.Ordinal);
        Assert.Equal(JudgeChooseStep.FactTextCap, text.Length);
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("choices")]
    [InlineData("facts")]
    public async Task A_missing_parameter_fails_the_Step(string name)
    {
        var judge = new FakeJudgeChoice();
        var context = Context();
        var without = new StepContext(context.Step, context.Arguments.Where(a => !string.Equals(a.Key, name, StringComparison.Ordinal)).ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal), context.Facts, context.Injection);

        var result = await Run(judge, without);

        Assert.StartsWith("step.judge.choose needs ", result.Failure, StringComparison.Ordinal);
        Assert.Empty(judge.Requests);
    }

    [Fact]
    public async Task A_choice_without_a_criterion_fails_the_Step()
    {
        var judge = new FakeJudgeChoice();

        var result = await Run(judge, Context(With("choices", _badChoices)));

        Assert.Equal("step.judge.choose: the choice 'ui' is not '<outcome>: <criterion>'", result.Failure);
    }
}
