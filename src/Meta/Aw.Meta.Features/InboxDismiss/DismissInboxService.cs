using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.InboxDismiss;

/// <summary>Closes a message's reply obligation without a reply, keeping the reason.</summary>
/// <param name="messages">Stores the resolution.</param>
/// <param name="obligations">Writes obligation.closed; null to write none.</param>
internal sealed class DismissInboxService(IMessageStore messages, ObligationEventWriter? obligations = null) : IService<DismissInboxRequest, DismissInboxResponse>
{
    public async ValueTask<Result<DismissInboxResponse>> Handle(DismissInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Only a reply that is owed now closes: a second dismiss, an answered message and a notice were never open, or closed already.
        var owed = obligations is not null && await obligations.IsOwed(request.Id.Value, cancellationToken).ConfigureAwait(false);
        var resolved = await messages.Resolve(request.Id, new MessageResolution(MessageResolutionKind.Dismissed, request.Reason.Trim()), cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        if (owed && obligations is not null)
        {
            await WriteClosed(obligations, request, cancellationToken).ConfigureAwait(false);
        }

        return new DismissInboxResponse(request.Id);
    }

    private async ValueTask WriteClosed(ObligationEventWriter writer, DismissInboxRequest request, CancellationToken cancellationToken)
    {
        var all = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (all.IsSuccess && all.Value.FirstOrDefault(m => m.Id == request.Id) is { } message)
        {
            var who = message.FromName.Length > 0 ? message.FromName : message.From;
            await writer.Closed(message.Id.Value, who, cancellationToken).ConfigureAwait(false);
        }
    }
}
