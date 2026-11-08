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
    /// <param name="createdAt">ISO time of the history entry.</param>
    /// <returns>The JSON object.</returns>
    public static string LinearLabelAdded(string identifier, string title, string historyId, string label, string createdAt)
        => $$$"""
            {"identifier":"{{{identifier}}}","title":"{{{title}}}","url":"https://linear.app/acme/issue/{{{identifier}}}","history":{"nodes":[{"id":"{{{historyId}}}","createdAt":"{{{createdAt}}}","addedLabels":[{"name":"{{{label}}}"}],"fromState":null,"toState":null}]}}
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
