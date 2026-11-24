namespace Aw.Sindri.Core.Messages;

/// <summary>One conversation as the store reads it for the conversation list (messages spec §8.1 conversations table, §8.2 GET /conversations).</summary>
/// <param name="Conversation">The Slack conversation ('C0HARBOR').</param>
/// <param name="Kind">What it is.</param>
/// <param name="Name">The channel name, or the other person's id for a DM; never in a session's answer (W17).</param>
/// <param name="Starred">Whether the owner starred it.</param>
/// <param name="Unread">How many top-level lines from other people sit past the local read pointer.</param>
public sealed record ConversationSummary(string Conversation, ConversationKind Kind, string Name, bool Starred, int Unread);
