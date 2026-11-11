using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Config;

/// <summary>The one worktree root of a repo, the same rule as skills/_shared/worktree-path.sh (issue #2765).</summary>
public static class WorktreeRoot
{
    /// <summary>Resolves the root: the configured text, else &lt;home&gt;/repos/aw-wt/&lt;repo name&gt;; "~" is the home, a relative root is relative to the repo.</summary>
    /// <param name="configured">$AW_WORKTREE_ROOT or the file's "root"; null or empty means the default.</param>
    /// <param name="repo">The team's shared checkout.</param>
    /// <param name="home">The home directory; empty when unknown.</param>
    /// <returns>The normalized absolute root, or an error when it is inside the repo or the home is needed and unknown.</returns>
    public static Result<string> Resolve(string? configured, string repo, string home)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(home);
        var repoPath = WorktreeConfig.NormalizePath(repo);
        var root = Expand(configured, repoPath, home);
        if (root is null)
        {
            return MetaErrors.WorktreeHomeUnknown.With("the worktree root " + (string.IsNullOrEmpty(configured) ? "default" : configured) + " needs the home directory");
        }

        root = WorktreeConfig.NormalizePath(root);
        return root == repoPath || root.StartsWith(repoPath.TrimEnd('/') + "/", StringComparison.Ordinal)
            ? MetaErrors.WorktreeRootInRepo.With("root " + root + " is inside the repo " + repoPath)
            : root;
    }

    // The absolute root before normalizing; null when it needs a home directory that is unknown.
    private static string? Expand(string? configured, string repoPath, string home)
    {
        if (string.IsNullOrEmpty(configured))
        {
            return home.Length == 0 ? null : home + "/repos/aw-wt/" + repoPath[(repoPath.LastIndexOf('/') + 1)..];
        }

        if (configured == "~" || configured.StartsWith("~/", StringComparison.Ordinal))
        {
            return home.Length == 0 ? null : home + configured[1..];
        }

        return configured.StartsWith('/') ? configured : repoPath + "/" + configured;
    }

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

        var resolved = Resolve(configured.Value, repo, home);
        return resolved.IsError ? null : resolved.Value;
    }
}
