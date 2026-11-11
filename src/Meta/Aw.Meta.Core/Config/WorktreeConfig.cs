namespace Aw.Meta.Core.Config;

/// <summary>How the worktree sweep behaves; every field is optional.</summary>
/// <param name="Roots">Directories whose worktrees may be removed; null means the defaults (~/repos/aw-wt, the repo's .claude/worktrees, and the repo's worktree root when it is set elsewhere). The gate directory always counts.</param>
/// <param name="BuildOutputs">Directory names whose untracked files don't keep a worktree; null means bin, obj, artifacts, node_modules, dist.</param>
/// <param name="Enabled">Whether the triggers (merge-run, the watch) remove worktrees; false makes them only report. The explicit worktree-sweep always removes.</param>
public sealed record WorktreeConfig(IReadOnlyList<string>? Roots = null, IReadOnlyList<string>? BuildOutputs = null, bool Enabled = true)
{
    /// <summary>The build output directory names when the config gives none.</summary>
    public static IReadOnlyList<string> DefaultBuildOutputs { get; } = ["bin", "obj", "artifacts", "node_modules", "dist"];

    /// <summary>The build output directory names in force.</summary>
    public IReadOnlyList<string> EffectiveBuildOutputs => BuildOutputs ?? DefaultBuildOutputs;

    /// <summary>The roots in force: the configured ones (or the defaults), with "~" expanded, and the gate directory.</summary>
    /// <param name="repo">The team's shared checkout.</param>
    /// <param name="gatesDirectory">Where the gate trees live.</param>
    /// <param name="home">The home directory; empty when unknown (a "~" root is then left out).</param>
    /// <param name="repoRoot">The repo's resolved worktree root (<see cref="WorktreeRoot"/>), or null when unknown; added to the defaults, ignored when <see cref="Roots"/> is set.</param>
    /// <returns>Absolute directories.</returns>
    public IReadOnlyList<string> EffectiveRoots(string repo, string gatesDirectory, string home, string? repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(gatesDirectory);
        ArgumentNullException.ThrowIfNull(home);
        string[] defaults = repoRoot is { Length: > 0 } ? ["~/repos/aw-wt", repo + "/.claude/worktrees", repoRoot] : ["~/repos/aw-wt", repo + "/.claude/worktrees"];
        var configured = Roots ?? defaults;
        var roots = new List<string>();
        foreach (var root in configured)
        {
            var expanded = root == "~" || root.StartsWith("~/", StringComparison.Ordinal) ? (home.Length > 0 ? home + root[1..] : null) : root;
            if (expanded is not null && NormalizePath(expanded) is { Length: > 1 } normalized && normalized.StartsWith('/'))
            {
                roots.Add(normalized);
            }
        }

        roots.Add(NormalizePath(gatesDirectory));
        return [.. roots.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Resolves "." and ".." segments and drops trailing slashes of an absolute path; any other text comes back trimmed.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path.</returns>
    internal static string NormalizePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.StartsWith('/'))
        {
            return path.Trim();
        }

        List<string> kept = [];
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (kept.Count > 0)
                {
                    kept.RemoveAt(kept.Count - 1);
                }
            }
            else if (segment != ".")
            {
                kept.Add(segment);
            }
        }

        return "/" + string.Join('/', kept);
    }
}
