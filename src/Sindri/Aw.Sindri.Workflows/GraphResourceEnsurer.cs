using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Workflows;

#pragma warning disable MA0182 // the red commit: the next one wires it into GraphBlockActivities
/// <summary>'EnsureResources' (agent-first PR 14): before a node runs, makes the workflow's declared resources exist and checks their disks.</summary>
/// <param name="provisioner">Creates the files; null on a build with no resources.</param>
/// <param name="workflows">Where the workflow's slug is read from.</param>
internal sealed class GraphResourceEnsurer(IResourceProvisioner? provisioner, IWorkflowStore workflows)
{
    /// <summary>Provisions what <paramref name="spec"/> declares for the workflow.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="spec">The revision's spec.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What the run gets; nothing when the spec declares no resources.</returns>
    public ValueTask<Result<ProvisionedResources>> Ensure(WorkflowId workflow, GraphSpec spec, CancellationToken cancellationToken)
    {
        _ = (provisioner, workflows, workflow, spec, cancellationToken);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
