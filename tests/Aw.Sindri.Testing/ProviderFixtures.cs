using System.Globalization;

namespace Aw.Sindri.Testing;

/// <summary>Provider event JSON in the shape the real APIs answer, for <see cref="FakeProvider"/> and the poller tests.</summary>
public static class ProviderFixtures
{
    /// <summary>A GitHub PullRequestEvent for a merged pull request (action 'closed', merged true).</summary>
    /// <param name="eventId">GitHub's event id ('1001').</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="title">The title.</param>
    /// <param name="author">The author's login.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string GitHubPrMerged(string eventId, string repo, int number, string title, string author, string createdAt)
        => GitHubPr(eventId, repo, number, title, author, createdAt, "closed", merged: true, reviewer: null);

    /// <summary>A GitHub PullRequestEvent as the repo events feed sends it since late 2026: the action is 'merged' for a merge and pull_request holds only base, head, id, number and url (the API url), with no title, user or html_url.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="action">'merged', 'opened' ...</param>
    /// <param name="headSha">The head commit sha.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string GitHubPrTrimmed(string eventId, string repo, int number, string action, string headSha, string createdAt)
    {
        var n = number.ToString(CultureInfo.InvariantCulture);
        var pr = "{\"base\":{\"ref\":\"main\",\"sha\":\"0000000000000000000000000000000000000000\"},\"head\":{\"ref\":\"topic\",\"sha\":\"" + headSha + "\"},\"id\":" + n + "00,\"number\":" + n + ",\"url\":\"https://api.github.com/repos/" + repo + "/pulls/" + n + "\"}";
        return "{\"id\":\"" + eventId + "\",\"type\":\"PullRequestEvent\",\"repo\":{\"name\":\"" + repo + "\"},\"created_at\":\"" + createdAt + "\",\"payload\":{\"action\":\"" + action + "\",\"number\":" + n + ",\"pull_request\":" + pr + "}}";
    }

    /// <summary>GitHub's answer to GET /repos/{owner}/{name}/pulls/{n}: the full pull request, with its title, author and page url.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="title">The title.</param>
    /// <param name="author">The author's login.</param>
    /// <returns>The JSON object.</returns>
    public static string GitHubPull(string repo, int number, string title, string author)
    {
        var n = number.ToString(CultureInfo.InvariantCulture);
        return "{\"number\":" + n + ",\"title\":\"" + title + "\",\"html_url\":\"https://github.com/" + repo + "/pull/" + n + "\",\"user\":{\"login\":\"" + author + "\"}}";
    }

    /// <summary>A GitHub PullRequestEvent with action 'review_requested'.</summary>
    /// <param name="eventId">GitHub's event id.</param>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="title">The title.</param>
    /// <param name="author">The author's login.</param>
    /// <param name="reviewer">The requested reviewer's login.</param>
    /// <param name="createdAt">ISO time.</param>
    /// <returns>The JSON object.</returns>
    public static string GitHubReviewRequested(string eventId, string repo, int number, string title, string author, string reviewer, string createdAt)
        => GitHubPr(eventId, repo, number, title, author, createdAt, "review_requested", merged: false, reviewer);

    /// <summary>A Linear issue (the 'issues' query) with one history entry that added one label.</summary>
    /// <param name="identifier">'ENG-7'.</param>
    /// <param name="title">The title.</param>
    /// <param name="historyId">The history entry id ('h-55').</param>
    /// <param name="label">The label added.</param>
    /// <param name="labelId">The label's id.</param>
    /// <param name="createdAt">ISO time of the history entry.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearLabelAdded(string identifier, string title, string historyId, string label, string labelId, string createdAt)
        => $$$"""
            {"identifier":"{{{identifier}}}","title":"{{{title}}}","url":"https://linear.app/acme/issue/{{{identifier}}}","history":{"nodes":[{"id":"{{{historyId}}}","createdAt":"{{{createdAt}}}","addedLabels":[{"id":"{{{labelId}}}","name":"{{{label}}}"}],"fromState":null,"toState":null}]}}
            """;

    /// <summary>A Linear issue (the 'issues' query) with one history entry that moved it to a state.</summary>
    /// <param name="identifier">'ENG-7'.</param>
    /// <param name="title">The title.</param>
    /// <param name="historyId">The history entry id.</param>
    /// <param name="stateId">The new state's id.</param>
    /// <param name="stateName">The new state's name.</param>
    /// <param name="createdAt">ISO time of the history entry.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearStateChanged(string identifier, string title, string historyId, string stateId, string stateName, string createdAt)
        => $$$"""
            {"identifier":"{{{identifier}}}","title":"{{{title}}}","url":"https://linear.app/acme/issue/{{{identifier}}}","history":{"nodes":[{"id":"{{{historyId}}}","createdAt":"{{{createdAt}}}","addedLabels":[],"fromState":{"id":"s-0","name":"Todo"},"toState":{"id":"{{{stateId}}}","name":"{{{stateName}}}"}}]}}
            """;

    /// <summary>A Linear issue created at a time, with no history.</summary>
    /// <param name="identifier">'ENG-9'.</param>
    /// <param name="title">The title.</param>
    /// <param name="createdAt">ISO time the issue was created.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearIssueCreated(string identifier, string title, string createdAt)
        => $$$"""
            {"identifier":"{{{identifier}}}","title":"{{{title}}}","url":"https://linear.app/acme/issue/{{{identifier}}}","createdAt":"{{{createdAt}}}","history":{"nodes":[]}}
            """;

