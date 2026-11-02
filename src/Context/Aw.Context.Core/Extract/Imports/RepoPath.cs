namespace Aw.Context.Core.Extract.Imports;

/// <summary>Repo-relative path arithmetic with forward slashes.</summary>
internal static class RepoPath
{
    internal static string Dir(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    internal static bool InDependencyFolder(string path) =>
        path.StartsWith("node_modules/", StringComparison.Ordinal) || path.Contains("/node_modules/", StringComparison.Ordinal);

    internal static bool Within(string dir, string path) =>
        dir.Length == 0 || path.StartsWith(dir + "/", StringComparison.Ordinal);

    /// <summary>Joins <paramref name="relative"/> onto <paramref name="dir"/>, folding '.' and '..'.</summary>
    /// <returns>The path, or null when it climbs above the repo root.</returns>
    internal static string? Join(string dir, string relative)
    {
        var parts = new List<string>(dir.Length == 0 ? [] : dir.Split('/'));
        foreach (var segment in relative.Split('/'))
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (segment != "..")
            {
                parts.Add(segment);
            }
            else if (parts.Count == 0)
            {
                return null;
            }
            else
            {
                parts.RemoveAt(parts.Count - 1);
            }
        }

        return string.Join('/', parts);
    }
}
