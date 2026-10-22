namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>The paths graphify may name: the snapshot's files, and the root(s) an absolute path is cut at.</summary>
/// <param name="Files">The snapshot's repo-relative paths.</param>
/// <param name="Roots">The snapshot root as given and as resolved.</param>
internal sealed record GraphPaths(IReadOnlySet<string> Files, IReadOnlyList<string> Roots)
{
    /// <summary>Whether the path is one of the snapshot's files.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>True when it is.</returns>
    public bool InSnapshot(string? path) => path is not null && Files.Contains(path);

    /// <summary>A repo-relative path with '/' separators; an absolute one is cut textually at a segment boundary of a root. No filesystem call.</summary>
    /// <param name="path">What graphify wrote.</param>
    /// <returns>The relative path, or null when it is malformed (a backslash, a NUL, a '.' or '..' or empty segment, or an absolute path outside every root).</returns>
    public string? Relative(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal) || path.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        var relative = path;
        if (path.StartsWith('/'))
        {
            var root = Roots.FirstOrDefault(r => path.StartsWith(r.TrimEnd('/') + "/", StringComparison.Ordinal));
            if (root is null)
            {
                return null;
            }

            relative = path[(root.TrimEnd('/').Length + 1)..];
        }

        return relative.Split('/').Any(s => s.Length == 0 || string.Equals(s, "..", StringComparison.Ordinal) || string.Equals(s, ".", StringComparison.Ordinal)) ? null : relative;
    }
}
