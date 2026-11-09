using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>The ledger payload of 'workflow.notify' (issue 2258, PR 6), pinned as a golden: its rows can never be changed once written.</summary>
public sealed class WorkflowNotifyEventGoldenTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_ledger_payload_equals_its_golden()
    {
        var h = new SliceHarness();
        var payload = new WorkflowNotifyPayload(WorkflowRunId.From("nightly@3:12"), NodeId.From("notify"), "Drifted", "2 contracts changed", "service:workflow");

        var json = ApiGolden.Reindent(h.Events.Entry(EventTypes.WorkflowNotify, payload, "key", A.FirstItemId.Value).Envelope.Payload);

        Golden.Match(json, typeof(WorkflowNotifyEventGoldenTests).Assembly, "Golden/ledger/workflow.notify.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_event_is_catalogued_privately_under_its_key_shape()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowNotify);

        Assert.False(entry.Public);
        Assert.Null(entry.PublicPayload);
        Assert.Equal("workflow.notify:<run id>:<node id>", entry.KeyShape);
    }
}
