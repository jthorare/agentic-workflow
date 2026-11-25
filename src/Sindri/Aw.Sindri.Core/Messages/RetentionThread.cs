namespace Aw.Sindri.Core.Messages;

/// <summary>
/// A thread with at least one message older than the retention cut (messages spec §8.1), with what the store knows about it. A DM is one thread, its
/// conversation (<see cref="Root"/> is empty).
/// </summary>
/// <param name="Key">The row key: 'slack:thread:&lt;workspace&gt;/&lt;conversation&gt;/&lt;root ts&gt;' or 'slack:dm:&lt;workspace&gt;/&lt;conversation&gt;'.</param>
/// <param name="Conversation">The conversation.</param>
/// <param name="Root">The thread root's ts, or empty for a DM.</param>
/// <param name="NeedsReply">True when the thread waits on my reply by the store's columns (§7.1 items 1 to 3).</param>
/// <param name="OwnerState">True when the thread has a Later or follow flag, a draft or an unsent outbox row.</param>
public sealed record RetentionThread(string Key, string Conversation, string Root, bool NeedsReply, bool OwnerState);
