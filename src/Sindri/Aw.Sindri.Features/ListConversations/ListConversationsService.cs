using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListConversations;

/// <summary>A stub until M1f (messages spec §14): the conversation list.</summary>
internal sealed class ListConversationsService : IService<ListConversationsRequest, ListConversationsResponse>
{
    public ValueTask<Result<ListConversationsResponse>> Handle(ListConversationsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ListConversations is not built yet: messages spec §14 phase M1f");
}
