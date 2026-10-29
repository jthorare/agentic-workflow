using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

internal sealed partial class GitCli
{
    /// <summary>Every path some commit in the history of <paramref name="commit"/> deleted, from one 'git log --diff-filter=D'. The caller removes the ones still in the tree.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The distinct paths, '/'-separated, or SND-CONTEXT-010 when git can't read the commit.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Deleted(string repo, string commit, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var listed = await Text(repo, ["log", "--diff-filter=D", "--name-only", "-z", "--format=", commit], cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyList<string>>(text => [.. text.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)]);
    }
}
