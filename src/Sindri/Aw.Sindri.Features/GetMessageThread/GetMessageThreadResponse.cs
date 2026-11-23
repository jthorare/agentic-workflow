using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetMessageThread;

/// <summary>A thread (messages spec §8.2).</summary>
/// <param name="Root">The root line.</param>
/// <param name="Replies">The replies, oldest first.</param>
/// <param name="Row">The thread's Needs reply or Unread row, or null when it is in neither.</param>
public sealed record GetMessageThreadResponse(MessageLine Root, IReadOnlyList<MessageLine> Replies, ThreadRow? Row);
