namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// messages.db's SQL (messages spec §8.1). A thread is the pair (conversation, thread_root): thread_root is a channel thread's root ts and '' for a DM or
/// group DM, whose row is the conversation. Every ts is Slack's fixed-width text ('1767771000.000100'), so ts compare as text.
/// </summary>
internal static class MessagesSql
{
    internal const string SelectConversation = "SELECT workspace, kind FROM conversations WHERE id = $c";

    internal const string UpsertConversation =
        "INSERT INTO conversations (id, workspace, kind, name, ext_shared) VALUES ($id, $workspace, $kind, $name, $ext) " +
        "ON CONFLICT (id) DO UPDATE SET workspace = excluded.workspace, kind = excluded.kind, name = excluded.name, ext_shared = excluded.ext_shared";

    // A row known only from a search hit stays partial until a full read: partial AND partial.
    internal const string UpsertMessage =
        "INSERT INTO messages (workspace, conversation, ts, thread_ts, thread_root, user_id, text, blocks, reactions, subtype, edited_ts, reply_count, " +
        "latest_reply_ts, partial, file_ids, mine, names_me) VALUES ($workspace, $c, $ts, $threadTs, $root, $user, $text, $blocks, $reactions, $subtype, " +
        "$edited, $replies, $latestReply, $partial, $files, $mine, $namesMe) " +
        "ON CONFLICT (conversation, ts) DO UPDATE SET thread_ts = excluded.thread_ts, thread_root = excluded.thread_root, user_id = excluded.user_id, " +
        "text = excluded.text, blocks = excluded.blocks, reactions = excluded.reactions, subtype = excluded.subtype, edited_ts = excluded.edited_ts, " +
        "reply_count = excluded.reply_count, latest_reply_ts = excluded.latest_reply_ts, partial = (messages.partial AND excluded.partial), " +
        "file_ids = excluded.file_ids, mine = excluded.mine, names_me = excluded.names_me";

    internal const string SelectRootOf = "SELECT thread_root FROM messages WHERE conversation = $c AND ts = $ts";

    internal const string DeleteMessage = "DELETE FROM messages WHERE conversation = $c AND ts = $ts";

    /// <summary>
    /// The Needs reply rebuild of one thread (W7): writes that thread's row only, from its own messages. needs_reply is §7.1 items 1 to 3: the thread is
    /// mine (a DM or group DM, or I wrote in it, or a message names me) and its newest message is not mine (so I wrote nothing after it). Done, snooze and
    /// a linked owned issue (item 4) are Home's row state and apply on read. Writes nothing when the thread has no messages.
    /// </summary>
    internal const string RebuildThread =
        "WITH unit AS (SELECT ts, user_id, mine, names_me FROM messages WHERE conversation = $c AND thread_root = $root), " +
        "newest AS (SELECT ts, user_id, mine FROM unit ORDER BY ts DESC LIMIT 1) " +
        "INSERT INTO thread_state (conversation, thread_ts, my_last_ts, newest_ts, newest_user, needs_reply, rev) " +
        "SELECT $c, $root, (SELECT MAX(ts) FROM unit WHERE mine = 1), newest.ts, newest.user_id, " +
        "CASE WHEN newest.mine = 0 AND (COALESCE((SELECT kind IN ('dm', 'mpim') FROM conversations WHERE id = $c), 0) = 1 " +
        "OR EXISTS (SELECT 1 FROM unit WHERE mine = 1 OR names_me = 1)) THEN 1 ELSE 0 END, 1 " +
        "FROM newest WHERE 1 " +
        "ON CONFLICT (conversation, thread_ts) DO UPDATE SET my_last_ts = excluded.my_last_ts, newest_ts = excluded.newest_ts, " +
        "newest_user = excluded.newest_user, needs_reply = excluded.needs_reply, text_expired_at = NULL, rev = thread_state.rev + 1";

