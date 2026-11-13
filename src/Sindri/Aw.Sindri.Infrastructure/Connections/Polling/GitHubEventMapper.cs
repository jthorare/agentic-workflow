using System.Text.Json;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>Maps one GitHub repo event to the catalog event it is, or null when the catalog has none for it. Text in the fields is provider text: untrusted.</summary>
internal static class GitHubEventMapper
{
    internal static EventKind? KindOf(JsonElement item)
    {
        var payload = PollJson.Member(item, "payload") ?? default;
        var action = PollJson.Text(PollJson.Member(payload, "action"));
        return PollJson.Text(PollJson.Member(item, "type")) switch
        {
            "PullRequestEvent" => action switch
            {
                "opened" => EventKind.GitHubPrOpened,
                "review_requested" => EventKind.GitHubPrReviewRequested,
                "merged" => EventKind.GitHubPrMerged,   // the feed's shape since late 2026 (#3008)
                "closed" when PollJson.Member(PollJson.Member(payload, "pull_request") ?? default, "merged") is { ValueKind: JsonValueKind.True } => EventKind.GitHubPrMerged,
                _ => null,
            },
            "IssuesEvent" => action switch
            {
                "opened" => EventKind.GitHubIssueOpened,
                "labeled" => EventKind.GitHubIssueLabeled,
                _ => null,
            },
            "PushEvent" => EventKind.GitHubPush,
            _ => null,
        };
    }

    internal static PolledEvent? Map(JsonElement item, EventKind kind)
    {
        var id = PollJson.Text(PollJson.Member(item, "id"));
        var at = PollJson.Time(PollJson.Text(PollJson.Member(item, "created_at")));
        if (id is null || at is null)
        {
            return null;
        }

        var payload = PollJson.Member(item, "payload") ?? default;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        PollJson.Put(fields, "repo", PollJson.Text(PollJson.Path(item, "repo", "name")));
        if (kind == EventKind.GitHubPush)
        {
            PutPush(fields, payload);
        }
        else
        {
            PutSubject(fields, payload, kind.Key.StartsWith("github.pr.", StringComparison.Ordinal));
        }

        return new PolledEvent("github:" + id, kind, at.Value, fields);
    }

    private static void PutPush(Dictionary<string, string> fields, JsonElement payload)
    {
        PollJson.Put(fields, "ref", PollJson.Text(PollJson.Member(payload, "ref")));
        PollJson.Put(fields, "head", PollJson.Text(PollJson.Member(payload, "head")));
    }

    private static void PutSubject(Dictionary<string, string> fields, JsonElement payload, bool pullRequest)
    {
        var subject = PollJson.Member(payload, pullRequest ? "pull_request" : "issue") ?? default;
        PollJson.Put(fields, "number", PollJson.Text(PollJson.Member(subject, "number")));
        PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(subject, "title")));
        PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(subject, "html_url")));
        if (pullRequest)
        {
            PutPullUrl(fields);
        }

        PollJson.Put(fields, "author", PollJson.Text(PollJson.Path(subject, "user", "login")));
        PollJson.Put(fields, "reviewer", PollJson.Text(PollJson.Path(payload, "requested_reviewer", "login")));
        PollJson.Put(fields, "label", PollJson.Text(PollJson.Path(payload, "label", "name")));
    }

    // The trimmed feed has only the API url: build the page url from the repo and the number.
    private static void PutPullUrl(Dictionary<string, string> fields)
    {
        if (!fields.ContainsKey("url") && fields.TryGetValue("repo", out var repo) && fields.TryGetValue("number", out var number))
        {
            fields["url"] = $"https://github.com/{repo}/pull/{number}";
        }
    }
}
