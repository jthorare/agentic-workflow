namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// messages.db's read SQL for the Messages routes (messages spec §8.2). A thread is (conversation, thread_root) as in <see cref="MessagesSql"/>; a line
/// is top-level when it has no thread_ts or is its own thread's root. Every ts is Slack's fixed-width text, so ts compare as text.
/// </summary>
internal static class MessagesReadSql
{
    // A list row: the thread's state, its conversation and its newest message (a flagged thread whose messages expired keeps its row with no text).
    private const string RowSelect =
        "SELECT c.workspace, c.kind, t.conversation, t.thread_ts, COALESCE(t.newest_ts, t.thread_ts), t.my_last_ts IS NOT NULL, " +
        "EXISTS (SELECT 1 FROM messages m WHERE m.conversation = t.conversation AND m.thread_root = t.thread_ts AND m.names_me = 1), " +
        "t.follow, COALESCE(n.mine, 0), COALESCE(n.names_me, 0), COALESCE(n.text, '') " +
        "FROM thread_state t JOIN conversations c ON c.id = t.conversation " +
        "LEFT JOIN messages n ON n.conversation = t.conversation AND n.ts = t.newest_ts ";

    private const string RowOrder = " ORDER BY COALESCE(t.newest_ts, t.thread_ts), t.conversation, t.thread_ts";

    // Unread: the newest message is another person's, past the local pointer (Slack's when there is none), in a conversation the owner has not muted.
    private const string UnreadCondition = "(c.muted = 0 AND n.mine = 0 AND t.newest_ts > COALESCE(c.read_ts, c.last_read_ts, ''))";

    internal const string NeedsReplyRows = RowSelect + "WHERE t.needs_reply = 1" + RowOrder;

    internal const string UnreadRows = RowSelect + "WHERE " + UnreadCondition + RowOrder;

    internal const string LaterRows = RowSelect + "WHERE t.later = 1" + RowOrder;

    internal const string DraftRows = RowSelect + "WHERE EXISTS (SELECT 1 FROM drafts d WHERE d.conversation = t.conversation AND d.thread_ts = t.thread_ts)" + RowOrder;

    /// <summary>One thread's row when it is in Needs reply or Unread.</summary>
    internal const string RowOfThread = RowSelect + "WHERE t.conversation = $c AND t.thread_ts = $root AND (t.needs_reply = 1 OR " + UnreadCondition + ")";

    /// <summary>Every conversation with its unread count: top-level lines from other people past the local pointer.</summary>
    internal const string Conversations =
        "SELECT c.id, c.kind, c.name, c.starred, " +
        "(SELECT COUNT(*) FROM messages m WHERE m.conversation = c.id AND m.mine = 0 AND (m.thread_ts IS NULL OR m.thread_ts = m.ts) " +
        "AND m.ts > COALESCE(c.read_ts, c.last_read_ts, '')) " +
        "FROM conversations c ORDER BY c.starred DESC, c.name, c.id";

    internal const string ConversationExists = "SELECT EXISTS (SELECT 1 FROM conversations WHERE id = $c)";

    // A line with its author's display name from the users table (display name, else real name, else handle).
    private const string LineSelect =
        "SELECT m.ts, m.user_id, m.text, m.thread_ts, m.reply_count, m.partial, " +
        "COALESCE(NULLIF(u.display_name, ''), NULLIF(u.real_name, ''), u.name) " +
        "FROM messages m LEFT JOIN users u ON u.id = m.user_id ";

    private const string TopLevel = "WHERE m.conversation = $c AND (m.thread_ts IS NULL OR m.thread_ts = m.ts) ";

    internal const string WindowNewest = "SELECT * FROM (" + LineSelect + TopLevel + "ORDER BY m.ts DESC LIMIT $limit) ORDER BY 1";

    internal const string WindowBefore = "SELECT * FROM (" + LineSelect + TopLevel + "AND m.ts < $before ORDER BY m.ts DESC LIMIT $limit) ORDER BY 1";

    internal const string WindowAfter = LineSelect + TopLevel + "AND m.ts > $after ORDER BY m.ts LIMIT $limit";

    /// <summary>A thread's root and replies, oldest first.</summary>
    internal const string ThreadLines = LineSelect + "WHERE m.conversation = $c AND (m.ts = $ts OR m.thread_ts = $ts) ORDER BY m.ts";

    /// <summary>
    /// The gaps of a conversation (the sync writes conversations.gaps as a JSON array of {"after": ts, "before": ts, "missing": n}) whose edge falls between
    /// $first and $last, the window's oldest and newest lines.
    /// </summary>
    internal const string WindowGaps =
        "SELECT json_extract(g.value, '$.after'), json_extract(g.value, '$.before'), COALESCE(json_extract(g.value, '$.missing'), 0) " +
        "FROM conversations c, json_each(c.gaps) g WHERE c.id = $c " +
        "AND (json_extract(g.value, '$.after') BETWEEN $first AND $last OR json_extract(g.value, '$.before') BETWEEN $first AND $last) ORDER BY 1";
}
