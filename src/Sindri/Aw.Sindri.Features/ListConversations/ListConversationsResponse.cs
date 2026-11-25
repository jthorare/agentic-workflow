namespace Aw.Sindri.Features.ListConversations;

/// <summary>The conversations (messages spec §8.2).</summary>
/// <param name="Conversations">The conversations, starred first.</param>
public sealed record ListConversationsResponse(IReadOnlyList<ConversationListRow> Conversations);
