namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Which T3 project a repo belongs to.</summary>
internal static class T3Projects
{
    /// <summary>
    /// The project <paramref name="repoPath"/> belongs to, in this order: one whose folder is the repo or holds it (the deepest); one whose folder is the
    /// main checkout of the repo (it is a linked worktree); one T3 knows as the same repository (another clone: the same primary remote). Null when none.
    /// </summary>
    /// <param name="projects">T3's projects.</param>
    /// <param name="repoPath">The repo folder.</param>
    /// <param name="identity">What git says the repo is.</param>
    /// <returns>The project, or null.</returns>
    public static T3Project? Resolve(IReadOnlyList<T3Project> projects, string repoPath, T3RepoIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(identity);
        return Holding(projects, repoPath)
            ?? (identity.MainCheckout is { } main ? Holding(projects, main) : null)
            ?? (identity.RepositoryKey is { } key ? projects.FirstOrDefault(p => string.Equals(p.RepositoryKey, key, StringComparison.Ordinal)) : null);
    }

    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> are the same folder, links resolved.</summary>
    /// <param name="left">A folder.</param>
    /// <param name="right">Another.</param>
    /// <returns>True when they are.</returns>
    public static bool Same(string left, string right) => string.Equals(Normal(left), Normal(right), StringComparison.Ordinal);

    private static T3Project? Holding(IReadOnlyList<T3Project> projects, string path)
    {
        var target = Normal(path);
        return projects
            .Select(static p => (Project: p, Root: Normal(p.Root)))
            .Where(p => string.Equals(p.Root, target, StringComparison.Ordinal) || target.StartsWith(p.Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(static p => p.Root.Length)
            .Select(static p => p.Project)
            .FirstOrDefault();
    }

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(RealPath.Of(path) ?? Path.GetFullPath(path));   // macOS /tmp is /private/tmp: both sides compare as real paths
}
