namespace Aw.Sindri.Core.Sessions;

/// <summary>The collector's latest pass (collector spec §2): every session, the recently ended ones, one problem per failed source, and when it was taken.</summary>
/// <param name="Sessions">The live sessions.</param>
/// <param name="RecentlyEnded">The sessions that ended in the last 10 minutes.</param>
/// <param name="Problems">One row per source that failed.</param>
/// <param name="ObservedAt">When the pass ran.</param>
public sealed record SessionSnapshot(
    IReadOnlyList<ObservedSession> Sessions, IReadOnlyList<EndedSession> RecentlyEnded, IReadOnlyList<SessionProblem> Problems, DateTimeOffset ObservedAt)
{
    /// <summary>How many passes in a row kept the previous list because the process table overran (#4244); 0 after a full pass. <see cref="ObservedAt"/> is then the last full pass.</summary>
    public int ConsecutiveOverruns { get; init; }

    /// <summary>The last 20 state changes of each live session in this Sindri run, oldest first (collector spec CA-2).</summary>
    public IReadOnlyDictionary<SessionRef, IReadOnlyList<SessionStateEntry>> History { get; init; } = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>>();
}
