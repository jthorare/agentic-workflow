using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListConversations;

/// <summary>
/// The conversation list and the jump list (messages spec §8.2): starred first, filtered by starred and by a name fragment. A session gets the ids,
/// kinds, stars and unread counts, never a name (W17).
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="callers">The caller, for the session filter.</param>
internal sealed class ListConversationsService(IMessagesStore store, ICallerContextAccessor callers) : IService<ListConversationsRequest, ListConversationsResponse>
{
    public async ValueTask<Result<ListConversationsResponse>> Handle(ListConversationsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversations = await store.Conversations(cancellationToken).ConfigureAwait(false);
        if (conversations.IsError)
        {
            return conversations.Error;
        }

        var names = MessageReadFilter.ShowsText(callers.Current);
        var query = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query.Trim();
        var rows = conversations.Value
            .Where(c => request.Starred is not { } starred || c.Starred == starred)
            .Where(c => query is null || c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(c => new ConversationListRow(c.Conversation, c.Kind, names ? c.Name : null, c.Starred, c.Unread))
            .ToList();
        return new ListConversationsResponse(rows);
    }
}
