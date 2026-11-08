using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The instance's opaque id ('WFI-…') from its address, which is its workflow id (engine spec §5.1): the same address
/// always gives the same id, so the interpreter and a client outside the workflow name the same trigger keys.
/// </summary>
internal static class InstanceIds
{
    /// <summary>
    /// The 'open-request' Update's id (UI spec E-6, spec §5.2): its trigger key, 'wf:{instance}:open:{launch}', so a
    /// repeat for the same launch is deduplicated by Temporal and the ledger key is the same.
    /// </summary>
    /// <param name="address">The instance.</param>
    /// <param name="request">The open-request.</param>
    /// <returns>The update id.</returns>
    public static string OpenRequestUpdate(InstanceAddress address, OpenRequested request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.KeyFor(WorkflowInstanceId.Of(address)).Value;
    }
}
