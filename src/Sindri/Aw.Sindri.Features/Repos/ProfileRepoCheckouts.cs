using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Repos;

/// <summary><see cref="IRepoCheckouts"/> over the profile's repos.</summary>
/// <param name="approvals">The profiles.</param>
internal sealed class ProfileRepoCheckouts(IApprovedProfile approvals) : IRepoCheckouts
{
    /// <inheritdoc/>
    public async ValueTask<Result<string>> Of(RepoName repo, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        return approved.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) is { } found
            ? found.Path
            : SindriErrors.InvalidInput.With($"the repo {repo.Value} is not in the profile, so it has no checkout (fix: add the repo to the profile)");
    }
}