    /// <summary>A thread left with no messages loses its derived columns, so no gist outlives its messages; the owner columns stay.</summary>
    internal const string ClearEmptyThread =
        "UPDATE thread_state SET my_last_ts = NULL, newest_ts = NULL, newest_user = NULL, needs_reply = 0, gist = NULL, gist_key = NULL, rev = rev + 1 " +
        "WHERE conversation = $c AND thread_ts = $root AND (newest_ts IS NOT NULL OR gist IS NOT NULL OR my_last_ts IS NOT NULL) " +
        "AND NOT EXISTS (SELECT 1 FROM messages WHERE conversation = $c AND thread_root = $root)";

    /// <summary>A kept thread whose skip ended at the cap: its text went, its row says "text expired"; needs_reply and the owner columns stay.</summary>
    internal const string ExpireThreadText =
        "INSERT INTO thread_state (conversation, thread_ts, text_expired_at, rev) VALUES ($c, $root, $at, 1) " +
        "ON CONFLICT (conversation, thread_ts) DO UPDATE SET my_last_ts = NULL, gist = NULL, gist_key = NULL, text_expired_at = excluded.text_expired_at, " +
        "rev = thread_state.rev + 1";

    /// <summary>
    /// A kept thread the cap took only part of: the skip ended for the old text, so the row says "text expired", but the thread keeps its needs_reply and
    /// owner columns; only the text-derived columns move to what is left. A rebuild here would drop a thread nobody answered once its anchor (my message,
    /// or one naming me) went.
    /// </summary>
    internal const string PartExpireThreadText =
        "UPDATE thread_state SET my_last_ts = (SELECT MAX(ts) FROM messages WHERE conversation = $c AND thread_root = $root AND mine = 1), " +
        "newest_ts = (SELECT MAX(ts) FROM messages WHERE conversation = $c AND thread_root = $root), " +
        "newest_user = (SELECT user_id FROM messages WHERE conversation = $c AND thread_root = $root ORDER BY ts DESC LIMIT 1), " +
        "gist = NULL, gist_key = NULL, text_expired_at = $at, rev = rev + 1 WHERE conversation = $c AND thread_ts = $root";

    internal const string ThreadHasMessages = "SELECT EXISTS (SELECT 1 FROM messages WHERE conversation = $c AND thread_root = $root)";

    internal const string UpsertOwnerState =
        "INSERT INTO thread_state (conversation, thread_ts, later, follow, rev) VALUES ($c, $root, $later, $follow, 1) " +
        "ON CONFLICT (conversation, thread_ts) DO UPDATE SET later = excluded.later, follow = excluded.follow, rev = thread_state.rev + 1";

    /// <summary>Every thread holding a message older than $cut, with its Needs reply column and whether it has owner state (Later, follow, a draft, an unsent outbox row).</summary>
    internal const string SelectRetentionThreads =
        "SELECT c.workspace, c.kind, m.conversation, m.thread_root, COALESCE(t.needs_reply, 0), " +
        "CASE WHEN COALESCE(t.later, 0) = 1 OR COALESCE(t.follow, 0) = 1 " +
        "OR EXISTS (SELECT 1 FROM drafts d WHERE d.conversation = m.conversation AND d.thread_ts = m.thread_root) " +
        "OR EXISTS (SELECT 1 FROM outbox o WHERE o.conversation = m.conversation AND o.thread_ts = m.thread_root AND o.state NOT IN ('sent', 'discarded')) " +
        "THEN 1 ELSE 0 END " +
        "FROM (SELECT DISTINCT conversation, thread_root FROM messages WHERE ts < $cut) m " +
        "JOIN conversations c ON c.id = m.conversation " +
        "LEFT JOIN thread_state t ON t.conversation = m.conversation AND t.thread_ts = m.thread_root " +
        "ORDER BY m.conversation, m.thread_root";

