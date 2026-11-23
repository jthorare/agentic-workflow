using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetConversationMessages;

/// <summary>A window of a conversation's lines with its gap markers (GET /sindri/messages/conversations/{id}/messages?before=&amp;after=&amp;limit=; messages spec §8.2).</summary>
/// <param name="Conversation">The Slack conversation.</param>
/// <param name="Before">Lines older than this ts, or null.</param>
/// <param name="After">Lines newer than this ts, or null.</param>
/// <param name="Limit">At most this many lines.</param>
public sealed record GetConversationMessagesRequest(string Conversation, MessageTs? Before, MessageTs? After, int Limit) : IQuery<GetConversationMessagesResponse>;
