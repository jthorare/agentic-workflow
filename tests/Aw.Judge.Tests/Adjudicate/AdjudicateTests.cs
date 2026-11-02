using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.Adjudicate;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Adjudicate;

// 'judge adjudicate <question>': a stronger model labels the unlabelled items, a label only on a 2-of-3 majority.
[Trait("Lane", "unit")]
public sealed class AdjudicateTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(JudgeJsonContext.Default);

    private readonly AdjudicateEvalStore _store = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Adjudicate_refuses_a_question_the_judge_does_not_ask()
    {
        var result = await Service(new ScriptedProvider(ProviderName.ClaudeCli)).Handle(A.AdjudicateRequest().Question("no-such-question").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_two_of_three_majority_in_the_enum_is_stored_as_the_adjudicator_label_at_the_clock()
    {
        var provider = Claude().Decides("ask", 1).Decides("continue", 1).Decides("ask", 1);
        _ = _store.With(Item("item-a", "tail"));

        var result = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(1, 0, 0), result.Value);
        Assert.Equal(("ask", DateTimeOffset.UnixEpoch), _store.Labels[(EvalItemId.From("item-a"), LabelSource.Adjudicator)]);
        Assert.Equal(3, provider.Calls.Count);
    }

    [Fact]
    public async Task Three_different_answers_are_a_split_and_store_nothing()
    {
        var provider = Claude().Decides("ask", 1).Decides("continue", 1).Returns(new ProviderErrored("exit-1"));
        _ = _store.With(Item("item-a", "tail"));

        var result = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(0, 1, 0), result.Value);
        Assert.Empty(_store.Labels);
    }

    [Fact]
    public async Task An_answer_outside_the_enum_never_makes_a_majority()
    {
        var provider = new OffEnumProvider();
        _ = _store.With(Item("item-a", "tail"));

        var result = await new AdjudicateService(_store, new FakeProviderCatalog(provider), new FakeInputRedactor(), _clock, _json).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(0, 1, 0), result.Value);
        Assert.Empty(_store.Labels);
        Assert.Equal(3, provider.Calls);
    }

    [Fact]
    public async Task An_item_whose_three_runs_all_error_is_failed()
    {
        var provider = Claude().Returns(new ProviderUnavailable("timeout"));
        _ = _store.With(Item("item-a", "tail"));

        var result = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(0, 0, 1), result.Value);
        Assert.Empty(_store.Labels);
    }

    [Fact]
    public async Task At_most_limit_items_are_tried_and_an_unlabelled_one_is_not_tried_twice()
    {
        var provider = Claude().Returns(new ProviderErrored("exit-1"));
        _ = _store.With(Item("item-a", "one")).With(Item("item-b", "two")).With(Item("item-c", "three"));

        var result = await Service(provider).Handle(A.AdjudicateRequest().Limit(2).Build(), _ct);

        Assert.Equal(new AdjudicateResponse(0, 0, 2), result.Value);
        Assert.Equal(6, provider.Calls.Count);
    }

    [Fact]
    public async Task Items_the_adjudicator_labelled_or_of_other_questions_are_left_alone()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(Item("item-a", "done")).With(Item("item-b", "other question", question: "rule-check")).With(Item("item-c", "new"));
        _ = await _store.RecordLabel(EvalItemId.From("item-a"), LabelSource.Adjudicator, "ask", _clock.GetUtcNow(), _ct);

        var result = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(1, 0, 0), result.Value);
        Assert.Equal("ask", _store.Labels[(EvalItemId.From("item-c"), LabelSource.Adjudicator)].Label);
        Assert.DoesNotContain(_store.Labels.Keys, key => key.Item == EvalItemId.From("item-b"));
    }

    [Fact]
    public async Task The_model_never_sees_the_decision_being_scored_and_gets_the_options_criteria()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(Item("item-a", "the tail", modelDecision: "JUDGE-SAID-CONTINUE"));

        _ = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        var call = provider.Calls[0];
        Assert.DoesNotContain("JUDGE-SAID-CONTINUE", call.Question.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("JUDGE-SAID-CONTINUE", call.Input?.ToJsonString() ?? "", StringComparison.Ordinal);
        Assert.Contains("the tail", call.Question.Prompt, StringComparison.Ordinal);
        Assert.Contains("Options:\n- continue: the next step was already authorized by the dispatch brief or plan\n- ask: the next step needs the user's input first", call.Question.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_stored_input_is_redacted_before_it_reaches_the_provider()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(Item("item-a", $"deploy with {SampleSecrets.GitHubToken} and {SampleSecrets.AnthropicKey} now"));

        _ = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        var call = provider.Calls[0];
        Assert.DoesNotContain(SampleSecrets.GitHubToken, call.Question.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.AnthropicKey, call.Question.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, call.Input?.ToJsonString() ?? "", StringComparison.Ordinal);
        Assert.Contains(FakeInputRedactor.Placeholder, call.Question.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_input_that_is_not_the_questions_or_an_image_question_is_failed_without_asking()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(new EvalItem(EvalItemId.From("item-a"), "ask-check", "not json", "decision:a", ModelDecision: null, DateTimeOffset.UnixEpoch))
            .With(new EvalItem(EvalItemId.From("item-b"), "ask-check", """{"wrong":1}""", "decision:b", ModelDecision: null, DateTimeOffset.UnixEpoch))
            .With(new EvalItem(EvalItemId.From("item-c"), "visual-critique", """{"afterScreenshot":"after.png","evidenceDir":"/tmp/e"}""", "decision:c", ModelDecision: null, DateTimeOffset.UnixEpoch));

        var ask = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);
        var image = await Service(provider).Handle(A.AdjudicateRequest().Question("visual-critique").Build(), _ct);

        Assert.Equal(new AdjudicateResponse(0, 0, 2), ask.Value);
        Assert.Equal(new AdjudicateResponse(0, 0, 1), image.Value);
        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task Each_run_gets_at_least_a_minute()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(Item("item-a", "tail"));

        _ = await Service(provider).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.All(provider.Calls, call => Assert.Equal(TimeSpan.FromSeconds(60), call.Budget));
    }

    [Fact]
    public async Task A_providers_allowlist_without_claude_is_refused_before_any_item_is_read()
    {
        var provider = Claude().Decides("ask", 1);
        _ = _store.With(Item("item-a", "tail"));

        var result = await Service(provider).Handle(A.AdjudicateRequest().Allowing(ProviderName.Jev).Build(), _ct);

        Assert.Equal(JudgeErrors.ProviderExcluded.Code, result.Error.Code);
        Assert.Empty(provider.Calls);
        Assert.Empty(_store.Labels);
    }

    [Fact]
    public async Task Claude_missing_is_unavailable()
    {
        var result = await new AdjudicateService(_store, new FakeProviderCatalog(), new FakeInputRedactor(), _clock, _json).Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(JudgeErrors.ProviderUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public void A_limit_below_one_is_invalid() =>
        Assert.Equal(JudgeErrors.Invalid.Code, new AdjudicateValidator().Validate(A.AdjudicateRequest().Limit(0).Build()).Error.Code);

    private static ScriptedProvider Claude() => new(ProviderName.ClaudeCli, [.. ContentClasses.All]);

    private AdjudicateService Service(ScriptedProvider provider) =>
        new(_store, new FakeProviderCatalog(provider), new FakeInputRedactor(), _clock, _json);

    private static EvalItem Item(string id, string tail, string question = "ask-check", string? modelDecision = null) =>
        new(EvalItemId.From(id), question, $$"""{"transcriptTail":{{JsonValue.Create(tail).ToJsonString()}}}""", $"decision:{id}", modelDecision, DateTimeOffset.UnixEpoch);
}
