using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>What a pass found.</summary>
/// <param name="Changes">The changes worth a ledger row.</param>
/// <param name="Good">False when the process table failed or the pass overran: the snapshot is the previous one.</param>
/// <param name="Sessions">How many sessions are live.</param>
/// <param name="Pruned">How many dead sessions' state files the pass deleted.</param>
public sealed record CollectSessionsResponse(IReadOnlyList<FleetChange> Changes, bool Good, int Sessions, int Pruned)
{
    /// <summary>True when the sources took longer than the budget.</summary>
    public bool Overran { get; init; }

    /// <summary>The state changes worth a 'session.changed' row, oldest first.</summary>
    public IReadOnlyList<SessionStateChange> StateChanges { get; init; } = [];
}
