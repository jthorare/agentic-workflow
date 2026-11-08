using System.Text.Json.Nodes;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The child signal's payload: a signal recorded before it carried the child's park and escalation kind still reads, with no kind.</summary>
public sealed class ChildChangedSignalTests
{
    private static readonly WorkflowInstanceId _child = WorkflowInstanceId.Of(InstanceAddress.From("T-91/t-parent/1/t-child.1"));

    [Fact, Trait("Lane", "unit")]
    public void A_signal_with_the_childs_kinds_reads_them_back()
    {
        var sent = new ChildChanged("service:workflow.inbox", _child, ChildChange.Parked, ParkReason.NeedsAnswer, "q", TriggerKey.For(_child, "child-1"), ParkKind.Wait);

        var read = Read(DataConverter.Default.PayloadConverter.ToPayload(sent));

        Assert.Equal(ParkKind.Wait, read.ParkKind);
        Assert.Null(read.Escalation);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_signal_recorded_before_the_kinds_were_carried_reads_with_none()
    {
        var sent = new ChildChanged("service:workflow.inbox", _child, ChildChange.Parked, ParkReason.NeedsAnswer, "q", TriggerKey.For(_child, "child-1"), ParkKind.Wait, EscalationKind.Gate);
        var json = JsonNode.Parse(DataConverter.Default.PayloadConverter.ToPayload(sent).Data.ToStringUtf8())!.AsObject();
        var recorded = json.Select(static p => p.Key).Where(static k => string.Equals(k, "parkKind", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "escalation", StringComparison.OrdinalIgnoreCase)).ToList();
        recorded.ForEach(k => json.Remove(k));
        var old = new Payload { Data = ByteString.CopyFromUtf8(json.ToJsonString()) };
        old.Metadata["encoding"] = ByteString.CopyFromUtf8("json/plain");

        var read = Read(old);

        Assert.Equal(2, recorded.Count);
        Assert.Null(read.ParkKind);
        Assert.Null(read.Escalation);
        Assert.Equal((ParkReason.NeedsAnswer, "q"), (read.Reason, read.Question));
    }

    private static ChildChanged Read(Payload payload) => DataConverter.Default.PayloadConverter.ToValue<ChildChanged>(payload);
}
