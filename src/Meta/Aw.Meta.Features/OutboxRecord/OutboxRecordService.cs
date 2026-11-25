using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>
/// Stores a message the meta session sent. Nothing else is stored: the obligations the send closes follow from it (a reply to
/// that peer, a lane-map or main-moved message to a lane, an owner answer to the asker). The response names what closed.
/// </summary>
/// <param name="session">Tells the meta session from the rest.</param>
/// <param name="messages">Stores the send.</param>
/// <param name="reader">Evaluates the open set before and after.</param>
/// <param name="clock">Dates the send.</param>
/// <param name="obligations">Writes obligation.closed for each reply the send closes; null to write none.</param>
internal sealed class OutboxRecordService(MetaSession session, IMessageStore messages, ObligationReader reader, TimeProvider clock, ObligationEventWriter? obligations = null) : IService<OutboxRecordRequest, OutboxRecordResponse>
{
    public async ValueTask<Result<OutboxRecordResponse>> Handle(OutboxRecordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await session.Resolve(request.Session, cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var warning = NoticeRule.StatusAskWarning(request.Message);
        if (team.Value is not { } config)
        {
            return new OutboxRecordResponse(Recorded: false, []) { Warning = warning };
        }

        var before = await reader.Open(config.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var stored = await messages.RecordOutbound(new Outbound(request.To, request.Message, clock.GetUtcNow()) { Id = request.Id }, cancellationToken).ConfigureAwait(false);
        if (before.IsError || stored.IsError)
        {
            return before.IsError ? before.Error : stored.Error;
        }

        var after = await reader.Open(config.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (after.IsError)
        {
            return after.Error;
        }

        var closed = Closed(before.Value, after.Value);
        await WriteClosed(closed, cancellationToken).ConfigureAwait(false);
        return new OutboxRecordResponse(Recorded: true, closed) { Warning = warning };
    }

    // One obligation.closed per reply the send closed (the relay of an owner answer has no sender to name).
    private async ValueTask WriteClosed(List<Obligation> closed, CancellationToken cancellationToken)
    {
        if (obligations is null || closed.TrueForAll(static o => o.Kind != ObligationKind.Reply))
        {
            return;
        }

        var all = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return;
        }

        foreach (var obligation in closed.Where(static o => o.Kind == ObligationKind.Reply))
        {
            if (all.Value.FirstOrDefault(m => string.Equals(m.Id.Value, obligation.Key, StringComparison.Ordinal)) is { } message)
            {
                var who = message.FromName.Length > 0 ? message.FromName : message.From;
                await obligations.Closed(obligation.Key, who, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static List<Obligation> Closed(IReadOnlyList<Obligation> before, IReadOnlyList<Obligation> after) =>
        [.. before.Where(b => !after.Any(a => a.Kind == b.Kind && string.Equals(a.Key, b.Key, StringComparison.Ordinal)))];
}
