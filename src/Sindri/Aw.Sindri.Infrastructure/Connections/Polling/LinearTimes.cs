using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>The newest createdAt among a Linear issue, its history entries and its comments: the next cursor.</summary>
internal static class LinearTimes
{
    internal static PollCursorText Newest(JsonElement issue, PollCursorText current)
    {
        var best = current.Later(PollJson.Text(PollJson.Member(issue, "createdAt")));
        foreach (var list in new[] { "history", "comments" })
        {
            var nodes = PollJson.Member(PollJson.Member(issue, list) ?? default, "nodes");
            foreach (var node in nodes is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default)
            {
                best = best.Later(PollJson.Text(PollJson.Member(node, "createdAt")));
            }
        }

        return best;
    }
}
