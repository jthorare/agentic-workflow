using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Features.ImportLabels;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.ImportLabels;

// 'judge label import' (cutover spec §5.2): decided decisions in the window become eval items, once each.
public sealed class ImportLabelsTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddDays(20));
    private readonly InMemoryEvalStore _store = new();

    private ImportLabelsService Service() => new(_store, _clock, AwJson.CreateOptions(JudgeJsonContext.Default));

    private void Decided(string id, string question, string answer, string inputJson, int daysAgo = 1) =>
        _store.SeedDecision(new DecidedInput(DecisionId.From(id), question, answer, inputJson), _clock.GetUtcNow().AddDays(-daysAgo));

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_reports_nothing_imported_for_an_empty_decision_log()
    {
        var result = await Service().Handle(A.ImportLabelsRequest().Question("ask-check").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ImportLabelsResponse(Imported: 0, SkippedUnparsable: 0), result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_turns_a_decided_decision_into_an_item_named_after_it()
    {
        Decided("d1", "ask-check", "ask", """{"transcriptTail":"push it"}""");

        var result = await Service().Handle(A.ImportLabelsRequest().Build(), _ct);

        Assert.Equal(new ImportLabelsResponse(1, 0), result.Value);
        var item = Assert.Single(_store.Items);
        Assert.Equal("item-d1", item.Id.Value);
        Assert.Equal("decision:d1", item.Source);
        Assert.Equal("ask-check", item.Question);
        Assert.Equal("ask", item.ModelDecision);
        Assert.Equal(_clock.GetUtcNow(), item.CreatedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_adds_each_decision_once_however_often_it_runs()
    {
        Decided("d1", "ask-check", "ask", """{"transcriptTail":"a"}""");
        var service = Service();

        _ = await service.Handle(A.ImportLabelsRequest().Build(), _ct);
        var second = await service.Handle(A.ImportLabelsRequest().Build(), _ct);

        Assert.Equal(new ImportLabelsResponse(0, 0), second.Value);
        _ = Assert.Single(_store.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_counts_an_input_that_is_not_JSON_and_skips_one_that_does_not_fit_the_question()
    {
        Decided("d-bad", "ask-check", "ask", "not json {");
        Decided("d-shape", "ask-check", "ask", """{"somethingElse":1}""");
        Decided("d-unknown", "no-such-question", "x", """{"transcriptTail":"a"}""");
        Decided("d-ok", "ask-check", "continue", """{"transcriptTail":"a"}""");

        var result = await Service().Handle(A.ImportLabelsRequest().Build(), _ct);

        Assert.Equal(new ImportLabelsResponse(1, 1), result.Value);
        Assert.Equal("item-d-ok", Assert.Single(_store.Items).Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_leaves_out_decisions_before_the_window_and_other_questions()
    {
        Decided("d-new", "ask-check", "ask", """{"transcriptTail":"a"}""", daysAgo: 3);
        Decided("d-old", "ask-check", "ask", """{"transcriptTail":"a"}""", daysAgo: 15);
        Decided("d-wake", "wake-gate", "wake", """{"message":"hi"}""", daysAgo: 3);

        var result = await Service().Handle(A.ImportLabelsRequest().Since(TimeSpan.FromDays(14)).Question("ask-check").Build(), _ct);

        Assert.Equal(new ImportLabelsResponse(1, 0), result.Value);
        Assert.Equal("item-d-new", Assert.Single(_store.Items).Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_fails_when_the_store_cannot_be_used()
    {
        var result = await new ImportLabelsService(new InMemoryEvalStore { Unavailable = true }, _clock, AwJson.CreateOptions(JudgeJsonContext.Default))
            .Handle(A.ImportLabelsRequest().Build(), _ct);

        Assert.True(result.IsError);
    }
}
