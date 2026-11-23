using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetConversationMessages;

/// <summary>A conversation window (messages spec §8.2): every hole in it has a marker (W10).</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Lines">The lines, oldest first.</param>
/// <param name="Gaps">The gap markers inside the window.</param>
public sealed record GetConversationMessagesResponse(string Conversation, IReadOnlyList<MessageLine> Lines, IReadOnlyList<GapMarker> Gaps);
