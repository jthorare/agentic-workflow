using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>The ledger payloads of 'workflow.node.taken-over' and 'workflow.node.handed-back' (#2497), pinned as goldens (their rows can never be changed once written), and their catalog rows.</summary>
public sealed class WorkflowNodeControlEventGoldenTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_taken_over_payload_equals_its_golden()
    {
        var h = new SliceHarness();
        var payload = new WorkflowNodeTakenOverPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("agent"), 2, "human:cli");

        var json = ApiGolden.Reindent(h.Events.Entry(EventTypes.WorkflowNodeTakenOver, payload, "key", A.FirstItemId.Value).Envelope.Payload);

        Golden.Match(json, typeof(WorkflowNodeControlEventGoldenTests).Assembly, "Golden/ledger/workflow.node.taken-over.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_handed_back_payload_equals_its_golden()
    {
        var h = new SliceHarness();
        var payload = new WorkflowNodeHandedBackPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("agent"), 2, "session:launch:LCH-00000000000000000000000000000001");

        var json = ApiGolden.Reindent(h.Events.Entry(EventTypes.WorkflowNodeHandedBack, payload, "key", A.FirstItemId.Value).Envelope.Payload);

        Golden.Match(json, typeof(WorkflowNodeControlEventGoldenTests).Assembly, "Golden/ledger/workflow.node.handed-back.json", Normalization.None);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(EventTypes.WorkflowNodeTakenOver, "workflow.node.taken-over:<run id>:<node id>:<item index>:<command id>")]
    [InlineData(EventTypes.WorkflowNodeHandedBack, "workflow.node.handed-back:<run id>:<node id>:<item index>:<command id>")]
    public void The_events_are_catalogued_with_their_key_shape_and_refresh_the_timeline(string type, string keyShape)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.False(entry.Public);
        Assert.Equal(keyShape, entry.KeyShape);
        Expect.Sequence([ApiViews.Timeline], entry.Invalidates);
    }
}
