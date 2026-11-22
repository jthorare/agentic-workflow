using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.GetMessageThread;

/// <summary>
/// A thread: its root, its replies and its Needs reply or Unread row (messages spec §8.2). A session gets the ts, author ids and the row's keys and
/// flags, never the text or an author's name (W17).
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="callers">The caller, for the session filter.</param>
internal sealed class GetMessageThreadService(IMessagesStore store, ICallerContextAccessor callers) : IService<GetMessageThreadRequest, GetMessageThreadResponse>
{
    public async ValueTask<Result<GetMessageThreadResponse>> Handle(GetMessageThreadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var thread = await store.Thread(request.Conversation, request.Thread, cancellationToken).ConfigureAwait(false);
        if (thread.IsError)
        {
            return thread.Error;
        }

        var found = thread.Value;
        return MessageReadFilter.ShowsText(callers.Current)
            ? new GetMessageThreadResponse(found.Root, found.Replies, found.Row)
            : new GetMessageThreadResponse(MessageReadFilter.WithoutText(found.Root), [.. found.Replies.Select(MessageReadFilter.WithoutText)],
                found.Row is { } row ? MessageReadFilter.WithoutText(row) : null);
    }
}
