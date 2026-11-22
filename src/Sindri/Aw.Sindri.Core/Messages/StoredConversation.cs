namespace Aw.Sindri.Core.Messages;

/// <summary>
/// A conversation as the sync writes it into messages.db (messages spec §8.1, the conversations table). The owner's columns (starred, local mute) and the read
/// pointers are not part of it: a write keeps them.
/// </summary>
/// <param name="Workspace">The Slack workspace ('T0HARBOR').</param>
/// <param name="Conversation">The conversation ('C0HARBOR', 'D0SAM').</param>
/// <param name="Kind">What it is.</param>
/// <param name="Name">The channel name, or the other person's id for a DM.</param>
/// <param name="ExtShared">True for a Slack Connect conversation.</param>
public sealed record StoredConversation(string Workspace, string Conversation, ConversationKind Kind, string Name, bool ExtShared);
