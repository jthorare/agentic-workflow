namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// One pollable event, named by its catalog id ('github.pr.merged'): '&lt;preset&gt;.&lt;thing&gt;.&lt;what happened&gt;'. A Connection event block (connection_event) names a
/// connection and one of these; the poller makes one provider call per (connection, event kind).
/// </summary>
/// <param name="Key">The catalog id.</param>
public readonly record struct EventKind(string Key)
{
    /// <summary>Gets 'github.pr.opened'.</summary>
    public static EventKind GitHubPrOpened { get; } = new("github.pr.opened");

    /// <summary>Gets 'github.pr.merged'.</summary>
    public static EventKind GitHubPrMerged { get; } = new("github.pr.merged");

    /// <summary>Gets 'github.pr.review_requested'.</summary>
    public static EventKind GitHubPrReviewRequested { get; } = new("github.pr.review_requested");

    /// <summary>Gets 'github.pr.updated'.</summary>
    public static EventKind GitHubPrUpdated { get; } = new("github.pr.updated");

    /// <summary>Gets 'github.pr.ready_for_review'.</summary>
    public static EventKind GitHubPrReadyForReview { get; } = new("github.pr.ready_for_review");

    /// <summary>Gets 'github.pr.closed'.</summary>
    public static EventKind GitHubPrClosed { get; } = new("github.pr.closed");

    /// <summary>Gets 'github.pr.review_submitted'.</summary>
    public static EventKind GitHubPrReviewSubmitted { get; } = new("github.pr.review_submitted");

    /// <summary>Gets 'github.pr.comment'.</summary>
    public static EventKind GitHubPrComment { get; } = new("github.pr.comment");

    /// <summary>Gets 'github.pr.checks'.</summary>
    public static EventKind GitHubPrChecks { get; } = new("github.pr.checks");

    /// <summary>Gets 'github.pr.conflict'.</summary>
    public static EventKind GitHubPrConflict { get; } = new("github.pr.conflict");

    /// <summary>Gets 'github.issue.opened'.</summary>
    public static EventKind GitHubIssueOpened { get; } = new("github.issue.opened");

    /// <summary>Gets 'github.issue.labeled'.</summary>
    public static EventKind GitHubIssueLabeled { get; } = new("github.issue.labeled");

    /// <summary>Gets 'github.push'.</summary>
    public static EventKind GitHubPush { get; } = new("github.push");

    /// <summary>Gets 'linear.issue.created'.</summary>
    public static EventKind LinearIssueCreated { get; } = new("linear.issue.created");

    /// <summary>Gets 'linear.issue.state_changed'.</summary>
    public static EventKind LinearIssueStateChanged { get; } = new("linear.issue.state_changed");

    /// <summary>Gets 'linear.issue.labeled'.</summary>
    public static EventKind LinearIssueLabeled { get; } = new("linear.issue.labeled");

    /// <summary>Gets 'linear.comment.added'.</summary>
    public static EventKind LinearCommentAdded { get; } = new("linear.comment.added");

    /// <summary>Gets 'slack.message': a message in the connection's named channel that matches its filter.</summary>
    public static EventKind SlackMessage { get; } = new("slack.message");

    /// <summary>Gets 'slack.mention': a message in the named channel that mentions the owner.</summary>
    public static EventKind SlackMention { get; } = new("slack.mention");

    /// <summary>Gets 'google.calendar.event_starting': a calendar event that starts within the connection's window.</summary>
    public static EventKind GoogleCalendarEventStarting { get; } = new("google.calendar.event_starting");

    /// <summary>The id as typed back.</summary>
    /// <returns>The catalog id.</returns>
    public override string ToString() => Key;
}
