using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>The calls behind the default-branch check: which local ref is the default, and whether it holds a commit.</summary>
internal sealed partial class GitCli
{
    private static readonly string[] _defaultRefs = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master"];

    /// <summary>Whether a commit is reachable from the default branch, read from local refs only (no fetch, no working tree).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A full commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the first ref of origin/HEAD, main, master that exists holds the commit; false when it doesn't or none exists; SND-CONTEXT-010 when git can't answer.</returns>
    public async ValueTask<Result<bool>> OnDefaultBranch(string repo, string commit, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        foreach (var name in _defaultRefs)
        {
            var resolved = await Run(repo, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            if (resolved.Value.Exit == 1)
            {
                continue;   // no such ref: try the next
            }

            if (resolved.Value.Exit != 0 || !IsSha(resolved.Value.Stdout.Trim()))
            {
                return Failed("rev-parse", resolved.Value.Exit);
            }

            var run = await Run(repo, ["merge-base", "--is-ancestor", commit, resolved.Value.Stdout.Trim()], cancellationToken).ConfigureAwait(false);
            return run switch
            {
                { IsError: true } => run.Error,
                { Value.Exit: 0 } => true,
                { Value.Exit: 1 } => false,
                _ => Failed("merge-base", run.Value.Exit),
            };
        }

        return false;
    }
}
