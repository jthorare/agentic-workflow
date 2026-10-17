using System.IO.Enumeration;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Expands the small glob a catalog entry's newest-file fallback uses: '~' for the home, '*' and '?' inside one path segment.</summary>
internal static class GlobFiles
{
    private const int Limit = 2000;

    /// <summary>The files <paramref name="pattern"/> names.</summary>
    /// <param name="pattern">An absolute or '~' glob.</param>
    /// <returns>The existing files, at most 2000.</returns>
    internal static IReadOnlyList<string> Expand(string pattern)
    {
        var expanded = pattern.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), pattern[2..]) : pattern;
        if (!Path.IsPathRooted(expanded))
        {
            return [];
        }

        var current = new List<string> { Path.GetPathRoot(expanded)! };   // why: the path is rooted, so it has a root
        var segments = expanded[current[0].Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var next = new List<string>();
            foreach (var parent in current)
            {
                if (segment.Contains('*', StringComparison.Ordinal) || segment.Contains('?', StringComparison.Ordinal))
                {
                    next.AddRange(Matching(parent, segment));
                }
                else if (File.Exists(Path.Combine(parent, segment)) || Directory.Exists(Path.Combine(parent, segment)))
                {
                    next.Add(Path.Combine(parent, segment));
                }

                if (next.Count > Limit)
                {
                    return [];
                }
            }

            current = next;
        }

        return [.. current.Where(File.Exists)];
    }

    private static List<string> Matching(string directory, string segment)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateFileSystemEntries(directory).Where(entry => FileSystemName.MatchesSimpleExpression(segment, Path.GetFileName(entry))).Take(Limit + 1)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
