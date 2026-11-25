namespace Aw.Sindri.Core.Messages;

/// <summary>A thread as the store reads it (messages spec §8.2 GET /threads/{conversation}/{ts}): its root and replies, and its list row.</summary>
/// <param name="Root">The root line.</param>
/// <param name="Replies">The replies, oldest first.</param>
/// <param name="Row">The thread's row when it is in Needs reply or Unread, else null.</param>
public sealed record StoredThread(MessageLine Root, IReadOnlyList<MessageLine> Replies, ThreadRow? Row);
