namespace Aw.Sindri.Core.Profile;

/// <summary>A path glob: '**' matches any number of directories (and a name in them), '*' any run of characters within one segment, '?' one character of a segment. Case-insensitive on macOS (its default volumes are), case-sensitive elsewhere, '/' separated.</summary>
internal static class PathGlob
{
    /// <summary>Whether the glob matches the path.</summary>
    /// <param name="glob">The glob.</param>
    /// <param name="path">The path, relative to the repo root.</param>
    /// <returns>True when it matches.</returns>
    internal static bool Matches(string glob, string path) => Matches(glob, path, OperatingSystem.IsMacOS());

    /// <summary>Whether the glob matches the path, with the case rule given.</summary>
    /// <param name="glob">The glob.</param>
    /// <param name="path">The path, relative to the repo root.</param>
    /// <param name="ignoreCase">True to compare letters without case (macOS).</param>
    /// <returns>True when it matches.</returns>
    internal static bool Matches(string glob, string path, bool ignoreCase) => Segments(glob.Split('/'), 0, path.Split('/'), 0, ignoreCase);

    private static bool Segments(string[] glob, int g, string[] path, int p, bool ic)
    {
        if (g == glob.Length)
        {
            return p == path.Length;
        }

        if (string.Equals(glob[g], "**", StringComparison.Ordinal))
        {
            for (var skip = p; skip <= path.Length; skip++)
            {
                if (Segments(glob, g + 1, path, skip, ic))
                {
                    return true;
                }
            }

            return false;
        }

        return p < path.Length && Within(glob[g], 0, path[p], 0, ic) && Segments(glob, g + 1, path, p + 1, ic);
    }

    private static bool Within(string pattern, int i, string text, int t, bool ic)
    {
        if (i == pattern.Length)
        {
            return t == text.Length;
        }

        return pattern[i] == '*'
            ? Enumerable.Range(t, text.Length - t + 1).Any(next => Within(pattern, i + 1, text, next, ic))
            : t < text.Length && (pattern[i] == '?' || Same(pattern[i], text[t], ic)) && Within(pattern, i + 1, text, t + 1, ic);
    }

    private static bool Same(char a, char b, bool ic) => ic ? char.ToUpperInvariant(a) == char.ToUpperInvariant(b) : a == b;
}
