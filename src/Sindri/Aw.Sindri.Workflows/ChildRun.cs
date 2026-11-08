using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A child workflow this instance started (engine P7 plan D1, D10): where it runs, the state its last signal reported, and whether it has ended.</summary>
/// <param name="id">The child's instance id.</param>
/// <param name="address">Its address, which is its workflow id.</param>
/// <param name="started">The start of the child workflow.</param>
internal sealed class ChildRun(WorkflowInstanceId id, InstanceAddress address, Task<ChildWorkflowHandle> started)
{
    public WorkflowInstanceId Id { get; } = id;

    public InstanceAddress Address { get; } = address;

    public Task<ChildWorkflowHandle> Started { get; } = started;

    /// <summary>The state the child's last signal reported: running, parked or escalated.</summary>
    public string State { get; set; } = "running";

    /// <summary>Whether the child ended and its end was delivered.</summary>
    public bool Done { get; set; }
}
