using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The ids of an instance's needs-you rows, the one place their prefixes are spelled. They live here, not on <see cref="NeedsYouRowId"/>, so Core.Ids takes no engine type (ARCH-CYCLE-001).</summary>
public static class NeedsYouRowIds
{
    /// <summary>The 'desk:' row of the instance at <paramref name="address"/>: an open desk task, a root's or a child's.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>The row id.</returns>
    public static NeedsYouRowId Desk(InstanceAddress address) => NeedsYouRowId.From("desk:" + WorkflowInstanceId.Of(address).Value);

    /// <summary>The 'park:' row of the root instance at <paramref name="root"/>: its park or escalation, or its deepest parked descendant.</summary>
    /// <param name="root">The root instance's address.</param>
    /// <returns>The row id.</returns>
    public static NeedsYouRowId Park(InstanceAddress root) => NeedsYouRowId.From("park:" + WorkflowInstanceId.Of(root).Value);

    /// <summary>The 'takeover:' row of the instance at <paramref name="address"/>: a takeover that went idle.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>The row id.</returns>
    public static NeedsYouRowId Takeover(InstanceAddress address) => NeedsYouRowId.From("takeover:" + WorkflowInstanceId.Of(address).Value);
}
