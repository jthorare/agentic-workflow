using System.Globalization;

namespace Aw.Forge.Infrastructure.Text;

/// <summary>Which new-side lines a unified diff covers: each '@@ -a,b +c,d @@' header covers lines c to c+d-1 (d missing means 1).</summary>
internal static class PatchLines
{
    internal static bool Covers(string? patch, int line)
    {
        if (patch is null)
        {
            return false;
        }

        foreach (var row in patch.Split('\n'))
        {
            if (Hunk(row) is var (start, count) && line >= start && line < start + count)
            {
                return true;
            }
        }

        return false;
    }

    private static (int Start, int Count)? Hunk(string row)
    {
        if (!row.StartsWith("@@ ", StringComparison.Ordinal) || row.IndexOf(" +", 3, StringComparison.Ordinal) is not (>= 0 and var plus))
        {
            return null;
        }

        var range = row[(plus + 2)..];
        range = range[..(range.IndexOf(' ', StringComparison.Ordinal) is var end and >= 0 ? end : range.Length)];
        var parts = range.Split(',');
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            return null;
        }

        var count = 1;
        return parts.Length < 2 || int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) ? (start, count) : null;
    }
}
