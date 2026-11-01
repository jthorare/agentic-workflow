using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>The sandbox allow-writer (AllowDenial) with a profile check after each allow that succeeded.</summary>
internal sealed class RecheckingProfileSandboxWriter(IProfileSandboxWriter inner, ProfileRecheck recheck) : IProfileSandboxWriter
{
    public async ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken)
    {
        var allowed = await inner.Allow(repo, kind, target, cancellationToken).ConfigureAwait(false);
        if (allowed.IsSuccess)
        {
            await recheck.Now(cancellationToken).ConfigureAwait(false);
        }

        return allowed;
    }
}
