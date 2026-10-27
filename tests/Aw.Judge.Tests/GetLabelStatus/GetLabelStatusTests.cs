using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Features.GetLabelStatus;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.GetLabelStatus;

// 'judge label status': coverage and label agreement per question.
public sealed class GetLabelStatusTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly InMemoryEvalStore _store = new();

    private async Task Item(string id, string question) =>
        _ = await _store.AddItem(new EvalItem(EvalItemId.From(id), question, """{"transcriptTail":"a"}""", "decision:" + id, ModelDecision: null, KnownInstants.Epoch), _ct);

    [Fact, Trait("Lane", "unit")]
    public async Task GetLabelStatus_lists_no_questions_when_no_item_was_imported()
    {
        var result = await new GetLabelStatusService(_store).Handle(A.GetLabelStatusRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Questions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLabelStatus_reports_coverage_and_agreement_for_each_question()
    {
        await Item("item-1", "ask-check");
        await Item("item-2", "ask-check");
        await Item("item-3", "wake-gate");
        _ = await _store.RecordLabel(EvalItemId.From("item-1"), LabelSource.Outcome, "ask", KnownInstants.Epoch, _ct);
        _ = await _store.RecordLabel(EvalItemId.From("item-1"), LabelSource.Adjudicator, "ask", KnownInstants.Epoch, _ct);
        _ = await _store.RecordLabel(EvalItemId.From("item-2"), LabelSource.Override, "skip", KnownInstants.Epoch, _ct);

        var result = await new GetLabelStatusService(_store).Handle(A.GetLabelStatusRequest().Build(), _ct);

        Assert.Equal(
            [
                new QuestionLabelStatus("ask-check", Items: 2, Labeled: 1, Skipped: 1, new LabelAgreement(1, 1)),
                new QuestionLabelStatus("wake-gate", Items: 1, Labeled: 0, Skipped: 0, new LabelAgreement(0, 0)),
            ],
            result.Value.Questions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetLabelStatus_fails_when_the_store_cannot_be_used()
    {
        var result = await new GetLabelStatusService(new InMemoryEvalStore { Unavailable = true }).Handle(A.GetLabelStatusRequest().Build(), _ct);

        Assert.True(result.IsError);
    }
}
