using Aw.Kernel;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Resources;

#pragma warning disable MA0182 // the red commit: nothing registers it yet
/// <summary>Plans, creates and checks an owner's resources, and books them in the 'resource' table.</summary>
/// <param name="store">Creates the files.</param>
/// <param name="database">The ledger the bookkeeping rows go in.</param>
/// <param name="clock">The clock for the rows' times.</param>
internal sealed class ResourceProvisioner(ResourceStore store, LedgerDatabase database, TimeProvider clock) : IResourceProvisioner
{
    public ValueTask<Result<ProvisionedResources>> Ensure(ResourceOwner owner, ResourceSpec spec, CancellationToken cancellationToken)
    {
        _ = (store, database, clock);
        throw new NotSupportedException();
    }

    /// <summary>Reads the rows booked for <paramref name="owner"/>.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows, by kind and name.</returns>
    internal ValueTask<Result<IReadOnlyList<ResourceRow>>> Booked(ResourceOwner owner, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
#pragma warning restore MA0182
