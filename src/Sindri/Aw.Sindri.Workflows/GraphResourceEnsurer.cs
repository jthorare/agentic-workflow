using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Workflows;

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
    public async ValueTask<Result<ProvisionedResources>> Ensure(WorkflowId workflow, GraphSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (provisioner is null || spec.Resources is null)
        {
            return ProvisionedResources.None;
        }

        var record = await workflows.Get(workflow, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        var owner = ResourceOwner.Parse($"workflow:{record.Value.Slug.Value}");
        return owner.IsError ? owner.Error : await provisioner.Ensure(owner.Value, GraphResourceSpec.From(spec.Resources), cancellationToken).ConfigureAwait(false);
    }
}
