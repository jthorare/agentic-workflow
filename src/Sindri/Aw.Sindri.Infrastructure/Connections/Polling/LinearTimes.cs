using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>The newest createdAt among a Linear issue, its history entries and its comments: the next cursor.</summary>
internal static class LinearTimes
{
    private static readonly string[] _lists = ["history", "comments"];

    internal static PollCursorText Newest(JsonElement issue, PollCursorText current)
    {
        var best = current.Later(PollJson.Text(PollJson.Member(issue, "createdAt")));
        foreach (var list in _lists)
        {
            var nodes = PollJson.Member(PollJson.Member(issue, list) ?? default, "nodes");
            foreach (var node in nodes is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default)
            {
                best = best.Later(PollJson.Text(PollJson.Member(node, "createdAt")));
            }
        }

        return best;
    }

    internal static bool CommentsHaveMore(JsonElement issue) => HasNextPage(issue, "comments");

    /// <summary>
    /// Whether entries newer than the cursor may be missing from an issue's history page. Linear gives history no filter, so we drop old nodes
    /// ourselves; a next page matters only when the page never reached back to an entry at or before the cursor.
    /// </summary>
    internal static bool HistoryMissing(JsonElement issue, DateTimeOffset cursor, out bool oldestFirst)
    {
        oldestFirst = false;
        if (!HasNextPage(issue, "history"))
        {
            return false;
        }

        var times = HistoryTimes(issue);
        // why: we assume Linear serves history newest first; nobody has checked that live yet, so a page that goes up in time counts as a gap
        oldestFirst = times.Count > 1 && times[0] < times[^1];
        return oldestFirst || times.TrueForAll(time => time > cursor);
    }

    private static List<DateTimeOffset> HistoryTimes(JsonElement issue)
    {
        var times = new List<DateTimeOffset>();
        var nodes = PollJson.Member(PollJson.Member(issue, "history") ?? default, "nodes");
        foreach (var node in nodes is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default)
        {
            if (PollJson.Time(PollJson.Text(PollJson.Member(node, "createdAt"))) is { } at)
            {
                times.Add(at);
            }
        }

        return times;
    }

    private static bool HasNextPage(JsonElement issue, string list)
        => PollJson.Path(PollJson.Member(issue, list) ?? default, "pageInfo", "hasNextPage") is { ValueKind: JsonValueKind.True };
}
