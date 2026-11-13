using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing;

/// <summary>
/// GitHub JSON in the shapes the REST API answers, for the GitHub event kinds of the Workflows page (#3201, #2988): the repo events feed's items
/// (PullRequestEvent, PullRequestReviewEvent, PullRequestReviewCommentEvent, IssueCommentEvent, IssuesEvent, PushEvent), an item of
/// GET /repos/{r}/pulls, the GET /repos/{r}/pulls/{n} detail and a GET /repos/{r}/commits/{sha}/check-runs answer. Every pull request has the
/// title 'Fix login', the author 'ada' and the base 'main' unless a test names others.
/// </summary>
public static class GitHubFeedFixtures
{
    /// <summary>The title every fixture pull request has.</summary>
    public const string Title = "Fix login";

    /// <summary>The author every fixture pull request has.</summary>
    public const string Author = "ada";

    /// <summary>A full-shape PullRequestEvent (title, user, html_url, draft, merged, head and base).</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="action">'opened', 'synchronize', 'ready_for_review', 'review_requested', 'closed', 'merged', 'reopened' ...</param>
    /// <param name="createdAt">ISO time.</param>
    /// <param name="headSha">The head commit sha.</param>
    /// <param name="draft">Whether the pull request is a draft.</param>
    /// <param name="merged">The pull request's merged flag.</param>
    /// <param name="reviewer">The requested reviewer's login (review_requested), or null.</param>
    /// <returns>The JSON object.</returns>
    public static string PullRequest(string eventId, string repo, int number, string action, string createdAt, string headSha = "abc123", bool draft = false, bool merged = false, string? reviewer = null)
    {
        var payload = new JsonObject { ["action"] = action, ["number"] = number, ["pull_request"] = Pull(repo, number, headSha, draft, merged) };
        if (reviewer is not null)
        {
            payload["requested_reviewer"] = new JsonObject { ["login"] = reviewer };
        }

        return Event(eventId, "PullRequestEvent", repo, createdAt, payload);
    }

    /// <summary>A PullRequestReviewEvent: a review was submitted on a pull request.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="reviewer">The reviewer's login.</param>
    /// <param name="state">The review state as the feed sends it ('approved', 'changes_requested', 'commented', 'dismissed').</param>
    /// <param name="createdAt">ISO time.</param>
    /// <param name="action">The event action ('created' in the events feed).</param>
    /// <returns>The JSON object.</returns>
    public static string ReviewSubmitted(string eventId, string repo, int number, string reviewer, string state, string createdAt, string action = "created")
    {
        var payload = new JsonObject
        {
            ["action"] = action,
            ["review"] = new JsonObject { ["id"] = 900 + number, ["state"] = state, ["user"] = new JsonObject { ["login"] = reviewer } },
            ["pull_request"] = Pull(repo, number, "abc123", draft: false, merged: false),
        };
        return Event(eventId, "PullRequestReviewEvent", repo, createdAt, payload);
    }

    /// <summary>A PullRequestReviewCommentEvent: a comment on a line of a pull request's diff.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="commenter">The commenter's login.</param>
    /// <param name="body">The comment text.</param>
    /// <param name="commentId">The comment id.</param>
    /// <param name="inReplyTo">The id of the thread's first comment when this is a reply, or null.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string ReviewComment(string eventId, string repo, int number, string commenter, string body, long commentId, long? inReplyTo, string createdAt)
    {
        var comment = new JsonObject { ["id"] = commentId, ["body"] = body, ["user"] = new JsonObject { ["login"] = commenter } };
        if (inReplyTo is { } first)
        {
            comment["in_reply_to_id"] = first;
        }

        var payload = new JsonObject { ["action"] = "created", ["comment"] = comment, ["pull_request"] = Pull(repo, number, "abc123", draft: false, merged: false) };
        return Event(eventId, "PullRequestReviewCommentEvent", repo, createdAt, payload);
    }

    /// <summary>An IssueCommentEvent: a conversation comment on an issue, or on a pull request when <paramref name="onPull"/> is true (the issue then carries a pull_request link).</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="commenter">The commenter's login.</param>
    /// <param name="body">The comment text.</param>
    /// <param name="commentId">The comment id.</param>
    /// <param name="onPull">Whether the issue is a pull request.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string IssueComment(string eventId, string repo, int number, string commenter, string body, long commentId, bool onPull, string createdAt)
    {
        var n = Num(number);
        var issue = new JsonObject
        {
            ["number"] = number,
            ["title"] = Title,
            ["html_url"] = $"https://github.com/{repo}/{(onPull ? "pull" : "issues")}/{n}",
            ["user"] = new JsonObject { ["login"] = Author },
        };
        if (onPull)
        {
            issue["pull_request"] = new JsonObject { ["url"] = $"https://api.github.com/repos/{repo}/pulls/{n}", ["html_url"] = $"https://github.com/{repo}/pull/{n}" };
        }

        var payload = new JsonObject
        {
            ["action"] = "created",
            ["issue"] = issue,
            ["comment"] = new JsonObject { ["id"] = commentId, ["body"] = body, ["user"] = new JsonObject { ["login"] = commenter } },
        };
        return Event(eventId, "IssueCommentEvent", repo, createdAt, payload);
    }

