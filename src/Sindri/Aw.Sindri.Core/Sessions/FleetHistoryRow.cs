namespace Aw.Sindri.Core.Sessions;

/// <summary>A 'fleet.changed' row read back from the ledger at start, to rebuild what was last reported (collector spec §4.1 step 9).</summary>
/// <param name="Ref">The session.</param>
/// <param name="Kind">What the row said.</param>
/// <param name="At">When the row was observed.</param>
public sealed record FleetHistoryRow(SessionRef Ref, FleetChangeKind Kind, DateTimeOffset At)
{
    /// <summary>The repo the row carried.</summary>
    public Ids.RepoName? Repo { get; init; }

    /// <summary>The host kind the row carried.</summary>
    public string HostKind { get; init; } = "unknown";

    /// <summary>The launch the row carried.</summary>
    public Launch.LaunchId? LaunchId { get; init; }
}
