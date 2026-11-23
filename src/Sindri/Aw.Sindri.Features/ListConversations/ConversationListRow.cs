using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListConversations;

/// <summary>One conversation in the list (messages spec §8.1 conversations table).</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Name">Its name; a session's answer carries none (W17).</param>
/// <param name="Starred">Whether it is starred.</param>
/// <param name="Unread">How many lines sit past the local read pointer.</param>
public sealed record ConversationListRow(string Conversation, ConversationKind Kind, string? Name, bool Starred, int Unread);
