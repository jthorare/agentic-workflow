using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>Where the pickers' data is kept: one small table each for states, cycles, members and labels (spec 2026-11-04 §4.2).</summary>
public interface ILinearLookupStore
{
    /// <summary>Replaces everything the store holds for one team, in one transaction: rows Linear no longer lists are gone.</summary>
    /// <param name="team">The team and its states, cycles, members and labels.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> ReplaceTeam(LinearTeamLookups team, CancellationToken cancellationToken);

    /// <summary>Reads what the store holds for one team: states by position, cycles by start, members and labels by name.</summary>
    /// <param name="teamKey">The team's key, 'ENG'.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The team's lists, or null when none was ever stored for that key; the store's error.</returns>
    public ValueTask<Result<LinearTeamLookups?>> Team(string teamKey, CancellationToken cancellationToken);

    /// <summary>Keeps the workspace's URL key, replacing any earlier one.</summary>
    /// <param name="urlKey">The key, 'acme'.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or the store's error.</returns>
    public ValueTask<Result<Unit>> ReplaceWorkspaceUrlKey(string urlKey, CancellationToken cancellationToken);

    /// <summary>Reads the workspace's URL key.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key, or null when none was ever stored; the store's error.</returns>
    public ValueTask<Result<string?>> WorkspaceUrlKey(CancellationToken cancellationToken);
}
