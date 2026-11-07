namespace Aw.Sindri.Core.Launch;

/// <summary>What a launched agent Step's environment is scrubbed of and given (line spec LN-LCH1): a Step never holds a forge credential, so it can't push; Sindri pushes.</summary>
public static class LaunchEnvironment
{
    /// <summary>The variable naming the Step's worktree; the pre-push hook the launcher installs refuses only where it is set.</summary>
    public const string Worktree = "AW_SINDRI_WORKTREE";

    /// <summary>The forge credentials a Step must not hold: the GitHub CLI's token, the same for Actions and the API, the Enterprise token, and the forge port's default credential variable, and the default variable of the owner's review posting token (PR review page spec §7.1; a custom 'review.credential: env:NAME' is passed to <see cref="Scrub(IReadOnlyDictionary{string, string}, string, string, IEnumerable{string})"/>).</summary>
    public static IReadOnlyList<string> Credentials { get; } = ["GH_TOKEN", "GITHUB_TOKEN", "GH_ENTERPRISE_TOKEN", "AW_FORGE_TOKEN", "AW_REVIEW_GITHUB_TOKEN"];

    /// <summary>The variable the GitHub CLI reads its config directory from.</summary>
    public const string GhConfigDirectory = "GH_CONFIG_DIR";

    /// <summary>
    /// <paramref name="env"/> scrubbed for a Step: each credential variable is set to the empty string (the host only adds variables to the session it starts,
    /// so a variable can't be removed; an empty token is no token to `gh` and `git`), <see cref="GhConfigDirectory"/> points at an empty directory
    /// and <see cref="Worktree"/> names the Step's worktree.
    /// </summary>
    /// <param name="env">The variables the launch already sets.</param>
    /// <param name="worktree">The Step's worktree (the phase worktree, else its working directory).</param>
    /// <param name="ghConfigDir">An empty directory for the GitHub CLI's config.</param>
    /// <returns>A new dictionary; <paramref name="env"/> is not changed.</returns>
    public static IReadOnlyDictionary<string, string> Scrub(IReadOnlyDictionary<string, string> env, string worktree, string ghConfigDir) =>
        Scrub(env, worktree, ghConfigDir, []);

    /// <summary>Like <see cref="Scrub(IReadOnlyDictionary{string, string}, string, string)"/>, and also empties each variable in <paramref name="extra"/> (the variable a profile's 'review.credential: env:NAME' reads).</summary>
    /// <param name="env">The variables the launch already sets.</param>
    /// <param name="worktree">The Step's worktree (the phase worktree, else its working directory).</param>
    /// <param name="ghConfigDir">An empty directory for the GitHub CLI's config.</param>
    /// <param name="extra">More credential variables to empty.</param>
    /// <returns>A new dictionary; <paramref name="env"/> is not changed.</returns>
    public static IReadOnlyDictionary<string, string> Scrub(IReadOnlyDictionary<string, string> env, string worktree, string ghConfigDir, IEnumerable<string> extra)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(extra);
        Dictionary<string, string> scrubbed = new(env, StringComparer.Ordinal);
        foreach (var name in Credentials.Concat(extra))
        {
            scrubbed[name] = string.Empty;
        }

        scrubbed[GhConfigDirectory] = ghConfigDir;
        scrubbed[Worktree] = worktree;
        return scrubbed;
    }
}
