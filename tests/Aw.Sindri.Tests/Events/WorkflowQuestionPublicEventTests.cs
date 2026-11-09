using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Events;

/// <summary>'workflow.question' and 'workflow.answered' reach a stream client with run, node and kind only (issue 2426): the Needs-you row updates live, and no question, answer or choice text leaves the ledger.</summary>
public sealed class WorkflowQuestionPublicEventTests
{
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public void A_stream_client_gets_the_question_with_run_node_and_kind_only()
    {
        var payload = new WorkflowQuestionPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "SECRET-QUESTION?", ["SECRET-CHOICE-A", "SECRET-CHOICE-B"], "SECRET-DEFAULT", TimeoutAt: null, "service:workflow");

        var shown = Shown(EventTypes.WorkflowQuestion, payload);

        Assert.Equal(["kind", "node", "run"], PropertyNames(shown));
        Assert.Equal("nightly@3:12", shown.Payload.GetProperty("run").GetString());
        Assert.Equal("ask", shown.Payload.GetProperty("node").GetString());
        Assert.Equal("question", shown.Payload.GetProperty("kind").GetString());
        Assert.DoesNotContain("SECRET", shown.Payload.GetRawText(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stream_client_gets_the_answer_with_run_node_and_kind_only()
    {
        var payload = new WorkflowAnsweredPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "SECRET-ANSWER", "human:cli");

        var shown = Shown(EventTypes.WorkflowAnswered, payload);

        Assert.Equal(["kind", "node", "run"], PropertyNames(shown));
        Assert.Equal("question", shown.Payload.GetProperty("kind").GetString());
        Assert.DoesNotContain("SECRET", shown.Payload.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("human:cli", shown.Payload.GetRawText(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Both_events_are_public_with_their_public_records()
    {
        var question = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowQuestion);
        var answered = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowAnswered);

        Assert.True(question.Public);
        Assert.Equal(typeof(WorkflowQuestionPublic), question.PublicPayload);
        Assert.True(answered.Public);
        Assert.Equal(typeof(WorkflowAnsweredPublic), answered.PublicPayload);
    }

    private PublicEvent Shown<T>(string type, T payload)
    {
        var entry = _h.Events.Entry(type, payload, "key", A.FirstItemId.Value);
        var row = new LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);
        return PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value
            ?? throw new InvalidOperationException($"{type} was not sent to a stream client");
    }

    private static string[] PropertyNames(PublicEvent shown) => [.. shown.Payload.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal)];
}
