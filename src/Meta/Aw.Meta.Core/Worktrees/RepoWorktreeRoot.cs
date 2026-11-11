using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads and resolves a repo's worktree root for the sweep and the watch (in Worktrees, not Config: Config must not depend on Ports).</summary>
public static class RepoWorktreeRoot
{
    /// <summary>The root for a repo as the sweep needs it: the configured text, resolved; null on any failure, because a sweep never fails on this setting.</summary>
    /// <param name="files">Reads the configured root.</param>
    /// <param name="repo">The team's shared checkout.</param>
    /// <param name="home">The home directory; empty when unknown.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The root, or null when it can't be read or is refused.</returns>
    public static async ValueTask<string?> ResolveOrNull(IWorktreeFiles files, string repo, string home, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var configured = await files.ConfiguredRoot(repo, cancellationToken).ConfigureAwait(false);
        if (configured.IsError)
        {
            return null;
        }

        var resolved = WorktreeRoot.Resolve(configured.Value, repo, home);
        return resolved.IsError ? null : resolved.Value;
    }
}