    /// <summary>An IssuesEvent.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="action">'opened', 'labeled', 'closed' ...</param>
    /// <param name="createdAt">ISO time.</param>
    /// <param name="label">The label added (labeled), or null.</param>
    /// <returns>The JSON object.</returns>
    public static string Issue(string eventId, string repo, int number, string action, string createdAt, string? label = null)
    {
        var payload = new JsonObject
        {
            ["action"] = action,
            ["issue"] = new JsonObject
            {
                ["number"] = number,
                ["title"] = "Login is slow",
                ["html_url"] = $"https://github.com/{repo}/issues/{Num(number)}",
                ["user"] = new JsonObject { ["login"] = "bob" },
            },
        };
        if (label is not null)
        {
            payload["label"] = new JsonObject { ["name"] = label };
        }

        return Event(eventId, "IssuesEvent", repo, createdAt, payload);
    }

    /// <summary>A PushEvent; the actor is the pusher.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="gitRef">The pushed ref ('refs/heads/main').</param>
    /// <param name="head">The new head sha.</param>
    /// <param name="pusher">The pusher's login.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string Push(string eventId, string repo, string gitRef, string head, string pusher, string createdAt)
    {
        var payload = new JsonObject { ["ref"] = gitRef, ["head"] = head, ["before"] = "0000000" };
        var item = JsonNode.Parse(Event(eventId, "PushEvent", repo, createdAt, payload))!.AsObject();
        item["actor"] = new JsonObject { ["login"] = pusher };
        return item.ToJsonString();
    }

    /// <summary>Any other feed event (a WatchEvent), which no catalog kind takes.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string Watch(string eventId, string repo, string createdAt) => Event(eventId, "WatchEvent", repo, createdAt, new JsonObject { ["action"] = "started" });

    /// <summary>One open pull request as GET /repos/{r}/pulls lists it (and as GET /repos/{r}/pulls/{n} answers it, with <paramref name="mergeableState"/>).</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="headSha">The head commit sha.</param>
    /// <param name="updatedAt">ISO time of the pull request's last change.</param>
    /// <param name="mergeableState">'clean', 'dirty', 'unknown' ... for the detail answer; null for a list item, which has none.</param>
    /// <param name="draft">Whether the pull request is a draft.</param>
    /// <param name="createdAt">ISO time the pull request was opened; null leaves created_at out.</param>
    /// <returns>The JSON object.</returns>
    public static string OpenPull(string repo, int number, string headSha, string updatedAt, string? mergeableState = null, bool draft = false, string? createdAt = null)
    {
        var pull = Pull(repo, number, headSha, draft, merged: false);
        pull["state"] = "open";
        pull["updated_at"] = updatedAt;
        if (createdAt is not null)
        {
            pull["created_at"] = createdAt;
        }

        if (mergeableState is not null)
        {
            pull["mergeable_state"] = mergeableState;
            pull["mergeable"] = !string.Equals(mergeableState, "dirty", StringComparison.Ordinal);
        }

        return pull.ToJsonString();
    }

    /// <summary>A GET /repos/{r}/commits/{sha}/check-runs answer.</summary>
    /// <param name="runs">Each run's name, status ('queued', 'in_progress', 'completed'), conclusion ('success', 'failure', 'neutral', 'skipped', 'timed_out', 'cancelled' or null while not completed) and completed_at (ISO time or null).</param>
    /// <returns>The JSON object.</returns>
    public static string CheckRuns(params (string Name, string Status, string? Conclusion, string? CompletedAt)[] runs) => CheckRunsFrom(7001, runs);

    /// <summary>A check-runs answer whose runs get ids counting up from <paramref name="firstId"/>, so a later answer can model a re-run (new run ids).</summary>
    /// <param name="firstId">The id of the first run.</param>
    /// <param name="runs">As for <see cref="CheckRuns"/>.</param>
    /// <returns>The JSON object.</returns>
    public static string CheckRunsFrom(int firstId, params (string Name, string Status, string? Conclusion, string? CompletedAt)[] runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var list = new JsonArray();
        var id = firstId - 1;
        foreach (var (name, status, conclusion, completedAt) in runs)
        {
            list.Add(new JsonObject { ["id"] = ++id, ["name"] = name, ["status"] = status, ["conclusion"] = conclusion, ["completed_at"] = completedAt });
        }

        return new JsonObject { ["total_count"] = runs.Length, ["check_runs"] = list }.ToJsonString();
    }

    private static JsonObject Pull(string repo, int number, string headSha, bool draft, bool merged)
    {
        var n = Num(number);
        return new JsonObject
        {
            ["number"] = number,
            ["title"] = Title,
            ["draft"] = draft,
            ["merged"] = merged,
            ["html_url"] = $"https://github.com/{repo}/pull/{n}",
            ["url"] = $"https://api.github.com/repos/{repo}/pulls/{n}",
            ["user"] = new JsonObject { ["login"] = Author },
            ["head"] = new JsonObject { ["ref"] = "topic", ["sha"] = headSha },
            ["base"] = new JsonObject { ["ref"] = "main", ["sha"] = "0000000000000000000000000000000000000000" },
        };
    }

    private static string Event(string eventId, string type, string repo, string createdAt, JsonObject payload) =>
        new JsonObject
        {
            ["id"] = eventId,
            ["type"] = type,
            ["actor"] = new JsonObject { ["login"] = Author },
            ["repo"] = new JsonObject { ["name"] = repo },
            ["created_at"] = createdAt,
            ["payload"] = payload,
        }.ToJsonString();

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}
