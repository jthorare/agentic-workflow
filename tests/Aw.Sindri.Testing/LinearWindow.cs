using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing;

/// <summary>
/// What Linear does to an issue's nested lists. comments(first: 10) keeps only entries created after the since filter when the query has one.
/// history(first: 25) takes NO filter argument (a query that gives it one is a GraphQL validation error) and is served newest first unless the test
/// says otherwise. Both return at most their limit and say hasNextPage when more were left (or the fixture already said so).
/// </summary>
internal static class LinearWindow
{
    private const int CommentLimit = 10;
    private const int HistoryLimit = 25;

    /// <summary>The answer Linear gives to a history list with a filter argument.</summary>
    internal const string UnknownHistoryFilter = "{\"errors\":[{\"message\":\"Unknown argument \\\"filter\\\" on field \\\"Issue.history\\\"\"}]}";

    internal static bool FiltersHistory(string? query)
        => query is not null && System.Text.RegularExpressions.Regex.IsMatch(query, @"history\([^)]*filter", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal static string Apply(string issueJson, DateTimeOffset? since, bool historyOldestFirst)
    {
        if (JsonNode.Parse(issueJson) is not JsonObject issue)
        {
            return issueJson;
        }

        Window(issue, "comments", CommentLimit, since, newestFirst: false);
        Window(issue, "history", HistoryLimit, since: null, newestFirst: !historyOldestFirst);
        return issue.ToJsonString();
    }

    private static void Window(JsonObject issue, string name, int limit, DateTimeOffset? since, bool newestFirst)
    {
        if (issue[name] is not JsonObject list || list["nodes"] is not JsonArray nodes)
        {
            return;
        }

        var kept = nodes.OfType<JsonObject>().Where(node => since is null || (DateTimeOffset.TryParse((string?)node["createdAt"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at) && at > since)).ToList();
        if (newestFirst)
        {
            kept.Reverse();
        }

        var more = kept.Count > limit || (bool?)list["pageInfo"]?["hasNextPage"] == true;
        var window = new JsonArray();
        foreach (var node in kept.Take(limit))
        {
            window.Add(node.DeepClone());
        }

        list["nodes"] = window;
        list["pageInfo"] = new JsonObject { ["hasNextPage"] = more };
    }
}
