using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one read of a (connection, event kind) did, summed over its provider calls.</summary>
/// <param name="Events">Events the provider returned, each delivery id counted once.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped.</param>
/// <param name="Capped">True when the read stopped at a cap and recorded catchup.capped.</param>
/// <param name="RateLimited">True when the provider asked to wait.</param>
internal sealed record ReadOutcome(int Events, int Started, int Skipped, bool Capped, bool RateLimited);

/// <summary>Reads one (connection, event kind) forward: one provider call for a regular poll, repeated calls for a catch-up.</summary>
internal sealed class CatchUp
{
    internal CatchUp(PollRound round, ConnectionEvents events) => _ = (round, events);

    internal ValueTask<Result<ReadOutcome>> Read(PollGroup group, Connection connection, IEventPoller poller, PollCursor? cursor, int pages, bool forward, LeaseGrant grant, CancellationToken cancellationToken) =>
        throw new NotSupportedException("unbuilt: " + group + connection + poller + cursor + pages + forward + grant + cancellationToken);
}
