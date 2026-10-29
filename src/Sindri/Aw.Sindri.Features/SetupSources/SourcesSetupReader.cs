using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>What the sources step reads: the github and slack sources the approved profile names, and whether each token pointer resolves. A read; no source is called.</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="engine">The context engine, which checks the profile's context section.</param>
/// <param name="secrets">Resolves a token pointer.</param>
internal sealed class SourcesSetupReader(IApprovedProfile approvals, IContextEngine engine, ISecretPointers secrets)
{
    internal ValueTask<Result<SourcesFact>> Read(CancellationToken cancellationToken)
    {
        _ = (approvals, engine, secrets, cancellationToken);
        throw new NotSupportedException();
    }
}
