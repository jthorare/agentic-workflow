namespace Aw.Sindri.Core.Evolve;

/// <summary>A file path a model named, checked before any decision reads it.</summary>
internal static class RepoPath
{
    /// <summary>Normalizes a repo-relative path: drops a leading './' and repeated '/'.</summary>
    /// <param name="path">The path as named.</param>
    /// <returns>The normalized path, or null for an empty, absolute, '..', backslash or otherwise unsafe path.</returns>
    public static string? Normalize(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] == '/' || !path.All(IsAllowed))
        {
            return null;
        }

        var segments = path.Split('/').ToList();
        while (segments.Count > 1 && (string.Equals(segments[0], ".", StringComparison.Ordinal) || segments[0].Length == 0))
        {
            segments.RemoveAt(0);
        }

        if (segments[^1].Length == 0)
        {
            return null;   // a directory, not a file
        }

        _ = segments.RemoveAll(static s => s.Length == 0);
        return segments.Exists(static s => string.Equals(s, ".", StringComparison.Ordinal) || string.Equals(s, "..", StringComparison.Ordinal)) ? null : string.Join('/', segments);
    }

    // why: letters, digits and . _ - / only; a backslash, a colon (C:), '~', white space or a control character fails closed
    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/';
}
