using Aw.Kernel;

namespace Aw.Sindri.Core.Messages;

/// <summary>The Messages store, messages.db (messages spec §8.1): a cache plus owner state, never a view of the ledger.</summary>
public interface IMessagesStore
{
    /// <summary>Recomputes Needs reply for one thread after its messages changed (W7): only that thread's row is touched.</summary>
    /// <param name="conversation">The Slack conversation.</param>
    /// <param name="thread">The thread root's ts; a DM passes its newest ts.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The keys of the rows the rebuild touched.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> RebuildNeedsReply(string conversation, MessageTs thread, CancellationToken cancellationToken);

    /// <summary>Writes a conversation's row; the owner's columns and the read pointers of an existing row stay.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-MSG-025.</returns>
    public ValueTask<Result<Unit>> PutConversation(StoredConversation conversation, CancellationToken cancellationToken);

    /// <summary>Writes messages (new ones, or an edit of a known ts) and rebuilds Needs reply for each thread they belong to, and no other (W7).</summary>
    /// <param name="messages">The messages; each conversation's row must exist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The keys of the thread rows rebuilt, or SND-MSG-025.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> PutMessages(IReadOnlyList<StoredMessage> messages, CancellationToken cancellationToken);

    /// <summary>Deletes one message (a Slack delete event) with its FTS row, and rebuilds its thread's row.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="ts">The message's ts.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The keys of the thread rows rebuilt (none when the message was not stored), or SND-MSG-025.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> RemoveMessage(string conversation, MessageTs ts, CancellationToken cancellationToken);

    /// <summary>Sets the owner's flags on a thread; its derived columns stay.</summary>
    /// <param name="state">The flags.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-MSG-025.</returns>
    public ValueTask<Result<Unit>> PutThreadState(ThreadOwnerState state, CancellationToken cancellationToken);

    /// <summary>The threads that hold a message older than <paramref name="olderThan"/>, for the retention pass to classify. No messages.db reads as none.</summary>
    /// <param name="olderThan">The retention cut, as a ts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The threads, or SND-MSG-025.</returns>
    public ValueTask<Result<IReadOnlyList<RetentionThread>>> RetentionThreads(MessageTs olderThan, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes messages older than <paramref name="olderThan"/> (and their FTS rows) except in the <paramref name="keep"/> threads, which keep their messages
    /// and a bounded context of 20 around them unless older than <paramref name="capOlderThan"/>. Landed outbox rows created before
    /// <paramref name="outboxOlderThan"/> go too. A thread left with no messages loses its derived columns (gist, its cache key, my last ts); a kept one says
    /// "text expired".
    /// </summary>
    /// <param name="olderThan">The retention cut (30 days by default).</param>
    /// <param name="capOlderThan">The cap on any skip (180 days).</param>
    /// <param name="outboxOlderThan">The retention cut as a time, for outbox rows.</param>
    /// <param name="keep">The threads the pass skips.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What was removed, or SND-MSG-025.</returns>
    public ValueTask<Result<RetentionOutcome>> Expire(MessageTs olderThan, MessageTs capOlderThan, DateTimeOffset outboxOlderThan, IReadOnlyList<RetentionThread> keep, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the cache (messages spec §8.1 "Two groups of tables"): messages with their FTS rows and the derived columns of thread_state, for one
    /// conversation or all (all also takes the conversations and users); with <paramref name="ownerState"/> also the owner columns, drafts and outbox rows.
    /// </summary>
    /// <param name="conversation">One conversation, or null for all.</param>
    /// <param name="ownerState">True for `purge --all --yes`.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The messages deleted, or SND-MSG-025.</returns>
    public ValueTask<Result<int>> Purge(string? conversation, bool ownerState, CancellationToken cancellationToken);

    /// <summary>
    /// The rows of one list (messages spec §8.2), oldest newest message first, then by conversation: Needs reply by the store's column (§7.1 items 1 to 3),
    /// Unread (threads whose newest message is another person's and sits past the read pointer of an unmuted conversation), Later and Drafts. Each row
    /// carries its newest message's text; the caller drops it for a session. No messages.db reads as none.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or SND-MSG-025.</returns>
    public ValueTask<Result<IReadOnlyList<ThreadRow>>> Rows(MessageList list, CancellationToken cancellationToken);

    /// <summary>Every conversation, starred first, then by name. No messages.db reads as none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The conversations, or SND-MSG-025.</returns>
    public ValueTask<Result<IReadOnlyList<ConversationSummary>>> Conversations(CancellationToken cancellationToken);

    /// <summary>
    /// A window of a conversation's top-level lines, oldest first: the <paramref name="limit"/> lines just newer than <paramref name="after"/>, else just
    /// older than <paramref name="before"/>, else the newest; with the gaps whose edge is one of its lines.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="before">Lines older than this ts, or null.</param>
    /// <param name="after">Lines newer than this ts, or null.</param>
    /// <param name="limit">At most this many lines.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The window, SND-MSG-011 when the store has no such conversation, or SND-MSG-025.</returns>
    public ValueTask<Result<ConversationWindow>> Window(string conversation, MessageTs? before, MessageTs? after, int limit, CancellationToken cancellationToken);

    /// <summary>A thread: its root and replies, oldest first, and its Needs reply or Unread row.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="thread">The root's ts.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The thread, SND-MSG-011 when the store holds no such conversation or root, or SND-MSG-025.</returns>
    public ValueTask<Result<StoredThread>> Thread(string conversation, MessageTs thread, CancellationToken cancellationToken);
}
