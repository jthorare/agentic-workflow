using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// Maps GitHub answers to catalog events: a repo events feed item to the kind it is (or null when the catalog has none for it), and an open pull
/// request's state to github.pr.checks or github.pr.conflict, which the feed doesn't carry. Every event holds exactly its kind's fields (#3201, engine's
/// lists): a field the answer lacks is the empty string, never a missing key. Text in the fields is provider text: untrusted.
/// </summary>
internal static class GitHubEventMapper
{
    private static readonly string[] _prBase = ["repo", "number", "title", "author", "url", "headSha", "baseRef"];
    private static readonly string[] _issue = ["repo", "number", "title", "url", "author"];

    // The fields each kind holds besides the trigger's own deliveryId, occurredAt, connection and event.
    private static readonly FrozenDictionary<string, string[]> _fields = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [EventKind.GitHubPrOpened.Key] = [.. _prBase, "draft"],
        [EventKind.GitHubPrUpdated.Key] = [.. _prBase, "draft"],
        [EventKind.GitHubPrReadyForReview.Key] = [.. _prBase, "draft"],
        [EventKind.GitHubPrClosed.Key] = [.. _prBase, "draft"],
        [EventKind.GitHubPrMerged.Key] = [.. _prBase, "draft"],
        [EventKind.GitHubPrReviewRequested.Key] = [.. _prBase, "reviewer"],
        [EventKind.GitHubPrReviewSubmitted.Key] = [.. _prBase, "reviewer", "state"],
        [EventKind.GitHubPrComment.Key] = [.. _prBase, "commenter", "body", "threadId"],
        [EventKind.GitHubPrChecks.Key] = [.. _prBase, "conclusion", "failing"],
        [EventKind.GitHubPrConflict.Key] = _prBase,
        [EventKind.GitHubIssueOpened.Key] = _issue,
        [EventKind.GitHubIssueLabeled.Key] = [.. _issue, "label"],
        [EventKind.GitHubPush.Key] = ["repo", "ref", "headSha", "pusher"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // why: the REST feed sends 'approved', GraphQL 'APPROVED'; the payload always holds the lowercase form
    private static readonly FrozenDictionary<string, string> _reviewStates = new[] { "approved", "changes_requested", "commented" }.ToFrozenDictionary(static s => s, static s => s, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the mapper knows a kind (the poller takes these even before the catalog lists them).</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True for a GitHub kind the mapper builds.</returns>
    internal static bool Knows(EventKind kind) => _fields.ContainsKey(kind.Key);

    /// <summary>Whether a kind comes from the open pull requests' state instead of the events feed.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>True for github.pr.checks and github.pr.conflict.</returns>
    internal static bool FromPullState(EventKind kind) => kind == EventKind.GitHubPrChecks || kind == EventKind.GitHubPrConflict;

    internal static EventKind? KindOf(JsonElement item)
    {
        var payload = PollJson.Member(item, "payload") ?? default;
        var action = PollJson.Text(PollJson.Member(payload, "action"));
        return PollJson.Text(PollJson.Member(item, "type")) switch
        {
            "PullRequestEvent" => PullRequestKind(payload, action),
            "PullRequestReviewEvent" => ReviewKind(payload),
            "PullRequestReviewCommentEvent" or "IssueCommentEvent" => CommentKind(payload, action),
            "IssuesEvent" => IssueKind(action),
            "PushEvent" => EventKind.GitHubPush,
            _ => null,
        };
    }

    internal static PolledEvent? Map(JsonElement item, EventKind kind)
    {
        var id = PollJson.Text(PollJson.Member(item, "id"));
        var at = PollJson.Time(PollJson.Text(PollJson.Member(item, "created_at")));
        if (id is null || at is null || !Knows(kind))
        {
            return null;
        }

        var payload = PollJson.Member(item, "payload") ?? default;
        var fields = Blank(kind);
        var repo = PollJson.Text(PollJson.Path(item, "repo", "name"));
        PollJson.Put(fields, "repo", repo);
        if (kind == EventKind.GitHubPush)
        {
            PushFields(fields, item, payload);
        }
        else if (kind.Key.StartsWith("github.issue.", StringComparison.Ordinal))
        {
            IssueFields(fields, payload);
        }
        else
        {
            PullFields(fields, PullOf(payload, kind), repo);
            PullExtras(fields, payload);
        }

        return new PolledEvent("github:" + id, kind, at.Value, fields);
    }

    /// <summary>A github.pr.checks event: the checks on an open pull request's head finished red or green (#3201). Not in the events feed; the pull request state poll builds it.</summary>
    /// <param name="pull">The pull request as GET /repos/{r}/pulls lists it.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="outcome">How the head's check runs ended: conclusion, failing names, when the last finished and the highest run id.</param>
    /// <returns>The event, keyed by pull request, head, conclusion and run id (a re-run that ends in the same state is a new occurrence).</returns>
    internal static PolledEvent Checks(JsonElement pull, string repo, CheckRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var fields = Blank(EventKind.GitHubPrChecks);
        fields["repo"] = repo;
        PullFields(fields, pull, repo);
        fields["conclusion"] = outcome.Conclusion;
        fields["failing"] = new JsonArray([.. outcome.Failing.Select(static f => (JsonNode?)JsonValue.Create(f))]).ToJsonString();
        return new PolledEvent($"github:{repo}#{fields["number"]}:checks:{fields["headSha"]}:{outcome.Conclusion}:{outcome.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture)}", EventKind.GitHubPrChecks, outcome.At, fields);
    }

    /// <summary>A github.pr.conflict event: an open pull request's mergeable state turned dirty (#3201). Not in the events feed; the pull request state poll builds it.</summary>
    /// <param name="pull">The pull request detail (GET /repos/{r}/pulls/{n}).</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="at">When the pull request last changed.</param>
    /// <param name="occurrence">How many times this pull request has turned dirty (1 for the first), so a second conflict is a new event.</param>
    /// <returns>The event, keyed by pull request, head and occurrence.</returns>
    internal static PolledEvent Conflict(JsonElement pull, string repo, DateTimeOffset at, int occurrence)
    {
        var fields = Blank(EventKind.GitHubPrConflict);
        fields["repo"] = repo;
        PullFields(fields, pull, repo);
        return new PolledEvent($"github:{repo}#{fields["number"]}:conflict:{fields["headSha"]}:{occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)}", EventKind.GitHubPrConflict, at, fields);
    }

    /// <summary>Fills the fields a trimmed feed item left empty from the pull request detail (GET /repos/{r}/pulls/{n}); a field that has a value keeps it.</summary>
    /// <param name="polled">The event as the feed had it.</param>
    /// <param name="pull">The detail.</param>
    /// <returns>The event with the empty fields filled.</returns>
    internal static PolledEvent Enriched(PolledEvent polled, JsonElement pull)
    {
        ArgumentNullException.ThrowIfNull(polled);
        var detail = polled.Fields.Keys.ToDictionary(static k => k, static _ => string.Empty, StringComparer.Ordinal);   // why: Set fills only the keys the kind holds
        PullFields(detail, pull, polled.Fields.GetValueOrDefault("repo"));
        var fields = new Dictionary<string, string>(polled.Fields, StringComparer.Ordinal);
        foreach (var (name, value) in detail)
        {
            if (fields.TryGetValue(name, out var had) && had.Length == 0)
            {
                fields[name] = value;
            }
        }

        return polled with { Fields = fields };
    }

    /// <summary>Whether a pull request event still lacks the title the detail read fills.</summary>
    /// <param name="polled">The event.</param>
    /// <returns>True when its title is empty.</returns>
    internal static bool NeedsDetail(PolledEvent polled) =>
        polled.Kind.Key.StartsWith("github.pr.", StringComparison.Ordinal) && polled.Fields.TryGetValue("title", out var title) && title.Length == 0;

    private static EventKind? PullRequestKind(JsonElement payload, string? action) => action switch
    {
        "opened" => EventKind.GitHubPrOpened,
        "synchronize" => EventKind.GitHubPrUpdated,
        "ready_for_review" => EventKind.GitHubPrReadyForReview,
        "review_requested" => EventKind.GitHubPrReviewRequested,
        "merged" => EventKind.GitHubPrMerged,   // the feed's shape since late 2026 (#3008)
        "closed" => PollJson.Path(payload, "pull_request", "merged") is { ValueKind: JsonValueKind.True } ? EventKind.GitHubPrMerged : EventKind.GitHubPrClosed,
        _ => null,
    };

    private static EventKind? ReviewKind(JsonElement payload) => ReviewState(payload) is null ? null : EventKind.GitHubPrReviewSubmitted;

    // A diff comment carries pull_request; a conversation comment is on a pull request only when its issue links one.
    private static EventKind? CommentKind(JsonElement payload, string? action) =>
        Is(action, "created") && (PollJson.Member(payload, "pull_request") is not null || PollJson.Path(payload, "issue", "pull_request") is not null) ? EventKind.GitHubPrComment : null;

    private static EventKind? IssueKind(string? action) => action switch
    {
        "opened" => EventKind.GitHubIssueOpened,
        "labeled" => EventKind.GitHubIssueLabeled,
        _ => null,
    };

    // The feed sends 'created' for a submitted review; a dismissed or pending review is no catalog event.
    private static string? ReviewState(JsonElement payload)
    {
        var action = PollJson.Text(PollJson.Member(payload, "action"));
        var state = PollJson.Text(PollJson.Path(payload, "review", "state"));
        return (Is(action, "created") || Is(action, "submitted")) && state is not null ? _reviewStates.GetValueOrDefault(state) : null;
    }

    private static bool Is(string? text, string value) => string.Equals(text, value, StringComparison.Ordinal);

    private static Dictionary<string, string> Blank(EventKind kind) => _fields[kind.Key].ToDictionary(static k => k, static _ => string.Empty, StringComparer.Ordinal);

    // A conversation comment's subject is the issue the pull request is; every other pull request event carries pull_request.
    private static JsonElement PullOf(JsonElement payload, EventKind kind) =>
        PollJson.Member(payload, "pull_request") ?? (kind == EventKind.GitHubPrComment ? PollJson.Member(payload, "issue") : null) ?? default;

    // Sets a field only when the kind holds it, so a kind never gains a key its list lacks.
    private static void Set(Dictionary<string, string> fields, string name, string? value)
    {
        if (value is not null && fields.ContainsKey(name))
        {
            fields[name] = value;
        }
    }

    private static void PullFields(Dictionary<string, string> fields, JsonElement pull, string? repo)
    {
        var number = PollJson.Text(PollJson.Member(pull, "number"));
        PollJson.Put(fields, "number", number);
        PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(pull, "title")));
        PollJson.Put(fields, "author", PollJson.Text(PollJson.Path(pull, "user", "login")));
        PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(pull, "html_url")) ?? (repo is null || number is null ? null : $"https://github.com/{repo}/pull/{number}"));   // the trimmed feed has only the API url
        PollJson.Put(fields, "headSha", PollJson.Text(PollJson.Path(pull, "head", "sha")));
        PollJson.Put(fields, "baseRef", PollJson.Text(PollJson.Path(pull, "base", "ref")));
        Set(fields, "draft", PollJson.Member(pull, "draft") switch
        {
            { ValueKind: JsonValueKind.True } => "true",
            { ValueKind: JsonValueKind.False } => "false",
            _ => null,
        });
    }

    private static void PullExtras(Dictionary<string, string> fields, JsonElement payload)
    {
        Set(fields, "reviewer", PollJson.Text(PollJson.Path(payload, "requested_reviewer", "login")) ?? PollJson.Text(PollJson.Path(PollJson.Member(payload, "review") ?? default, "user", "login")));
        Set(fields, "state", ReviewState(payload));
        var comment = PollJson.Member(payload, "comment") ?? default;
        Set(fields, "commenter", PollJson.Text(PollJson.Path(comment, "user", "login")));
        Set(fields, "body", PollJson.Text(PollJson.Member(comment, "body")));
        Set(fields, "threadId", PollJson.Text(PollJson.Member(comment, "in_reply_to_id")) ?? PollJson.Text(PollJson.Member(comment, "id")));   // a reply names its thread's first comment
    }

    private static void IssueFields(Dictionary<string, string> fields, JsonElement payload)
    {
        var issue = PollJson.Member(payload, "issue") ?? default;
        PollJson.Put(fields, "number", PollJson.Text(PollJson.Member(issue, "number")));
        PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(issue, "title")));
        PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(issue, "html_url")));
        PollJson.Put(fields, "author", PollJson.Text(PollJson.Path(issue, "user", "login")));
        Set(fields, "label", PollJson.Text(PollJson.Path(payload, "label", "name")));
    }

    private static void PushFields(Dictionary<string, string> fields, JsonElement item, JsonElement payload)
    {
        PollJson.Put(fields, "ref", PollJson.Text(PollJson.Member(payload, "ref")));
        PollJson.Put(fields, "headSha", PollJson.Text(PollJson.Member(payload, "head")));
        PollJson.Put(fields, "pusher", PollJson.Text(PollJson.Path(item, "actor", "login")));
    }
}
