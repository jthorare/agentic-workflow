using Aw.Kernel.Services;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ConnectMessages;
using Aw.Sindri.Features.GetConversationMessages;
using Aw.Sindri.Features.GetMessagesStatus;
using Aw.Sindri.Features.GetMessageThread;
using Aw.Sindri.Features.ListConversations;
using Aw.Sindri.Features.ListMessageRows;
using Aw.Sindri.Features.MarkAllRead;
using Aw.Sindri.Features.MoveReadPointer;
using Aw.Sindri.Features.ResolveLinks;
using Aw.Sindri.Features.RetainMessages;
using Aw.Sindri.Features.SearchMessages;
using Aw.Sindri.Features.SyncMessages;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the Messages slices (messages spec §8.2, §14 M1s): a service its phase has not built yet is a stub that throws.</summary>
internal static class MessagesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Release 1 reads (status, lists, conversations, a conversation window, a thread, search, links) behind the query chain and the local writes
    /// (connect, the read pointer, Mark all read) behind the command chain. 'messages-retention' (M1c) is a built-in scheduled service; 'messages-sync' is
    /// added as its own type only, so the scheduler does not run it before M1e registers it as IScheduledService.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddMessagesSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetMessagesStatusRequest>, AcceptAllValidator<GetMessagesStatusRequest>>().AddQuery<GetMessagesStatusRequest, GetMessagesStatusResponse, GetMessagesStatusService>()
            .AddScoped<IValidator<ListMessageRowsRequest>, AcceptAllValidator<ListMessageRowsRequest>>().AddQuery<ListMessageRowsRequest, ListMessageRowsResponse, ListMessageRowsService>()
            .AddScoped<IValidator<ListConversationsRequest>, AcceptAllValidator<ListConversationsRequest>>().AddQuery<ListConversationsRequest, ListConversationsResponse, ListConversationsService>()
            .AddScoped<IValidator<GetConversationMessagesRequest>, AcceptAllValidator<GetConversationMessagesRequest>>().AddQuery<GetConversationMessagesRequest, GetConversationMessagesResponse, GetConversationMessagesService>()
            .AddScoped<IValidator<GetMessageThreadRequest>, AcceptAllValidator<GetMessageThreadRequest>>().AddQuery<GetMessageThreadRequest, GetMessageThreadResponse, GetMessageThreadService>()
            .AddScoped<IValidator<SearchMessagesRequest>, AcceptAllValidator<SearchMessagesRequest>>().AddQuery<SearchMessagesRequest, SearchMessagesResponse, SearchMessagesService>()
            .AddScoped<IValidator<ResolveLinksRequest>, AcceptAllValidator<ResolveLinksRequest>>().AddQuery<ResolveLinksRequest, ResolveLinksResponse, ResolveLinksService>()
            .AddScoped<IValidator<ConnectMessagesRequest>, AcceptAllValidator<ConnectMessagesRequest>>().AddCommand<ConnectMessagesRequest, ConnectMessagesResponse, ConnectMessagesService>()
            .AddScoped<IValidator<MoveReadPointerRequest>, AcceptAllValidator<MoveReadPointerRequest>>().AddCommand<MoveReadPointerRequest, MoveReadPointerResponse, MoveReadPointerService>()
            .AddScoped<IValidator<MarkAllReadRequest>, AcceptAllValidator<MarkAllReadRequest>>().AddCommand<MarkAllReadRequest, MarkAllReadResponse, MarkAllReadService>()
            .AddScoped<SyncMessagesService>()
            .AddSingleton<LedgerSnoozes>()
            .AddScoped<IScheduledService, RetainMessagesService>()
            .AddSingleton(RetainMessagesService.BuiltIn);   // the nightly retention pass runs without a profile entry, so PauseService refuses it
    }
}
