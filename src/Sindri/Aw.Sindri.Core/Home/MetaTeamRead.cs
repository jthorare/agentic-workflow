namespace Aw.Sindri.Core.Home;

/// <summary>What the home-lanes collector read from aw-meta in one pass (home spec §3.3): the lanes' context, the merge queue, usage and the team's repository.</summary>
/// <param name="RecycleAt">The recycle mark in tokens ('lanes-context' recycleAt).</param>
/// <param name="Lanes">Each lane's context size and verdict.</param>
/// <param name="MergeQueue">The pull requests lanes sent 'ready' for, in queue order.</param>
/// <param name="Usage">Each subject's usage standing.</param>
/// <param name="RepoDirectory">The selected team's repository directory, or null when aw-meta did not say.</param>
/// <param name="Unavailable">Why aw-meta could not be read (missing, exit 2, JSON that does not parse), or null when it was.</param>
public sealed record MetaTeamRead(long RecycleAt, IReadOnlyList<MetaLane> Lanes, IReadOnlyList<MetaQueuedPr> MergeQueue, IReadOnlyList<MetaUsage> Usage, string? RepoDirectory, string? Unavailable)
{
    /// <summary>A read that found no aw-meta team.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The read, with no lanes.</returns>
    public static MetaTeamRead NotAvailable(string reason) => new(0, [], [], [], RepoDirectory: null, reason);
}
