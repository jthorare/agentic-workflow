namespace Aw.Sindri.Features.StartSession;

/// <summary>The title a started session shows until its agent names it: the first non-empty line of the start prompt, trimmed and cut to 80 characters (Agents page, issue 3082).</summary>
internal static class StartTitle
{
    internal const int MaxLength = 80;

    /// <summary>The title of <paramref name="prompt"/>, or null when it has no text.</summary>
    /// <param name="prompt">The start prompt.</param>
    /// <returns>The title, or null.</returns>
    internal static string? Of(string? prompt)
    {
        if (prompt is null)
        {
            return null;
        }

        foreach (var raw in prompt.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0)
            {
                return Cut(line);
            }
        }

        return null;
    }

    private static string Cut(string line)
    {
        if (line.Length <= MaxLength)
        {
            return line;
        }

        var length = char.IsHighSurrogate(line[MaxLength - 1]) && char.IsLowSurrogate(line[MaxLength]) ? MaxLength - 1 : MaxLength;
        return line[..length];
    }
}
