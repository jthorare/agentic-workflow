namespace Aw.Transcripts.Core;

/// <summary>The pure lookup behind <see cref="WorktreeRoot"/>: which main checkout a gone worktree cwd belongs to.</summary>
public static class WorktreeRoots
{
    /// <summary>The main checkout of the longest root the cwd is at or under (whole '/'-separated segments only).</summary>
    /// <param name="roots">The host's pairs; empty means no mapping.</param>
    /// <param name="cwd">An absolute cwd that no longer exists.</param>
    /// <returns>The main checkout, or null when no root holds the cwd.</returns>
    public static string? MainCheckoutOf(IReadOnlyList<WorktreeRoot> roots, string cwd)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(cwd);
        var path = cwd.TrimEnd('/');
        WorktreeRoot? best = null;
        var bestLength = -1;
        foreach (var pair in roots)
        {
            var root = pair.Root.TrimEnd('/');
            var holds = root.Length > 0
                && (string.Equals(path, root, StringComparison.Ordinal) || path.StartsWith(root + "/", StringComparison.Ordinal));
            if (holds && root.Length > bestLength)
            {
                best = pair;
                bestLength = root.Length;
            }
        }

        return best?.MainCheckout.TrimEnd('/');
    }
}
