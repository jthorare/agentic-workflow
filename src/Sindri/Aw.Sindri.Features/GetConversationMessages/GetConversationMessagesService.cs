using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetConversationMessages;

/// <summary>A stub until M1f (messages spec §14): the conversation window.</summary>
internal sealed class GetConversationMessagesService : IService<GetConversationMessagesRequest, GetConversationMessagesResponse>
{
    public ValueTask<Result<GetConversationMessagesResponse>> Handle(GetConversationMessagesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetConversationMessages is not built yet: messages spec §14 phase M1f");
}
