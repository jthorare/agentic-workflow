using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>The ledger payload of 'workflow.question' (issue 2258, PR 9), pinned as a golden: its rows can never be changed once written.</summary>
public sealed class WorkflowQuestionEventGoldenTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_ledger_payload_equals_its_golden()
    {
        var h = new SliceHarness();
        var payload = new WorkflowQuestionPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "Ship the release?", ["yes", "no"], "no", new DateTimeOffset(2026, 3, 7, 14, 0, 0, TimeSpan.Zero), "service:workflow");

        var json = ApiGolden.Reindent(h.Events.Entry(EventTypes.WorkflowQuestion, payload, "key", A.FirstItemId.Value).Envelope.Payload);

        Golden.Match(json, typeof(WorkflowQuestionEventGoldenTests).Assembly, "Golden/ledger/workflow.question.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_catalogued_publicly_under_its_key_shape()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowQuestion);

        Assert.True(entry.Public);
        Assert.Equal(typeof(WorkflowQuestionPublic), entry.PublicPayload);
        Assert.Equal("workflow.question:<run id>:<node id>[:<item index>]", entry.KeyShape);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_invalidates_the_needs_you_view()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowQuestion);

        Expect.Sequence([ApiViews.NeedsYou], entry.Invalidates);
    }
}
