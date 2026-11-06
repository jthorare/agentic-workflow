using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Inbox;

/// <summary>Lists the inbound messages still owed a reply, or with all, every message with its state (open, answered, dismissed or deferred).</summary>
/// <param name="config">The team config, for the meta lane.</param>
/// <param name="messages">The messages.</param>
/// <param name="reader">The open obligations.</param>
/// <param name="clock">Decides whether a deferral is still running.</param>
internal sealed class ListInboxService(IMetaConfig config, IMessageStore messages, ObligationReader reader, TimeProvider clock) : IService<ListInboxRequest, ListInboxResponse>
{
    public async ValueTask<Result<ListInboxResponse>> Handle(ListInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (team.IsError || inbound.IsError)
        {
            return team.IsError ? team.Error : inbound.Error;
        }

        var now = clock.GetUtcNow();
        var open = await reader.Open(team.Value.MetaLane, now, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var owed = open.Value.Where(o => o.Kind == ObligationKind.Reply).Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        var items = inbound.Value
            .Select(m => Item(m, owed.Contains(m.Id.Value), now))
            .Where(i => request.All || string.Equals(i.State, InboxState.Open, StringComparison.Ordinal))
            .ToList();
        return new ListInboxResponse(items);
    }

    private static InboxItem Item(InboxMessage message, bool owed, DateTimeOffset now)
    {
        var deferred = InboxState.IsDeferred(message, now);
        var state = InboxState.Of(message, owed, now);
        return new InboxItem(
            message.Id,
            message.FromName.Length > 0 ? message.FromName : message.From,
            message.Body,
            message.At,
            message.Class.Kind.ToString(),
            state,
            deferred ? message.Resolution?.Until : null);
    }
}
