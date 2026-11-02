using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Features.SetLabel;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.SetLabel;

// 'judge label set <itemId> <label|skip>': a manual override for one item.
public sealed class SetLabelTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemoryEvalStore _store = new();

    private async Task Item(string id, string question = "ask-check") =>
        _ = await _store.AddItem(new EvalItem(EvalItemId.From(id), question, """{"transcriptTail":"a"}""", "decision:" + id, "ask", _clock.GetUtcNow()), _ct);

    [Fact, Trait("Lane", "unit")]
    public async Task SetLabel_refuses_an_item_that_does_not_exist_with_AW_JUDGE_013()
    {
        var result = await new SetLabelService(_store, _clock).Handle(A.SetLabelRequest().Item("no-such-item").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.UnknownItem.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetLabel_stores_one_of_the_questions_outputs_as_an_override()
    {
        await Item("item-d1");

        var result = await new SetLabelService(_store, _clock).Handle(A.SetLabelRequest().Item("item-d1").Label("ask").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new SetLabelResponse(EvalItemId.From("item-d1"), "ask"), result.Value);
        var labeled = Assert.Single((await _store.Labeled("ask-check", source: null, _ct)).Value);
        Assert.Equal(LabelSource.Override, labeled.Source);
        Assert.Equal("ask", labeled.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetLabel_accepts_skip_and_the_item_then_scores_nowhere()
    {
        await Item("item-d1");

        var result = await new SetLabelService(_store, _clock).Handle(A.SetLabelRequest().Item("item-d1").Label("skip").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty((await _store.Labeled("ask-check", source: null, _ct)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetLabel_refuses_a_label_the_question_cannot_answer_and_stores_nothing()
    {
        await Item("item-d1");

        var result = await new SetLabelService(_store, _clock).Handle(A.SetLabelRequest().Item("item-d1").Label("wake").Build(), _ct);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("continue, ask, skip", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty((await _store.Labeled("ask-check", source: null, _ct)).Value);
    }
}
