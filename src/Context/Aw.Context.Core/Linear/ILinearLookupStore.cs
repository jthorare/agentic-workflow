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
}
