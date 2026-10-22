namespace Aw.Sindri.Core.Launch;

/// <summary>The paths an agent Step may neither read nor edit (line spec LN-LCH1): the other runs' and phases' worktrees beside its own, and the holdout store.</summary>
public static class StepDenials
{
    /// <summary>The Sindri-home directory the holdout store keeps its sets under.</summary>
    public const string HoldoutDirectory = "holdout";

    /// <summary>The denied paths for a Step working in <paramref name="worktree"/>.</summary>
    /// <param name="worktree">The Step's own worktree, which is never denied.</param>
    /// <param name="siblingWorktrees">The Sindri worktrees found beside it; the Step's own is dropped from them.</param>
    /// <param name="sindriHome">Sindri's home, or null when it is not known (no holdout path is then denied).</param>
    /// <returns>Absolute paths, the siblings first, no duplicates.</returns>
    public static IReadOnlyList<string> For(string worktree, IReadOnlyList<string> siblingWorktrees, string? sindriHome)
    {
        ArgumentNullException.ThrowIfNull(siblingWorktrees);
        var own = worktree.TrimEnd('/');
        List<string> denied = [.. siblingWorktrees.Where(w => !string.Equals(w.TrimEnd('/'), own, StringComparison.Ordinal))];
        if (sindriHome is not null)
        {
            denied.Add(sindriHome.TrimEnd('/') + "/" + HoldoutDirectory);
        }

        return [.. denied.Distinct(StringComparer.Ordinal)];
    }
}
