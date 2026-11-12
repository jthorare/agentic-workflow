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
            PollJson.Put(fields, "ref", PollJson.Text(PollJson.Member(payload, "ref")));
            PollJson.Put(fields, "head", PollJson.Text(PollJson.Member(payload, "head")));
        }
        else
        {
            var subject = PollJson.Member(payload, kind.Key.StartsWith("github.pr.", StringComparison.Ordinal) ? "pull_request" : "issue") ?? default;
            PollJson.Put(fields, "number", PollJson.Text(PollJson.Member(subject, "number")));
            PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(subject, "title")));
            PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(subject, "html_url")));
            if (!fields.ContainsKey("url") && fields.TryGetValue("repo", out var repo) && fields.TryGetValue("number", out var number) && kind.Key.StartsWith("github.pr.", StringComparison.Ordinal))
            {
                fields["url"] = $"https://github.com/{repo}/pull/{number}";   // the trimmed feed has only the API url
            }
            PollJson.Put(fields, "author", PollJson.Text(PollJson.Path(subject, "user", "login")));
            PollJson.Put(fields, "reviewer", PollJson.Text(PollJson.Path(payload, "requested_reviewer", "login")));
            PollJson.Put(fields, "label", PollJson.Text(PollJson.Path(payload, "label", "name")));
        }

        return new PolledEvent("github:" + id, kind, at.Value, fields);
    }

    /// <summary>A github.pr.checks event: the checks on an open pull request's head finished red or green (#3201). Not in the events feed; the pull request state poll builds it.</summary>
    /// <param name="pull">The pull request as GET /repos/{r}/pulls lists it.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="conclusion">'red' or 'green'.</param>
    /// <param name="failing">The names of the failing checks (empty when green).</param>
    /// <param name="at">When the last check finished.</param>
    /// <returns>The event, keyed by pull request, head and conclusion.</returns>
    internal static PolledEvent Checks(JsonElement pull, string repo, string conclusion, IReadOnlyList<string> failing, DateTimeOffset at) => throw new NotSupportedException("red: #3201");

    /// <summary>A github.pr.conflict event: an open pull request's mergeable state turned dirty (#3201). Not in the events feed; the pull request state poll builds it.</summary>
    /// <param name="pull">The pull request detail (GET /repos/{r}/pulls/{n}).</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="at">When the pull request last changed.</param>
    /// <returns>The event, keyed by pull request and head.</returns>
    internal static PolledEvent Conflict(JsonElement pull, string repo, DateTimeOffset at) => throw new NotSupportedException("red: #3201");
}
