using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.InboxDismiss;

/// <summary>Closes a message's reply obligation without a reply, keeping the reason.</summary>
/// <param name="messages">Stores the resolution.</param>
/// <param name="events">Receives obligation.closed; null to write none.</param>
/// <param name="clock">Dates the event.</param>
internal sealed class DismissInboxService(IMessageStore messages, MetaEventSink? events = null, TimeProvider? clock = null) : IService<DismissInboxRequest, DismissInboxResponse>
{
    internal MetaEventSink? Events { get; } = events;

    internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public async ValueTask<Result<DismissInboxResponse>> Handle(DismissInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await messages.Resolve(request.Id, new MessageResolution(MessageResolutionKind.Dismissed, request.Reason.Trim()), cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        await WriteClosed(request, cancellationToken).ConfigureAwait(false);
        return new DismissInboxResponse(request.Id);
    }

    private async ValueTask WriteClosed(DismissInboxRequest request, CancellationToken cancellationToken)
    {
        if (Events is null)
        {
            return;
        }

        var all = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (all.IsSuccess && all.Value.FirstOrDefault(m => m.Id == request.Id) is { } message)
        {
            var who = message.FromName.Length > 0 ? message.FromName : message.From;
            await Events.Emit(MetaEvents.ObligationClosed(Clock.GetUtcNow(), message.Id.Value, who, who), cancellationToken).ConfigureAwait(false);
        }
    }
}
