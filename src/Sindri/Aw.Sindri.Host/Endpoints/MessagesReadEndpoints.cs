using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.GetConversationMessages;
using Aw.Sindri.Features.GetMessagesStatus;
using Aw.Sindri.Features.GetMessageThread;
using Aw.Sindri.Features.ListConversations;
using Aw.Sindri.Features.ListMessageRows;
using Aw.Sindri.Features.SearchMessages;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The Messages reads under /sindri/messages (messages spec §8.2; queries): status, a list, the conversations, a conversation window, a thread and local
/// search. Each binds and calls one service. The lists, conversations, window and thread read messages.db (M1f); status and search are stubs that answer a
/// 500 until M1f's second PR. A session's answer carries no text (W17).
/// </summary>
internal static class MessagesReadEndpoints
{
    private const int DefaultLimit = 50;

    internal static RouteGroupBuilder MapMessagesReads(this RouteGroupBuilder group) => group.MapListReads().MapConversationReads();

    private static RouteGroupBuilder MapListReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/messages/status", static async (IService<GetMessagesStatusRequest, GetMessagesStatusResponse> service, CancellationToken ct) =>
            (await service.Handle(new GetMessagesStatusRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/messages/lists/{list}", static async (string list, string? after, int? limit, IService<ListMessageRowsRequest, ListMessageRowsResponse> service, CancellationToken ct) =>
            List(list) is { } known
                ? (await service.Handle(new ListMessageRowsRequest(known, after, limit ?? DefaultLimit), ct).ConfigureAwait(false)).ToHttpResult()
                : ((Result<ListMessageRowsResponse>)SindriErrors.InvalidQuery.With($"'{list}' is not a list (needs-reply, unread, later, drafts)")).ToHttpResult());
        return group;
    }

    private static RouteGroupBuilder MapConversationReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/messages/conversations", static async (bool? starred, string? q, IService<ListConversationsRequest, ListConversationsResponse> service, CancellationToken ct) =>
            (await service.Handle(new ListConversationsRequest(starred, q), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/messages/conversations/{conversation}/messages", static async (string conversation, string? before, string? after, int? limit, IService<GetConversationMessagesRequest, GetConversationMessagesResponse> service, CancellationToken ct) =>
            (await service.Handle(new GetConversationMessagesRequest(conversation, Ts(before), Ts(after), limit ?? DefaultLimit), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/messages/threads/{conversation}/{ts}", static async (string conversation, string ts, IService<GetMessageThreadRequest, GetMessageThreadResponse> service, CancellationToken ct) =>
            (await service.Handle(new GetMessageThreadRequest(conversation, new MessageTs(ts)), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/messages/search", static async (string? q, string? scope, IService<SearchMessagesRequest, SearchMessagesResponse> service, CancellationToken ct) =>
            Scope(scope) is { } known
                ? (await service.Handle(new SearchMessagesRequest(q ?? string.Empty, known), ct).ConfigureAwait(false)).ToHttpResult()
                : ((Result<SearchMessagesResponse>)SindriErrors.InvalidQuery.With($"'{scope}' is not a search scope (local, slack, everything)")).ToHttpResult());
        return group;
    }

    private static MessageList? List(string slug) => slug switch
    {
        "needs-reply" => MessageList.NeedsReply,
        "unread" => MessageList.Unread,
        "later" => MessageList.Later,
        "drafts" => MessageList.Drafts,
        _ => null,
    };

    private static MessageSearchScope? Scope(string? scope) => scope switch
    {
        null or "local" => MessageSearchScope.Local,
        "slack" => MessageSearchScope.Slack,
        "everything" => MessageSearchScope.Everything,
        _ => null,
    };

    private static MessageTs? Ts(string? text) => text is null ? null : new MessageTs(text);
}
