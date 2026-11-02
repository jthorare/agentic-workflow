using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// What the needs-you rows add up to outside the list (UI spec §5): the window title, the dock or taskbar badge, the tray
/// and Home's summary line. Counts and words only; every row counts, calm ones too, and the tray's own states (stale,
/// not running) win over the counts.
/// </summary>
/// <param name="Title">The window title.</param>
/// <param name="Badge">The dock or taskbar badge; 0 clears it.</param>
/// <param name="Tray">What the tray shows.</param>
/// <param name="SummaryLine">Home's summary line; empty before the list is known.</param>
internal sealed record AttentionSummary(string Title, int Badge, TrayView Tray, string SummaryLine)
{
    /// <summary>Works out the summary.</summary>
    /// <param name="feed">The needs-you list, or null before it is known.</param>
    /// <param name="state">The shell's state.</param>
    /// <param name="now">The time the ages are read at.</param>
    /// <param name="sessions">The status' session counts, or null when it serves none; they add a tooltip line only, never the badge or the title.</param>
    /// <param name="listed">How many rows Home's card counts (the rows Home lists, before its cap), or null for every row. Only the summary line takes it: the card's "all N" link says the same N, so the header and the card agree (B-1); the title, badge and tray still count every row.</param>
    /// <returns>The summary.</returns>
    internal static AttentionSummary For(NeedsYouFeed? feed, ShellState state, DateTimeOffset now, SessionCountsView? sessions = null, int? listed = null)
    {
        var rows = feed?.Rows ?? [];
        var counts = feed?.Counts ?? new HomeCounts(Running: 0, WithYou: 0, Queued: 0);

        // Rows are kept while the views are stale; with no running Sindri, or none yet answering, there are none to count.
        var badge = IsCounted(state) ? rows.Count : 0;
        return new AttentionSummary(
            badge > 0 ? Labels.TitleNeedsYou(badge) : Labels.Sindri,
            badge,
            TrayFor(feed, state, now, sessions),
            feed is null ? "" : Labels.Summary(listed ?? rows.Count, counts.Running, counts.WithYou, counts.Queued));
    }

    /// <summary>True when the state shows the list's rows (a running Sindri, fresh or not); else the count is 0 because there is no list to count.</summary>
    /// <param name="state">The shell's state.</param>
    /// <returns>True when the rows count.</returns>
    internal static bool IsCounted(ShellState state) => state is ShellState.Normal or ShellState.Stale or ShellState.Unreadable;

    // The tray's own states win over the counts: only a running, fresh Sindri shows rows.
    private static TrayView TrayFor(NeedsYouFeed? feed, ShellState state, DateTimeOffset now, SessionCountsView? sessions) => state switch
    {
        ShellState.Normal => WithAgents(feed is null ? Plain(TrayState.Neutral, Labels.TrayRunning) : Counted(feed, now), sessions),
        ShellState.NotRunning => Plain(TrayState.NotRunning, Labels.TrayNotRunning),
        ShellState.VersionMismatch or ShellState.Connecting or ShellState.Unreadable or ShellState.Stale => Plain(TrayState.Stale, Labels.TrayNotResponding),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, message: null),
    };

    // A second tooltip line, "N agents · M asking", once the status serves session counts (collector spec CU-1).
    private static TrayView WithAgents(TrayView tray, SessionCountsView? sessions) =>
        sessions is null ? tray : tray with { Tooltip = $"{tray.Tooltip}\n{Labels.AgentsCounts(sessions.Live, sessions.Asking)}" };

    private static TrayView Plain(TrayState state, string tooltip) => new(state, tooltip, Labels.TrayOpen, Labels.TrayQuit);

    private static TrayView Counted(NeedsYouFeed feed, DateTimeOffset now)
    {
        var rows = feed.Rows;
        var urgent = rows.Any(r => r.Urgency is NeedsYouUrgency.Pulse or NeedsYouUrgency.Colour);
        var (state, tooltip) =
            urgent ? (TrayState.Attention, Labels.TrayNeedYou(rows.Count, feed.Counts.Running))
            : rows.Count > 0 ? (TrayState.Calm, Labels.TrayToLookAt(rows.Count))
            : (TrayState.Neutral, Labels.TrayNothingNeedsYou(feed.Counts.Running));
        return Plain(state, tooltip) with
        {
            Count = rows.Count,
            Rows = [.. rows.Take(TrayView.MaxRows).Select(r => new TrayRow(r.Id, Row(r, now)))],
            RunningLabel = Labels.TrayRunningItem(feed.Counts.Running),
        };
    }

    private static string Row(NeedsYouEntry row, DateTimeOffset now) =>
        Labels.TrayRow(row.Chip?.Word ?? "", row.ShownId, row.Title, row.Since == default ? "" : Labels.RowAge(now - row.Since));
}
