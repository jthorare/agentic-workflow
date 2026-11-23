using Aw.Kernel.Services;
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

/// <summary>Registers the Messages slices (messages spec §8.2, §14 M1s): every service is a stub that throws until its phase builds it.</summary>
internal static class MessagesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Release 1 reads (status, lists, conversations, a conversation window, a thread, search, links) behind the query chain and the local writes
    /// (connect, the read pointer, Mark all read) behind the command chain. The two scheduled services are added as their own types only, so the scheduler
    /// does not run them before M1c and M1e register them as IScheduledService.
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
            .AddScoped<RetainMessagesService>();
    }
}
