namespace Aw.Sindri.Core.Sessions;

/// <summary>A session the ledger said appeared.</summary>
/// <param name="Pid">The agent's process id, or null when restored from the ledger and not yet seen.</param>
/// <param name="StartTicks">Its start ticks, or null.</param>
/// <param name="AskingReported">True while the current asking episode is ledgered (or capped).</param>
/// <param name="Asks">When 'asking' was ledgered for it in the last hour.</param>
public sealed record ReportedSession(int? Pid, long? StartTicks, bool AskingReported, IReadOnlyList<DateTimeOffset> Asks)
{
    /// <summary>The profile repo it appeared under; its 'gone' row carries the same, so a repo-scoped reader never sees another repo's departures.</summary>
    public Ids.RepoName? Repo { get; init; }

    /// <summary>The host kind it appeared with.</summary>
    public string HostKind { get; init; } = "unknown";

    /// <summary>The Sindri launch it appeared with.</summary>
    public Launch.LaunchId? LaunchId { get; init; }
}
