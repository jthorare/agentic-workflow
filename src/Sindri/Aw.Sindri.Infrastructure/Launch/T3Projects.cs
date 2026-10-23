namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Which T3 project a repo belongs to.</summary>
internal static class T3Projects
{
    /// <summary>The project whose folder is <paramref name="repoPath"/> or contains it, the deepest one when several do; null when none does.</summary>
    /// <param name="projects">T3's projects.</param>
    /// <param name="repoPath">The repo checkout.</param>
    /// <returns>The project id, or null.</returns>
    public static string? Containing(IEnumerable<T3Wire.Project> projects, string repoPath)
    {
        var target = Normal(repoPath);
        return projects
            .Select(static p => (p.Id, Root: Normal(p.WorkspaceRoot)))
            .Where(p => string.Equals(p.Root, target, StringComparison.Ordinal) || target.StartsWith(p.Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(static p => p.Root.Length)
            .Select(static p => p.Id)
            .FirstOrDefault();
    }

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(RealPath.Of(path) ?? Path.GetFullPath(path));   // macOS /tmp is /private/tmp: both sides compare as real paths
}
