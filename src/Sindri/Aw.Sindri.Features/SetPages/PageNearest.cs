namespace Aw.Sindri.Features.SetPages;

/// <summary>The ids closest to a mistyped one, for a "fix: try" hint.</summary>
internal static class PageNearest
{
    private const int NearestShown = 5;

    internal static List<string> Of(string wanted, IEnumerable<string> known) =>
        [.. known.Distinct(StringComparer.Ordinal).Where(k => Distance(wanted, k) <= 3).OrderBy(k => Distance(wanted, k)).ThenBy(static k => k, StringComparer.Ordinal).Take(NearestShown)];

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