    internal const string CreateKeep = "CREATE TEMP TABLE IF NOT EXISTS retain_keep (id INTEGER PRIMARY KEY)";

    internal const string ClearKeep = "DELETE FROM temp.retain_keep";

    internal const string DropKeep = "DROP TABLE IF EXISTS temp.retain_keep";

    /// <summary>A kept channel thread keeps its own messages (root and replies) newer than the cap.</summary>
    internal const string KeepThread =
        "INSERT OR IGNORE INTO temp.retain_keep (id) SELECT id FROM messages WHERE conversation = $c AND thread_root = $root AND ts >= $cap";

    /// <summary>And its bounded context: the 10 top-level messages before its root and the 10 after, newer than the cap.</summary>
    internal const string KeepContext =
        "INSERT OR IGNORE INTO temp.retain_keep (id) " +
        "SELECT id FROM (SELECT id FROM messages WHERE conversation = $c AND thread_root = ts AND ts < $root AND ts >= $cap ORDER BY ts DESC LIMIT 10) " +
        "UNION ALL SELECT id FROM (SELECT id FROM messages WHERE conversation = $c AND thread_root = ts AND ts > $root AND ts >= $cap ORDER BY ts LIMIT 10)";

    /// <summary>A kept DM or group DM (one thread, its conversation) keeps its 20 newest messages newer than the cap.</summary>
    internal const string KeepDm =
        "INSERT OR IGNORE INTO temp.retain_keep (id) " +
        "SELECT id FROM (SELECT id FROM messages WHERE conversation = $c AND thread_root = '' AND ts >= $cap ORDER BY ts DESC LIMIT 20)";

    /// <summary>Each thread losing messages to this pass, and whether the 180-day cap takes any of them (1) or only the 30-day cut does (0).</summary>
    internal const string SelectExpiringThreads =
        "SELECT conversation, thread_root, MAX(ts < $cap) FROM messages WHERE ts < $cut AND id NOT IN (SELECT id FROM temp.retain_keep) " +
        "GROUP BY conversation, thread_root";

    internal const string DeleteExpired = "DELETE FROM messages WHERE ts < $cut AND id NOT IN (SELECT id FROM temp.retain_keep)";

    internal const string DeleteLandedOutbox = "DELETE FROM outbox WHERE state IN ('sent', 'discarded') AND created_at < $at";

    internal const string PurgeMessagesOf = "DELETE FROM messages WHERE conversation = $c";

    internal const string PurgeDerivedOf =
        "UPDATE thread_state SET my_last_ts = NULL, newest_ts = NULL, newest_user = NULL, needs_reply = 0, gist = NULL, gist_key = NULL, rev = rev + 1 " +
        "WHERE conversation = $c";

    internal const string PurgeLandedOutboxOf = "DELETE FROM outbox WHERE state IN ('sent', 'discarded') AND conversation = $c";

    internal const string PurgeOwnerThreadsOf = "DELETE FROM thread_state WHERE conversation = $c";

    internal const string PurgeDraftsOf = "DELETE FROM drafts WHERE conversation = $c";

    internal const string PurgeOutboxOf = "DELETE FROM outbox WHERE conversation = $c";

    internal const string PurgeMessages = "DELETE FROM messages";

    internal const string PurgeDerived =
        "UPDATE thread_state SET my_last_ts = NULL, newest_ts = NULL, newest_user = NULL, needs_reply = 0, gist = NULL, gist_key = NULL, rev = rev + 1";

    internal const string PurgeLandedOutbox = "DELETE FROM outbox WHERE state IN ('sent', 'discarded')";

    internal const string PurgeUsers = "DELETE FROM users";

    internal const string PurgeConversations = "DELETE FROM conversations";

    internal const string PurgeOwnerThreads = "DELETE FROM thread_state";

    internal const string PurgeDrafts = "DELETE FROM drafts";

    internal const string PurgeOutbox = "DELETE FROM outbox";
}
