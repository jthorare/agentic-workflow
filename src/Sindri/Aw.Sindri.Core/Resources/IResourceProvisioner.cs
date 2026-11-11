using Aw.Kernel;

namespace Aw.Sindri.Core.Resources;

/// <summary>Makes an owner's declared resources exist before a run: plans them, creates the files and checks each disk against its size.</summary>
public interface IResourceProvisioner
{
    /// <summary>Provisions <paramref name="spec"/> for <paramref name="owner"/>. Calling it again changes no data.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="spec">What it declares.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What the run gets; SND-RES-001 with the bytes used and the limit when a disk is over its size; the planner's and the store's errors otherwise.</returns>
    public ValueTask<Result<ProvisionedResources>> Ensure(ResourceOwner owner, ResourceSpec spec, CancellationToken cancellationToken);
}
