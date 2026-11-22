using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetConversationMessages;

/// <summary>
/// A window of a conversation's top-level lines with the gap markers that touch it (messages spec §8.2, W10). A session gets the ts, author ids, thread
/// and reply counts, never the text or an author's name (W17).
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="callers">The caller, for the session filter.</param>
internal sealed class GetConversationMessagesService(IMessagesStore store, ICallerContextAccessor callers) : IService<GetConversationMessagesRequest, GetConversationMessagesResponse>
{
    internal const int MaxLimit = 200;

    public async ValueTask<Result<GetConversationMessagesResponse>> Handle(GetConversationMessagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Before is not null && request.After is not null)
        {
            return SindriErrors.InvalidQuery.With("pass 'before' or 'after', not both");
        }

        var window = await store.Window(request.Conversation, request.Before, request.After, Math.Clamp(request.Limit, 1, MaxLimit), cancellationToken).ConfigureAwait(false);
        if (window.IsError)
        {
            return window.Error;
        }

        var lines = MessageReadFilter.ShowsText(callers.Current) ? window.Value.Lines : [.. window.Value.Lines.Select(MessageReadFilter.WithoutText)];
        return new GetConversationMessagesResponse(request.Conversation, lines, window.Value.Gaps);
    }
}
