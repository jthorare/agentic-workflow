namespace Aw.Judge.Core.Questions;

/// <summary>
/// The globs of index.denyPaths and the built-in secret file names, matched against a repo path: '/' separates segments,
/// '**' stands for any number of whole segments, '*' for any run of characters inside one segment and '?' for one; a pattern
/// with no '/' matches a file name at any depth. Case is ignored.
/// </summary>
internal static class ModelPickGlob
{
    public static bool IsMatch(string pattern, string path)
    {
        var anywhere = pattern.Contains('/', StringComparison.Ordinal) ? pattern : "**/" + pattern;
        return Segments(anywhere.Split('/'), 0, path.Split('/'), 0);
    }

    private static bool Segments(string[] pattern, int p, string[] path, int s)
    {
        for (; p < pattern.Length; p++, s++)
        {
            if (string.Equals(pattern[p], "**", StringComparison.Ordinal))
            {
                if (p == pattern.Length - 1)
                {
                    return true;
                }

                for (var skip = s; skip <= path.Length; skip++)
                {
                    if (Segments(pattern, p + 1, path, skip))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (s >= path.Length || !Segment(pattern[p], path[s]))
            {
                return false;
            }
        }

        return s == path.Length;
    }

    // '*' and '?' inside one segment, by the usual walk with one place to back up to.
    private static bool Segment(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