    /// <summary>A Linear issue created long ago with one comment on it.</summary>
    /// <param name="identifier">'ENG-3'.</param>
    /// <param name="title">The title.</param>
    /// <param name="commentId">The comment id ('c-7').</param>
    /// <param name="body">The comment text.</param>
    /// <param name="author">The commenter's name.</param>
    /// <param name="createdAt">ISO time of the comment.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearComment(string identifier, string title, string commentId, string body, string author, string createdAt)
        => $$$"""
            {"identifier":"{{{identifier}}}","title":"{{{title}}}","url":"https://linear.app/acme/issue/{{{identifier}}}","createdAt":"2025-01-01T00:00:00Z","history":{"nodes":[]},"comments":{"nodes":[{"id":"{{{commentId}}}","createdAt":"{{{createdAt}}}","body":"{{{body}}}","user":{"name":"{{{author}}}"}}]}}
            """;

    /// <summary>A Slack message as conversations.history returns it.</summary>
    /// <param name="ts">The message ts ('1731060000.000100').</param>
    /// <param name="user">The author's user id.</param>
    /// <param name="text">The text.</param>
    /// <returns>The JSON object.</returns>
    public static string SlackMessage(string ts, string user, string text)
        => $$$"""
            {"type":"message","user":"{{{user}}}","text":"{{{text}}}","ts":"{{{ts}}}"}
            """;

    /// <summary>A timed Google Calendar event as the events list returns it.</summary>
    /// <param name="eventId">The event id.</param>
    /// <param name="summary">The title.</param>
    /// <param name="start">ISO start time.</param>
    /// <returns>The JSON object.</returns>
    public static string CalendarEvent(string eventId, string summary, string start)
        => $$$"""
            {"id":"{{{eventId}}}","summary":"{{{summary}}}","htmlLink":"https://calendar.google.com/event?eid={{{eventId}}}","start":{"dateTime":"{{{start}}}"}}
            """;

    /// <summary>A Linear issue created long ago with <paramref name="count"/> comments, one second apart from <paramref name="firstAt"/>; the comments list says whether it has a next page.</summary>
    /// <param name="identifier">'ENG-4'.</param>
    /// <param name="count">How many comments the page holds.</param>
    /// <param name="hasNextPage">Whether Linear says more comments exist.</param>
    /// <param name="firstAt">Time of the first comment.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearManyComments(string identifier, int count, bool hasNextPage, DateTimeOffset firstAt)
    {
        var nodes = string.Join(',', Enumerable.Range(1, count).Select(i =>
            "{\"id\":\"c-" + Num(i) + "\",\"createdAt\":\"" + At(firstAt, i) + "\",\"body\":\"b\",\"user\":{\"name\":\"Ada\"}}"));
        return Issue(identifier, "\"history\":{\"nodes\":[]},\"comments\":{\"nodes\":[" + nodes + "],\"pageInfo\":{\"hasNextPage\":" + Flag(hasNextPage) + "}}");
    }

    /// <summary>A Linear issue with <paramref name="count"/> history entries that each added a label, one second apart from <paramref name="firstAt"/>; the history list says whether it has a next page.</summary>
    /// <param name="identifier">'ENG-5'.</param>
    /// <param name="count">How many history entries the page holds.</param>
    /// <param name="hasNextPage">Whether Linear says more history exists.</param>
    /// <param name="firstAt">Time of the first entry.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearManyLabels(string identifier, int count, bool hasNextPage, DateTimeOffset firstAt)
    {
        var nodes = string.Join(',', Enumerable.Range(1, count).Select(i =>
            "{\"id\":\"h-" + Num(i) + "\",\"createdAt\":\"" + At(firstAt, i) + "\",\"addedLabels\":[{\"id\":\"lbl-" + Num(i) + "\",\"name\":\"bug\"}],\"fromState\":null,\"toState\":null}"));
        return Issue(identifier, "\"history\":{\"nodes\":[" + nodes + "],\"pageInfo\":{\"hasNextPage\":" + Flag(hasNextPage) + "}},\"comments\":{\"nodes\":[]}");
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string At(DateTimeOffset first, int seconds) => first.AddSeconds(seconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Issue(string identifier, string rest)
        => "{\"identifier\":\"" + identifier + "\",\"title\":\"t\",\"url\":\"https://linear.app/acme/issue/" + identifier + "\",\"createdAt\":\"2025-01-01T00:00:00Z\"," + rest + "}";

    private static string GitHubPr(string eventId, string repo, int number, string title, string author, string createdAt, string action, bool merged, string? reviewer)
    {
        var requested = reviewer is null ? string.Empty : $$$""","requested_reviewer":{"login":"{{{reviewer}}}"}""";
        var mergedText = merged ? "true" : "false";
        var body = string.Create(CultureInfo.InvariantCulture,
            $$$"""
            {"id":"{{{eventId}}}","type":"PullRequestEvent","created_at":"{{{createdAt}}}","repo":{"name":"{{{repo}}}"},"payload":{"action":"{{{action}}}","number":{{{number}}}{{{requested}}},"pull_request":{"number":{{{number}}},"title":"{{{title}}}","merged":{{{mergedText}}},"html_url":"https://github.com/{{{repo}}}/pull/{{{number}}}","user":{"login":"{{{author}}}"}}
            """);
        return body + "}}";   // payload and event close here; a raw string holds at most two closers in a row
    }
}
