using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Home page as rows (home spec §7.1, §7.3).</summary>
internal static partial class Labels
{
    /// <summary>The heading of the rows that wait on the owner.</summary>
    internal const string HomeNeedsYouGroup = "Needs you";

    /// <summary>The heading of the rows that move without the owner.</summary>
    internal const string HomeMovingGroup = "Moving";

    /// <summary>The tab of every kind.</summary>
    internal const string HomeTabAll = "All";

    /// <summary>The tab of reviews and PRs ready to merge.</summary>
    internal const string HomeTabReviews = "Reviews";

    /// <summary>The tab of mentions and messages.</summary>
    internal const string HomeTabMessages = "Messages";

    /// <summary>The tab of issues, todos, runs, decisions and questions.</summary>
    internal const string HomeTabWork = "Work";

    /// <summary>The tab of sessions and lanes.</summary>
    internal const string HomeTabAgents = "Agents";

    /// <summary>The one line the empty Messages tab shows while there is no Messages page (home spec §7.3).</summary>
    internal const string HomeMessagesLater = "Slack messages arrive in a later release.";

    /// <summary>Settings' switch that shows Home as rows.</summary>
    internal const string HomeAsRows = "Home as rows";

    /// <summary>The line under Settings' Home switch.</summary>
    internal const string HomeAsRowsNote = "One ranked list in place of the panels.";

    /// <summary>What a screen reader says for the tabs as a whole.</summary>
    internal const string HomeTabsName = "Home tabs";

    /// <summary>What a screen reader says for one tab: its word and its open count.</summary>
    /// <param name="tab">The tab's word.</param>
    /// <param name="count">Its open rows.</param>
    /// <returns>The name.</returns>
    internal static string HomeTabName(string tab, int count) => string.Create(CultureInfo.InvariantCulture, $"{tab}, {count}");

    /// <summary>The one plain line a tab with no rows shows (home spec §7.3).</summary>
    /// <param name="tab">The tab's word.</param>
    /// <returns>The line.</returns>
    internal static string HomeNothingIn(string tab) => $"Nothing in {tab}";

    /// <summary>The page's title.</summary>
    internal const string HomeTitle = "Home";

    /// <summary>The object cell of a row that leads with its id: the repo it is in.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <returns>The cell's text.</returns>
    internal static string HomeObjectRepo(string repo) => $"repo {repo}";

    /// <summary>The object cell of a session's row.</summary>
    /// <param name="session">The session's name.</param>
    /// <returns>The cell's text.</returns>
    internal static string HomeObjectSession(string session) => $"session {session}";

    /// <summary>The reason word the why cell shows for a row that blocks fewer than 2 (home spec §4.2 "Reason words"), or null for a kind with no fixed word.</summary>
    /// <param name="kind">The row's kind, such as 'pr-ready-merge'.</param>
    /// <returns>The word, or null.</returns>
    internal static string? HomeReason(string kind) => kind switch
    {
        "pr-ready-merge" => "ready",
        "review-requested" => "review requested",
        "review-new-commits" => "new commits",
        "mention" => "mention",
        "dm" => "DM",
        "thread-reply" => "reply",
        "keyword" => "keyword",
        "run-failed" => "failed",
        "issue-assigned" => "assigned",
        "todo-due" => "due",
        "system" => "setup",
        "agent-asking" => "asking",
        "agent-limited" => "limited",
        _ => null,
    };
}
