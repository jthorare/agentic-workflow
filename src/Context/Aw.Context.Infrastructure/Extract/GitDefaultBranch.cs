using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Whether a commit is on a checkout's default branch, from local refs only: origin/HEAD, else main, else master. Never fetches and never touches the working tree; any git failure is SND-CONTEXT-048.</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitDefaultBranch(GitCli git) : IDefaultBranch
{
    public async ValueTask<Result<bool>> Reaches(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(commit);
        var reached = await git.OnDefaultBranch(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return reached.IsError
            ? ContextErrors.ExtractionUnreadable.With($"git can't read the default branch of {repo.Name.Value} (fix: check the checkout)")
            : reached;
    }
}
