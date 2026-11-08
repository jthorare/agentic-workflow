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

    internal static bool HasMore(JsonElement issue)
        => _lists.Any(list => PollJson.Path(PollJson.Member(issue, list) ?? default, "pageInfo", "hasNextPage") is { ValueKind: JsonValueKind.True });
}
