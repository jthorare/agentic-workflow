using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Append the pass's changes as 'fleet.changed' events, under the lease (collector spec CO3). An internal write: no command gate, audit row or catalog row.</summary>
/// <param name="Grant">The lease grant the collector writes under.</param>
/// <param name="Changes">The changes.</param>
public sealed record RecordFleetChangesRequest(LeaseGrant Grant, IReadOnlyList<FleetChange> Changes)
{
    /// <summary>The state changes to append as 'session.changed' after the fleet changes.</summary>
    public IReadOnlyList<SessionStateChange> StateChanges { get; init; } = [];
}
