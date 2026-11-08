using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>An instance's opaque id is minted from its address (engine spec §5.1, §6.3).</summary>
public sealed class WorkflowInstanceIdOfTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_id_is_minted_from_the_address_and_the_same_address_always_has_the_same_id()
    {
        var id = WorkflowInstanceId.Of(InstanceAddress.From("T-50/step/1"));

        Assert.Matches("^WFI-[0-9a-f]{32}$", id.Value);
        Assert.Equal(id, WorkflowInstanceId.Of(InstanceAddress.From("T-50/step/1")));
        Assert.NotEqual(id, WorkflowInstanceId.Of(InstanceAddress.From("T-50/step/2")));
    }
}
