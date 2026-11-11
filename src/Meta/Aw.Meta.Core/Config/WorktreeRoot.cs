using Aw.Kernel;

namespace Aw.Meta.Core.Config;

/// <summary>The one worktree root of a repo, the same rule as skills/_shared/worktree-path.sh (issue #2765).</summary>
public static class WorktreeRoot
{
    /// <summary>Resolves the root: the configured text, else &lt;home&gt;/repos/aw-wt/&lt;repo name&gt;; "~" is the home, a relative root is relative to the repo.</summary>
    /// <param name="configured">$AW_WORKTREE_ROOT or the file's "root"; null or empty means the default.</param>
    /// <param name="repo">The team's shared checkout.</param>
    /// <param name="home">The home directory; empty when unknown.</param>
    /// <returns>The normalized absolute root, or an error when it is inside the repo or the home is needed and unknown.</returns>
    public static Result<string> Resolve(string? configured, string repo, string home) => throw new NotSupportedException();
}
