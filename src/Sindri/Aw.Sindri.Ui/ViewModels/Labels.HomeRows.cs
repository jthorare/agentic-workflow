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

    /// <summary>The one plain line a tab with no rows shows (home spec §7.3).</summary>
    /// <param name="tab">The tab's word.</param>
    /// <returns>The line.</returns>
    internal static string HomeNothingIn(string tab) => $"Nothing in {tab}";
}
