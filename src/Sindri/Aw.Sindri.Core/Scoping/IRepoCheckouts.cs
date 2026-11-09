using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scoping;

/// <summary>Where a repo's checkout is, from the profile.</summary>
public interface IRepoCheckouts
{
    /// <summary>The absolute path of the repo's checkout.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The path; an error when no profile yet or the repo is not in it.</returns>
    public ValueTask<Result<string>> Of(RepoName repo, CancellationToken cancellationToken);
}
