using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>The repo appender (AddRepo) with a profile check after each append that succeeded.</summary>
internal sealed class RecheckingProfileRepoWriter(IProfileRepoWriter inner, ProfileRecheck recheck) : IProfileRepoWriter
{
    public async ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
    {
        var appended = await inner.Append(repo, defaultEffects, cancellationToken).ConfigureAwait(false);
        if (appended.IsSuccess)
        {
            await recheck.Now(cancellationToken).ConfigureAwait(false);
        }

        return appended;
    }
}
