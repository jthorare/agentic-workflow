namespace Aw.Sindri.Core.Profile;

/// <summary>The closest known id to a mistyped one, for 'did you mean' messages.</summary>
internal static class Nearest
{
    /// <summary>The known id within a small edit distance of <paramref name="wanted"/> (at most a third of its length, at least 2).</summary>
    /// <param name="wanted">The id that matched nothing.</param>
    /// <param name="known">The ids that exist.</param>
    /// <returns>The nearest, the first in ordinal order on a tie; null when none is close.</returns>
    public static string? Of(string wanted, IEnumerable<string> known)
    {
        var limit = Math.Max(2, wanted.Length / 3);
        return known.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(k => (Id: k, Distance: Distance(wanted, k)))
            .Where(c => c.Distance <= limit)
            .OrderBy(static c => c.Distance)
            .Select(static c => c.Id)
            .FirstOrDefault();
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }
}
