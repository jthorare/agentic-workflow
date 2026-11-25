using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>The ledger payload of 'workflow.answered' (issue 2258, PR 9), pinned as a golden: its rows can never be changed once written.</summary>
public sealed class WorkflowAnsweredEventGoldenTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_ledger_payload_equals_its_golden()
    {
        var h = new SliceHarness();
        var payload = new WorkflowAnsweredPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("ask"), "yes", "session:LCH-00000000000000000000000000000001");

        var json = ApiGolden.Reindent(h.Events.Entry(EventTypes.WorkflowAnswered, payload, "key", A.FirstItemId.Value).Envelope.Payload);

        Golden.Match(json, typeof(WorkflowAnsweredEventGoldenTests).Assembly, "Golden/ledger/workflow.answered.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_catalogued_publicly_under_its_key_shape()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowAnswered);

        Assert.True(entry.Public);
        Assert.Equal(typeof(WorkflowAnsweredPublic), entry.PublicPayload);
        Assert.Equal("workflow.answered:<run id>:<node id>[:<item index>]", entry.KeyShape);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_invalidates_the_needs_you_view()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowAnswered);

        Expect.Sequence([ApiViews.NeedsYou], entry.Invalidates);
    }
}
