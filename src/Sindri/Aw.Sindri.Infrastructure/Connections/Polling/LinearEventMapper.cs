using System.Text.Json;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>Maps the history entries of one Linear issue to catalog events. Text in the fields is provider text: untrusted.</summary>
internal static class LinearEventMapper
{
    internal static IEnumerable<PolledEvent> Map(JsonElement issue, EventKind kind, DateTimeOffset? after)
    {
        if (kind == EventKind.LinearIssueCreated)
        {
            return Created(issue, after);
        }

        return kind == EventKind.LinearCommentAdded ? Comments(issue, after) : History(issue, kind, after);
    }

    private static IEnumerable<PolledEvent> Created(JsonElement issue, DateTimeOffset? after)
    {
        var text = PollJson.Text(PollJson.Member(issue, "createdAt"));
        var identifier = PollJson.Text(PollJson.Member(issue, "identifier"));
        if (identifier is not null && PollJson.Time(text) is { } at && (after is null || at > after))
        {
            yield return new PolledEvent($"linear:issue:{identifier}:created", EventKind.LinearIssueCreated, at, IssueFields(issue));
        }
    }

    private static IEnumerable<PolledEvent> Comments(JsonElement issue, DateTimeOffset? after)
    {
        var nodes = PollJson.Member(PollJson.Member(issue, "comments") ?? default, "nodes");
        foreach (var node in nodes is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray() : default)
        {
            var id = PollJson.Text(PollJson.Member(node, "id"));
            if (id is null || PollJson.Time(PollJson.Text(PollJson.Member(node, "createdAt"))) is not { } at || (after is { } floor && at <= floor))
            {
                continue;
            }

            var fields = IssueFields(issue);
            PollJson.Put(fields, "body", PollJson.Text(PollJson.Member(node, "body")));
            PollJson.Put(fields, "author", PollJson.Text(PollJson.Path(node, "user", "name")));
            yield return new PolledEvent($"linear:comment:{id}", EventKind.LinearCommentAdded, at, fields);
        }
    }

    private static IEnumerable<PolledEvent> History(JsonElement issue, EventKind kind, DateTimeOffset? after)
    {
        var history = PollJson.Member(PollJson.Member(issue, "history") ?? default, "nodes");
        if (history is not { ValueKind: JsonValueKind.Array } nodes)
        {
            yield break;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            var id = PollJson.Text(PollJson.Member(node, "id"));
            var at = PollJson.Time(PollJson.Text(PollJson.Member(node, "createdAt")));
            if (id is null || at is null || (after is { } floor && at <= floor))
            {
                continue;
            }

            foreach (var polled in Events(issue, node, kind, id, at.Value))
            {
                yield return polled;
            }
        }
    }

    private static IEnumerable<PolledEvent> Events(JsonElement issue, JsonElement node, EventKind kind, string historyId, DateTimeOffset at)
    {
        if (kind == EventKind.LinearIssueLabeled
            && PollJson.Member(node, "addedLabels") is { ValueKind: JsonValueKind.Array } labels)
        {
            foreach (var label in labels.EnumerateArray().Select(static l => PollJson.Text(PollJson.Member(l, "name"))).OfType<string>())
            {
                var fields = IssueFields(issue);
                fields["label"] = label;
                yield return new PolledEvent($"linear:{historyId}:label:{label}", kind, at, fields);
            }
        }
        else if (kind == EventKind.LinearIssueStateChanged && PollJson.Text(PollJson.Path(node, "toState", "name")) is { } to)
        {
            var fields = IssueFields(issue);
            fields["to"] = to;
            PollJson.Put(fields, "from", PollJson.Text(PollJson.Path(node, "fromState", "name")));
            yield return new PolledEvent($"linear:{historyId}:state:{to}", kind, at, fields);
        }
    }

    private static Dictionary<string, string> IssueFields(JsonElement issue)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        PollJson.Put(fields, "identifier", PollJson.Text(PollJson.Member(issue, "identifier")));
        PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(issue, "title")));
        PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(issue, "url")));
        return fields;
    }
}
