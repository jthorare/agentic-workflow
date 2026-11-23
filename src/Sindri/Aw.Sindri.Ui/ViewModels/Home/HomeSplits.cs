namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>Which row kinds each <see cref="HomeSplit"/> holds (home spec §7.1 "Splits").</summary>
internal static class HomeSplits
{
    private static readonly HashSet<string> _reviews = new(StringComparer.Ordinal) { "review-requested", "review-new-commits", "pr-ready-merge" };
    private static readonly HashSet<string> _messages = new(StringComparer.Ordinal) { "mention", "dm", "thread-reply", "keyword" };
    private static readonly HashSet<string> _work = new(StringComparer.Ordinal) { "issue-assigned", "todo-due", "run-failed", "decision", "question" };
    private static readonly HashSet<string> _agents = new(StringComparer.Ordinal) { "agent-asking", "agent-limited", "lane-recycle" };

    /// <summary>The tabs in header order.</summary>
    internal static IReadOnlyList<HomeSplit> InOrder { get; } = [HomeSplit.All, HomeSplit.Reviews, HomeSplit.Messages, HomeSplit.Work, HomeSplit.Agents];

    /// <summary>True when a row of this kind shows under the split.</summary>
    /// <param name="split">The split.</param>
    /// <param name="kind">The row's kind, such as 'review-requested'.</param>
    /// <returns>True when the split holds the kind.</returns>
    internal static bool Holds(HomeSplit split, string kind) => split switch
    {
        HomeSplit.Reviews => _reviews.Contains(kind),
        HomeSplit.Messages => _messages.Contains(kind),
        HomeSplit.Work => _work.Contains(kind),
        HomeSplit.Agents => _agents.Contains(kind),
        HomeSplit.All => true,
        _ => true,
    };

    /// <summary>A tab's word.</summary>
    /// <param name="split">The tab's split.</param>
    /// <returns>The word.</returns>
    internal static string Label(HomeSplit split) => split switch
    {
        HomeSplit.Reviews => Labels.HomeTabReviews,
        HomeSplit.Messages => Labels.HomeTabMessages,
        HomeSplit.Work => Labels.HomeTabWork,
        HomeSplit.Agents => Labels.HomeTabAgents,
        HomeSplit.All => Labels.HomeTabAll,
        _ => Labels.HomeTabAll,
    };

    /// <summary>The one plain line a tab with no rows shows: the Messages tab says Slack comes later while there is no Messages page (home spec §7.3).</summary>
    /// <param name="split">The tab's split.</param>
    /// <returns>The line.</returns>
    internal static string EmptyLine(HomeSplit split) => split == HomeSplit.Messages ? Labels.HomeMessagesLater : Labels.HomeNothingIn(Label(split));

    /// <summary>The kind the row grammar draws a Home kind as: pr, thread, session, run, decision, issue or item.</summary>
    /// <param name="kind">The row's kind.</param>
    /// <returns>The grammar kind.</returns>
    internal static string GrammarKind(string kind) => kind switch
    {
        _ when _reviews.Contains(kind) => "pr",
        _ when _messages.Contains(kind) => "thread",
        _ when _agents.Contains(kind) => "session",
        "run-failed" => "run",
        "decision" or "question" => "decision",
        "issue-assigned" => "issue",
        _ => "item",
    };
}
