using System.Globalization;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>The multi-repo cursor as text: one '&lt;owner/name&gt;|&lt;updated ticks&gt;|&lt;id,id,...&gt;' part per repo, joined by ';'. A repo with no part has not been read yet.</summary>
internal static class GitHubCursors
{
    internal static string Format(IReadOnlyDictionary<string, GitHubCursor> cursors) =>
        string.Join(';', cursors.OrderBy(static c => c.Key, StringComparer.Ordinal).Select(static c =>
            string.Create(CultureInfo.InvariantCulture, $"{c.Key}|{c.Value.UpdatedAt.UtcTicks}|{string.Join(',', c.Value.SeenIds)}")));

    /// <summary>Reads a cursor; a part that does not parse is dropped, so its repo starts from the window again.</summary>
    internal static Dictionary<string, GitHubCursor> Parse(string? text)
    {
        var cursors = new Dictionary<string, GitHubCursor>(StringComparer.Ordinal);
        foreach (var part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split('|');
            if (fields.Length == 3
                && long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks is >= 0 and <= 3_155_378_975_999_999_999L)
            {
                var ids = new List<long>();
                var parts = fields[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (Array.TrueForAll(parts, part => long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && Add(ids, id)))
                {
                    cursors[fields[0]] = new GitHubCursor(new DateTimeOffset(ticks, TimeSpan.Zero), ids);
                }
            }
        }

        return cursors;
    }

    private static bool Add(List<long> ids, long id)
    {
        ids.Add(id);
        return true;
    }
}
