using System.Globalization;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Reads the added lines out of a zero-context unified diff ('git diff --unified=0 --no-renames'): the file from each '+++ b/…' header, the new-file line number from each hunk header.</summary>
internal static class DiffAddedLines
{
    internal static IReadOnlyList<(string Path, int Line, string Text)> Parse(string diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        List<(string Path, int Line, string Text)> added = [];
        string? path = null;
        var line = 0;
        foreach (var raw in diff.Split('\n'))
        {
            var text = raw.TrimEnd('\r');
            if (text.StartsWith("+++ ", StringComparison.Ordinal))
            {
                path = text.StartsWith("+++ b/", StringComparison.Ordinal) ? text[6..] : null;   // '+++ /dev/null' is a deleted file
            }
            else if (text.StartsWith("@@ ", StringComparison.Ordinal))
            {
                line = NewStart(text);
            }
            else if (text.StartsWith('+') && path is not null)
            {
                added.Add((path, line, text[1..]));
                line++;
            }
        }

        return added;
    }

    private static int NewStart(string hunk)
    {
        var plus = hunk.IndexOf(" +", StringComparison.Ordinal);
        if (plus < 0)
        {
            return 0;
        }

        var span = hunk.AsSpan(plus + 2);
        var end = span.IndexOfAny(',', ' ');
        return int.TryParse(end < 0 ? span : span[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 0;
    }
}
