using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListConversations;

/// <summary>The conversation list and the jump list (GET /sindri/messages/conversations?starred=&amp;q=, 'sindri messages conversations'; messages spec §8.2).</summary>
/// <param name="Starred">Only starred ones when true, only the others when false, all when null.</param>
/// <param name="Query">A name filter for the jump list, or null.</param>
public sealed record ListConversationsRequest(bool? Starred, string? Query) : IQuery<ListConversationsResponse>;
